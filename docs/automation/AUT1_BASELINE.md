# AUT-1.1 — Baseline del motor de automatizaciones

**Repositorio:** `C:\TitanMDM`
**Rama:** `feat/remote-support-rs-h`
**Bloque:** AUT-1 — Motor avanzado de automatizaciones empresariales
**Fecha:** 2026-10-16

Este documento es el informe de auditoría del motor existente y la base sobre la que
se construye AUT-1. No se crea un motor paralelo: se extiende el actual.

---

## 1. Alcance auditado

| Componente | Ubicación |
|---|---|
| Servicio de automatización | `src/backend/TitanMDM.Infrastructure/Automation/AutomationService.cs` (772 líneas) |
| Dispatcher de eventos | `src/backend/TitanMDM.Infrastructure/Automation/AutomationEventDispatcher.cs` (51 líneas) |
| Contratos de aplicación | `src/backend/TitanMDM.Application/Automation/{IAutomationService,IAutomationEventDispatcher,AutomationDtos}.cs` |
| Agregado de dominio | `src/backend/TitanMDM.Domain/Automation/{AutomationRule,AutomationExecution,AutomationEnums}.cs` |
| Configuraciones EF | `src/backend/TitanMDM.Infrastructure/Persistence/Configurations/{AutomationRuleConfiguration,AutomationExecutionConfiguration}.cs` |
| Migraciones | `20260921173143_AddAutomationEngine`, `20260921180146_AddGeofenceAutomationState` |
| Registro DI | `src/backend/TitanMDM.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` (L437-443, L457-458) |
| BackgroundService | `src/backend/TitanMDM.Infrastructure/Devices/Agent/DeviceOfflineMonitor.cs` |
| API | `src/backend/TitanMDM.Api/Controllers/AutomationController.cs` (236 líneas) |
| Consola React | `src/frontend/titanmdm-web/src/pages/automation/AutomationPage.tsx` (1608 líneas) + `AutomationPage.css` |
| Cliente API React | `src/frontend/titanmdm-web/src/api/automationApi.ts` (171 líneas) |

---

## 2. Qué ya existe (funcional)

### 2.1 Modelo Trigger → Condición → Acción

- `AutomationRule`: `OrganizationId`, `Name`, `Description`, `TriggerType`, `ConditionsJson`,
  `ActionType`, `ActionPayloadJson`, `IsEnabled`, `CreatedByUserId`, timestamps,
  `LastExecutedAtUtc`, `ExecutionCount`.
- `AutomationExecution`: `AutomationRuleId`, `DeviceId?`, `TriggerType`, `TriggerPayloadJson`,
  `Status`, `ResultJson?`, `ErrorMessage?`, `StartedAtUtc`, `CompletedAtUtc?`.
- Persistencia en `AutomationRules` / `AutomationExecutions`; enums almacenados como string;
  índice único `(OrganizationId, Name)` e índice `(OrganizationId, TriggerType, IsEnabled)`.

### 2.2 Flujo de ejecución actual

1. Un módulo llama `IAutomationEventDispatcher.DispatchAsync(org, deviceId, triggerType, payload, actor)`.
2. `AutomationEventDispatcher` serializa el payload y llama `IAutomationService.ProcessEventAsync`.
3. `ProcessEventAsync` resuelve el `TriggerType` (por nombre del enum, `Enum.TryParse`)
   y busca reglas habilitadas de la organización con ese trigger.
4. `ConditionsMatch` compara el `ConditionsJson` contra el payload: **igualdad plana de
   propiedades raíz, `OrdinalIgnoreCase`**. Sin operadores, sin anidamiento, sin AND/OR.
5. `ExecuteInternalAsync` crea un `AutomationExecution`, ejecuta la acción y guarda.

### 2.3 Eventos ya emitidos (estables, verificados en código)

| Trigger | Emisor | Estado |
|---|---|---|
| `DeviceEnrolled` | `Enrollment/DeviceRegistrationService` | estable |
| `DeviceOnline` | `Devices/Agent/DeviceAgentService:349` | estable |
| `LowBattery` | `Devices/Agent/DeviceAgentService:411` | estable |
| `DeviceOffline` | `Devices/Agent/DeviceOfflineMonitor:164` | estable |
| `GeofenceEnter` / `GeofenceExit` | `Location/DeviceLocationService:250` | estable |
| `LostModeActivated` | `FoundMode/LostModeService:110` | estable |
| `SecurityRisk` | `Security/SecurityPostureService:441,711` | estable |
| `DeviceNonCompliant` | `Security/SecurityPostureService:555,794` | estable |
| `DeviceSiteResolved` | `Devices/Agent/DeviceAgentService:269` | **roto** (ver 3.1) |

### 2.4 Acciones ya implementadas (13)

`SendCommand`, `RequestLocation`, `SecurityScan`, `ComplianceScan`, `AppInventory`,
`EnableLostMode`, `DisableLostMode`, `Notification`, `WindowsInventory`,
`WindowsUpdateScan`, `WindowsUpdateInstall`, `RestartDevice`, `LockDevice`.

Todas salvo `Notification` se traducen a `DeviceCommand` vía `IDeviceCommandService`
(expiración 60 min) y son ejecutadas por los agentes Windows/Android existentes.
La acción `Notification` **es un no-op**: sólo serializa un JSON (AutomationService L552-562).

### 2.5 Infraestructura reutilizable detectada

- **EF entity discovery sin tocar el DbContext**: `TitanMdmDbContext.OnModelCreating` usa
  `ApplyConfigurationsFromAssembly`, por lo que una `IEntityTypeConfiguration<T>` nueva
  registra la entidad automáticamente. Permite añadir entidades sin modificar
  `TitanMdmDbContext.cs`.
