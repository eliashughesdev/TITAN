# AUT-1 — Motor avanzado de automatizaciones empresariales

Implementación de la plataforma de automatizaciones de
TitanMDM. Extiende el motor existente (`AutomationService`) sin
crear uno paralelo: las reglas legacy siguen funcionando en
línea y las reglas empresariales (`DefinitionJson`) se ejecutan
sobre una cola persistente.

- Baseline y auditoría: [`AUT1_BASELINE.md`](./AUT1_BASELINE.md)
- Rama de trabajo: `feat/remote-support-rs-h`

## 1. Arquitectura

```
Evento (dominio)                      Regla empresarial
      │                                     │
      ▼                                     ▼
AutomationService.ProcessEventAsync ──► IAutomationEventIngest
      │  (reglas legacy, en línea)          │  evalúa condiciones
      │                                     ▼
      │                          AutomationQueueService (idempotente)
      │                                     │
      │                                     ▼
      │                          AutomationQueueProcessor
      │                          · anti-ciclos (CycleDepth)
      │                          · concurrencia / min-interval
      │                          · rate-limit por regla
      │                          · timeout y reintentos con backoff
      │                          · simulación sin efectos
      │                                     │
      │                                     ▼
      │                       AutomationActionRegistry → IAutomationActionHandler
      └──────────────────────────► AutomationExecution / AutomationNotification
```

- **Ruta legacy**: reglas sin `DefinitionJson` → ejecución en
  línea dentro de `AutomationService` (sin cambios de contrato).
- **Ruta empresarial**: reglas con `DefinitionJson` → cola
  persistente con reintentos, idempotencia y aislamiento por
  organización.
- **Integración tolerante**: `AutomationService` resuelve
  `IAutomationEventIngest` con `IServiceProvider.GetService<>()`.
  Si el motor no está registrado, las reglas legacy siguen
  operando y las empresariales simplemente no se procesan.

## 2. Punto de integración (único)

El motor se activa con **una sola línea** en el arranque
(`src/backend/TitanMDM.Api/Program.cs`), junto al resto de
extensiones de infraestructura:

```csharp
builder.Services.AddTitanAutomationPlatform(
    builder.Configuration);
```

No se modificó ningún archivo compartido: `Program.cs`,
`App.tsx`, `TitanMdmDbContext.cs`,
`InfrastructureServiceExtensions.cs`, `TitanMdmSeeder.cs` ni el
snapshot de EF quedan intactos por este cambio hasta que el
equipo apruebe el registro.

`AddTitanAutomationPlatform` (en
`src/backend/TitanMDM.Infrastructure/Automation/Enterprise/AutomationEnterpriseServiceCollectionExtensions.cs`)
registra:

- `IAutomationQueueService` / `AutomationQueueService`
- `IAutomationActionRegistry` / `AutomationActionRegistry`
- `IAutomationQueueProcessor` / `AutomationQueueProcessor`
- `IAutomationEventIngest` / `AutomationEventIngest`
- `IAutomationPlatformService` / `AutomationPlatformService`
- `AutomationQueueWorker` (hosted service)
- `AutomationPermissionSeeder` (hosted service)
- opciones `AutomationEngineOptions`

## 3. Modelo de datos

Entidades nuevas (configuradas por
`IEntityTypeConfiguration<T>` y descubiertas automáticamente por
`ApplyConfigurationsFromAssembly`, por lo que **no** se edita
`TitanMdmDbContext.cs`):

| Entidad | Archivo | Descripción |
|---|---|---|
| `AutomationQueueItem` | `Domain/Automation/AutomationQueueItem.cs` | Item de cola con idempotencia, reintentos, lease y estado. |
| `AutomationSchedule` | `Domain/Automation/AutomationSchedule.cs` | Programación por calendario (Once/Interval/Hourly/Daily/Weekly/Monthly). |
| `AutomationNotification` | `Domain/Automation/AutomationNotification.cs` | Notificación de la bandeja del centro de operaciones. |

Entidades extendidas (columnas nuevas):

