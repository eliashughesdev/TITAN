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
- `IAutomationAuthorizationService` /
  `AutomationAuthorizationService`
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
  registradas y habilitadas, parámetros requeridos, rechazo de
  parámetros desconocidos, longitud máxima de valores, políticas
  y programación).

## 5. Catálogos

- Eventos: `Events/AutomationEventCatalog.cs`. `Connected = true`
  significa que el evento ya se emite y el motor lo procesa hoy.
  `Connected = false` son contratos preparados para adaptadores
  futuros (Windows, Android, Helpdesk, Ponches).
- Acciones: `Actions/AutomationActionCatalog.cs`. Las acciones de
  alto impacto se registran como `Enabled = false` (stub) y no
  pueden activarse todavía.
- Integraciones: `Integrations/AutomationIntegrationCatalog.cs`
  separa módulos `connected` de contratos `prepared`.
- AUT-2: `Integrations/AutomationToolContractCatalog.cs` publica
  contratos de herramientas con `WriteEnabled = false` hasta que
  exista permiso y aprobación explícitos.

## 6. API (`AutomationController`)

Los endpoints legacy conservan su contrato. Los nuevos viven
bajo `platform/*` y usan RBAC (`RequireAnyPermission`) y auditoría
(`[ServiceFilter(typeof(RbacAuditFilter))]`):

| Método | Ruta | Permiso |
|---|---|---|
| GET | `platform/events` | `automation.view` |
| GET | `platform/actions` | `automation.view` |
| GET | `platform/integrations` | `automation.view` |
| GET | `platform/tool-contracts` | `automation.view` |
| POST | `platform/definitions/validate` | `automation.simulate` |
| POST | `platform/rules` | `automation.create` |
| PUT | `platform/rules/{ruleId}` | `automation.edit` |
| POST | `platform/rules/{ruleId}/simulate` | `automation.simulate` |
| GET | `platform/dashboard` | `automation.view` |
| GET | `platform/queue` | `automation.view` |
| GET | `platform/executions/{executionId}` | `automation.view` |
| POST | `platform/executions/{executionId}/retry` | `automation.execute` |
| GET | `platform/notifications` | `automation.view` |
| POST | `platform/notifications/{id}/read` | `automation.view` |
| GET | `platform/rules/{ruleId}/schedule` | `automation.view` |
| PUT | `platform/rules/{ruleId}/schedule` | `automation.edit` |
| DELETE | `platform/rules/{ruleId}/schedule` | `automation.admin` |

`automation.manage` conserva compatibilidad para crear, editar y
operaciones destructivas que históricamente exigían ese permiso.
No concede ejecución ni simulación: esas capacidades requieren
`automation.execute` y `automation.simulate` respectivamente (o
`automation.admin`). Ningún permiso del motor sustituye permisos
externos de una acción (`devices.commands`, `tickets.assign`,
etc.).

Si el motor no está registrado, los endpoints `platform/*`
responden **503** con un mensaje accionable en lugar de fallar
el arranque del controlador.

## 7. Permisos

Definidos en `Application/Automation/AutomationPermissions.cs`
(archivo propio, sin tocar `PermissionCodes.cs`):

- `automation.view`
- `automation.create`
- `automation.edit`
- `automation.manage`
- `automation.execute`
- `automation.simulate`
- `automation.admin`

La autorización se evalúa en dos niveles:

1. El endpoint comprueba la capacidad granular (con equivalencia
   `manage` para roles existentes).
2. El worker vuelve a resolver los permisos efectivos desde la
   base de datos para el actor de la ejecución, incluso en reglas
   programadas y tareas diferidas.

Las acciones de dispositivo exigen además `devices.commands`;
`EscalateTicket` exige `tickets.assign` y scope sobre el ticket.
Los stubs AUT-2 declaran sus permisos futuros sin habilitar
escritura.

El scope se resuelve mediante `IScopeAccessService`: las reglas
sin sitios requieren scope de organización; una regla regional
sólo puede contener sitios activos de la misma organización y
todos deben pertenecer al alcance del actor. Listados, tablero,
ejecuciones, cola, notificaciones y programaciones aplican el
mismo filtro.

Sembrado: `AutomationPermissionSeeder` (hosted service) inserta
los permisos de forma idempotente y los concede a los roles de
sistema `SuperAdmin` activos, replicando la lógica del seeder
principal **sin modificar** `TitanMdmSeeder.cs`.

## 8. Consola React