- **RBAC**: `RequirePermissionAttribute` (policy `permission:<code>`),
  `PermissionAuthorizationHandler`, `PermissionAuthorizationPolicyProvider`.
- **Auditoría**: `IAdministrativeAuditWriter` + entidad `AdministrativeAuditEvent`
  (tabla `AdministrativeAuditEvents`), y `RbacAuditFilter` (ya registrado en DI en
  `Program.cs` L182).
- **Helpdesk**: `IHelpdeskService` expone `RetryAutomaticAssignmentEnterpriseAsync`,
  `RetryAutomaticAssignmentWithFallbackAsync`, `TryAutomaticHandoverAsync`, `AssignAsync`,
  `TransitionAsync` — base para la acción «Escalar ticket».
- **BackgroundService**: patrón `DeviceOfflineMonitor` (IServiceScopeFactory + loop + delay).
- **Comandos**: `IDeviceCommandService.CreateAsync(org, user, CreateDeviceCommandRequest, ct)`.

---

## 3. Hallazgos, correcciones y carencias

### 3.1 Correcciones necesarias (bugs reales)

1. **`DeviceSiteResolved` se descarta silenciosamente.** `DeviceAgentService:269` publica
   este evento, pero no existe en `AutomationTriggerType`, por lo que `ProcessEventAsync`
   retorna sin procesar. Se añadirá al enum y al catálogo.
2. **Acción `Notification` no hace nada.** Sólo devuelve JSON. Se sustituye por un
   registro persistente (outbox de notificaciones).
3. **`AutomationController` sin RBAC específico ni auditoría.** Solo `[Authorize]`; no usa
   `RequirePermission` ni `RbacAuditFilter`.

### 3.2 Carencias frente a AUT-1

| Requisito AUT-1 | Estado actual |
|---|---|
| Condiciones AND/OR con valores tipados | No existe (igualdad plana) |
| Módulo, alcance, prioridad, políticas | No existen |
| Catálogo de eventos extensible | No existe (enum cerrado) |
| Registro de acciones con resultado real | Parcial (switch interno) |
| Cola persistente, reintentos, idempotencia | No existe (ejecución inline) |
| Estados de ejecución y recuperación tras reinicio | No existe |
| Anti-loop / control de ciclos | No existe |
| Programación de tareas (calendario) | `Schedule` en enum pero sin scheduler |
| Consola: dashboard, simulador, diagnóstico | No existe (solo CRUD + historial) |
| RBAC / auditoría de automatizaciones | No existe |
| Aislamiento org/localidad en acciones | Parcial (org sí, site no) |
| Pruebas de automatización | No existen (0 tests) |

---

## 4. Decisiones de diseño de AUT-1

1. **Extender, no duplicar.** El motor legacy sigue operando para reglas antiguas
   (sin `DefinitionJson`); las reglas nuevas usan una definición versionada.
2. **Definición versionada en JSON** (`AutomationRule.DefinitionJson`, nullable) con
   árbol de condiciones AND/OR, lista ordenada de acciones y políticas. Los campos
   legacy se mantienen sincronizados para retrocompatibilidad.
3. **Cola persistente** (`AutomationQueueItem`) con idempotencia, lease, reintentos con
   backoff, dead-letter y recuperación al reinicio. El evento se encola siempre; además
   se intenta un procesamiento inline best-effort para no depender de que el worker esté
   registrado (integración pendiente de `Program.cs`).
4. **Registro de acciones** (`IAutomationActionHandler`) con validación, RBAC y resultado
   real persistido. Acciones de alto impacto quedan registradas como stub deshabilitado.
5. **Catálogo de eventos** (`AutomationEventCatalog`) con descriptores y adaptadores;
   solo se conectan eventos ya estables; el resto queda preparado y documentado.
6. **Aislamiento de integración global**: entidades nuevas registradas vía configuraciones
   propias (sin tocar `TitanMdmDbContext.cs`); DI en una extensión propia
   (`AddTitanAutomationPlatform`, sin tocar `InfrastructureServiceExtensions.cs` ni
   `Program.cs`); permisos sembrados por código propio; migración EF **no ejecutada**
   y documentada.
7. **Sin `eval`, sin SQL de usuario, sin ejecución dinámica.** Evaluador tipado con
   whitelist de campos.
8. **Seguridad**: permisos `automation.view/manage/execute/simulate`, aislamiento por
   organización y site, anti-loop, rate limits, redacción de secretos.

---

## 5. Integraciones globales pendientes (no aplicadas en AUT-1)

1. `Program.cs`: añadir
   `builder.Services.AddTitanAutomationPlatform(builder.Configuration);`
   (antes del cierre de registro de servicios; no puede ir tras la advertencia de L356).
2. Migración EF (no ejecutar mientras la pestaña Android use SQL Server):
   `dotnet ef migrations add AddAutomationEnterpriseEngine --project src/backend/TitanMDM.Infrastructure --startup-project src/backend/TitanMDM.Api --configuration Release --no-build`
3. Conexión de adaptadores de eventos Helpdesk / Ponches / Assistant (contratos listos).

---

## 6. Referencias de documentación previa

- `auditoria_titanmdm.md` (línea 133): confirma ausencia de dry-run, anti-loop, límites.
- `plan_maestro_on_premise_titanmdm.md` (Fase 9, L284-301): automatizaciones planificadas.
- `docs/architecture/ARCHITECTURE.md`, `docs/security/RBAC_MATRIX.md`.