- `AutomationRule`: `Module`, `Priority`, `ScopeSiteIdsJson`,
  `DefinitionJson`, `ModifiedByUserId`, `ConsecutiveFailures`,
  y `HasDefinition`.
- `AutomationExecution`: `RetryCount`, `DurationMs`,
  `Simulated`, `CycleDepth`, `ActorUserId`.

Configuraciones EF:
`Persistence/Configurations/AutomationRuleConfiguration.cs`,
`AutomationExecutionConfiguration.cs`,
`AutomationScheduleConfiguration.cs`,
`AutomationQueueItemConfiguration.cs`,
`AutomationNotificationConfiguration.cs`.

> **Migraciones EF**: no se ejecutó `dotnet ef` (restricción de
> la tarea). Al integrar, generar la migración con:
> `dotnet ef migrations add AUT1_EnterpriseAutomationEngine`
> y revisar que incluya las tres tablas nuevas y las columnas
> añadidas. El snapshot **no** se editó manualmente.

## 4. Definición de regla (`DefinitionJson`)

Modelo versionado en
`Application/Automation/AutomationRuleDefinition.cs`:

```jsonc
{
  "version": 1,
  "module": "general",          // AutomationModules
  "trigger": "DeviceOffline",   // AutomationEventCatalog (debe estar conectado)
  "priority": 100,              // 0..1000
  "conditions": {               // grupos AND/OR anidados
    "operator": "All",
    "conditions": [
      { "field": "platform", "operator": "Eq", "value": "Windows" }
    ],
    "groups": []
  },
  "actions": [                  // ordenadas, hasta MaxActionsPerRule
    {
      "code": "CreateNotification",
      "order": 1,
      "parameters": { "title": "Equipo offline", "message": "..." },
      "continueOnError": false
    }
  ],
  "policies": {
    "errorPolicy": "StopRule",
    "maxRetries": 2,
    "retryBackoffSeconds": 30,
    "timeoutSeconds": 60,
    "concurrencyPolicy": "SkipIfRunning",
    "minSecondsBetweenExecutions": 0
  },
  "schedule": null,             // requerido si trigger = "Schedule"
  "scopeSiteIds": []
}
```

- **Serialización**: `AutomationDefinitionSerializer` (camelCase,
  tolerante a mayúsculas).
- **Evaluación**: `AutomationConditionEvaluator` (operadores
  tipados, sin `eval`, sin SQL de usuario).
- **Validación**: `AutomationDefinitionValidator` (eventos del
  catálogo y conectados, campos con sintaxis segura, acciones
  registradas y habilitadas, políticas y programación).

## 5. Catálogos

- Eventos: `Events/AutomationEventCatalog.cs`. `Connected = true`
  significa que el evento ya se emite y el motor lo procesa hoy.
  `Connected = false` son contratos preparados para adaptadores
  futuros (Windows, Android, Helpdesk, Ponches).
- Acciones: `Actions/AutomationActionCatalog.cs`. Las acciones de
  alto impacto se registran como `Enabled = false` (stub) y no
  pueden activarse todavía.

## 6. API (`AutomationController`)

Los endpoints legacy conservan su contrato. Los nuevos viven
bajo `platform/*` y usan RBAC (`RequirePermission`) y auditoría
(`[ServiceFilter(typeof(RbacAuditFilter))]`):

| Método | Ruta | Permiso |
|---|---|---|
| GET | `platform/events` | `automation.view` |
| GET | `platform/actions` | `automation.view` |
| POST | `platform/definitions/validate` | `automation.manage` |
| POST | `platform/rules` | `automation.manage` |
| PUT | `platform/rules/{ruleId}` | `automation.manage` |
| POST | `platform/rules/{ruleId}/simulate` | `automation.simulate` |
| GET | `platform/dashboard` | `automation.view` |
| GET | `platform/queue` | `automation.view` |
| GET | `platform/executions/{executionId}` | `automation.view` |
| POST | `platform/executions/{executionId}/retry` | `automation.execute` |
| GET | `platform/notifications` | `automation.view` |
| POST | `platform/notifications/{id}/read` | `automation.view` |
| GET | `platform/rules/{ruleId}/schedule` | `automation.view` |
| PUT | `platform/rules/{ruleId}/schedule` | `automation.manage` |
| DELETE | `platform/rules/{ruleId}/schedule` | `automation.manage` |