La consola vive en `src/frontend/titanmdm-web/src/pages/automation/`.
El cliente de API (`src/api/automationApi.ts`) expone los
endpoints `platform/*` (catálogos, validación, reglas
empresariales, simulación, tablero, cola, notificaciones y
programaciones), además de los contratos de integraciones y
herramientas AUT-2. La página `AutomationPage.tsx` conserva las
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

Pruebas adicionales de AUT-1.8/1.9/1.10:

- permisos granulares, equivalencias y separación de permisos
  externos;
- scope de organización/localidad y rechazo de sitios ajenos;
- parámetros desconocidos o demasiado extensos;
- contratos de integración conectados/preparados;
- contratos AUT-2 con escritura deshabilitada;
- propagación de actor y límite anti-ciclo en `ScheduleTask`;
- atributos RBAC de endpoints empresariales;
- rutas del cliente React, simulación y traducción de errores
  403/503.

Acceso a tipos `internal` del motor mediante
`src/backend/TitanMDM.Infrastructure/AssemblyInfo.cs`
(`InternalsVisibleTo`). Ejecutar:

```powershell
dotnet test tests\TitanMDM.UnitTests\TitanMDM.UnitTests.csproj `
  -c Release --filter "FullyQualifiedName~TitanMDM.UnitTests.Automation"

dotnet test tests\TitanMDM.IntegrationTests\TitanMDM.IntegrationTests.csproj `
  -c Release --filter "FullyQualifiedName~Automation"

cd src\frontend\titanmdm-web
npm run test -- --run src/api/automationApi.test.ts `
  src/pages/automation/automationUi.test.ts
```

Resultado de cierre AUT-1: 29 pruebas unitarias de Automation,
18 pruebas de integración de Automation y 7 pruebas Vitest
específicas, todas superadas. `dotnet build` y `npm run build`
también completaron sin errores. En las suites completas:
237/237 unitarias, 102 superadas y 20 omitidas de 122 pruebas de
integración, y 83/83 pruebas frontend.

## 10. Integraciones reales (AUT-1.9)

Eventos conectados hoy mediante `IAutomationEventDispatcher`:

- `DeviceRegistrationService`: enrolamiento.
- `DeviceAgentService` y `DeviceOfflineMonitor`: online/offline y
  telemetría del dispositivo.
- `DeviceLocationService`: geocercas/localización.
- `LostModeService`: activación de modo perdido.
- `SecurityPostureService`: cumplimiento, riesgo y batería.

Contratos preparados, todavía sin emisor: Windows Update e
inventario, Android Enterprise, Helpdesk y Ponches. Se exponen
como `Connected = false`; el validador impide guardar una regla
con esos eventos hasta conectar el emisor correspondiente.

## 11. Seguridad de ejecución (AUT-1.8)

- Actor efectivo: actor manual o, en background, último editor /
  creador de la regla.
- Revalidación en worker: `automation.execute`, permiso específico
  de acción y scope actual del actor.
- Concurrencia: máximo 4 ejecuciones simultáneas por organización,
  además de la política por regla.
- Auditoría: denegaciones y acciones de impacto medio/alto,
  exitosas o fallidas.
- Tareas diferidas: revalidan permisos/scope al ejecutarse,
  conservan actor, respetan regla deshabilitada, timeout y límite
  de concurrencia, y aumentan `CycleDepth`.
- Rutas legacy: requieren scope de organización para reglas
  legacy y nunca ejecutan una regla empresarial por fallback si
  la plataforma no está registrada.
- Simulación: sólo evalúa condiciones y acciones planificadas; no
  encola ni invoca handlers con efectos.

## 12. Puesta en marcha

1. Registrar la extensión (sección 2).
2. Generar y aplicar la migración EF (sección 3).
3. Reiniciar la API: el seeder de permisos crea
   `automation.*` y los asigna a `SuperAdmin`.
4. Abrir la consola de Automatización.

## 13. Limitaciones conocidas / pendientes

- Los adaptadores de eventos `Connected = false` requieren que
  los módulos emisores publiquen el evento (Windows, Android,
  Helpdesk, Ponches).
- Las acciones de alto impacto permanecen deshabilitadas (stub)
  hasta AUT-2.
- El registro de la extensión y la migración EF se dejan al
  equipo para evitar tocar archivos compartidos.
- Hasta registrar `AddTitanAutomationPlatform` en `Program.cs`,
  los endpoints `platform/*` responden 503 y una ejecución manual
  de una regla empresarial también falla 503 de forma segura.
- Las notificaciones dirigidas respetan `TargetUserId` y
  `TargetRole`; el estado `IsRead` sigue siendo compartido para
  notificaciones de rol/organización porque el modelo actual no
  tiene recibos de lectura por usuario.