Si el motor no está registrado, los endpoints `platform/*`
responden **503** con un mensaje accionable en lugar de fallar
el arranque del controlador.

## 7. Permisos

Definidos en `Application/Automation/AutomationPermissions.cs`
(archivo propio, sin tocar `PermissionCodes.cs`):

- `automation.view`
- `automation.manage`
- `automation.execute`
- `automation.simulate`

Sembrado: `AutomationPermissionSeeder` (hosted service) inserta
los permisos de forma idempotente y los concede a los roles de
sistema `SuperAdmin` activos, replicando la lógica del seeder
principal **sin modificar** `TitanMdmSeeder.cs`.

## 8. Consola React

La consola vive en `src/frontend/titanmdm-web/src/pages/automation/`.
El cliente de API (`src/api/automationApi.ts`) expone los
endpoints `platform/*` (catálogos, validación, reglas
empresariales, simulación, tablero, cola, notificaciones y
programaciones). La página `AutomationPage.tsx` conserva las
pestañas legacy **Reglas** e **Historial** y añade tres pestañas
empresariales, manteniendo intacto el constructor clásico.

Módulos creados (subcomponentes modulares):

- `AutomationDashboardPanel.tsx` — tablero del motor: KPIs
  (reglas activas, ejecuciones 24 h, fallos, tasa de éxito,
  duración media), estado de la cola y alertas recientes.
- `AutomationNotificationsPanel.tsx` — bandeja de notificaciones
  con marcar leída / marcar todo leído; informa el contador de no
  leídas a la pestaña.
- `AutomationEnterprisePanel.tsx` — listado de reglas
  empresariales, activar/pausar (vía `PUT`) y lanzar simulación.
- `AutomationRuleBuilder.tsx` — constructor de definiciones
  versionadas: módulo, prioridad, evento disparador (catálogo),
  grupo de condiciones AND/OR, lista ordenada de acciones con
  parámetros y continuar-si-falla, políticas y programación.
- `AutomationSimulatorPanel.tsx` — simulación por regla con
  trazas de condiciones y acciones planificadas.
- `automationUi.ts` — utilidades de formato y traducción de
  errores (incluye detección del HTTP 503 del motor no
  registrado).

Todas las peticiones `platform/*` resuelven el error 503 con un
aviso explícito («motor no registrado») en lugar de fallar en
silencio. Verificación: `npm run build` (`tsc -b && vite build`) y
`npx eslint src/pages/automation src/api/automationApi.ts` sin
errores.

## 9. Pruebas

`tests/TitanMDM.UnitTests/Automation/`:

- `AutomationDomainTests.cs`: ciclo de vida de la cola
  (idempotencia, reintentos, dead-letter, lease), ejecución
  (duración, estado, simulación) y programaciones.
- `AutomationEngineTests.cs`: validador, serializador,
  evaluador de condiciones y calculadora de programación.

Acceso a tipos `internal` del motor mediante
`src/backend/TitanMDM.Infrastructure/AssemblyInfo.cs`
(`InternalsVisibleTo`). Ejecutar:

```powershell
dotnet test tests\TitanMDM.UnitTests\TitanMDM.UnitTests.csproj `
  -c Release --filter "FullyQualifiedName~TitanMDM.UnitTests.Automation"
```

## 10. Puesta en marcha

1. Registrar la extensión (sección 2).
2. Generar y aplicar la migración EF (sección 3).
3. Reiniciar la API: el seeder de permisos crea
   `automation.*` y los asigna a `SuperAdmin`.
4. Abrir la consola de Automatización.

## 11. Limitaciones conocidas / pendientes

- Los adaptadores de eventos `Connected = false` requieren que
  los módulos emisores publiquen el evento (Windows, Android,
  Helpdesk, Ponches).
- Las acciones de alto impacto permanecen deshabilitadas (stub)
  hasta AUT-2.
- El registro de la extensión y la migración EF se dejan al
  equipo para evitar tocar archivos compartidos.
