# AI-2 - Catalogo de herramientas

## Reglas comunes

- Todas las tools activas son de lectura.
- `AssistantToolRegistry` revalida permisos en servidor.
- Los servicios aplican organizacion y alcance de sitios/dispositivos/tickets.
- La salida se sanea, limita y envuelve como `UNTRUSTED_TOOL_DATA`.
- Si falta permiso, dato o tool, Titan no debe inventar una respuesta.
- Las acciones `Action` y `Sensitive` siguen sin registrarse.

## Titan Assistant

| Tool | Permiso | Fuente real | Uso principal |
|---|---|---|---|
| `helpdesk.overview` | `tickets.view` | `HelpdeskTickets` | Abiertos, sin asignar, vencidos y asignados al usuario. |
| `helpdesk.assigned_tickets` | `tickets.view` | `HelpdeskTickets` | Lista los tickets abiertos asignados al usuario autenticado. |
| `helpdesk.my_tickets` | Ninguno adicional; solo identidad propia | `HelpdeskTickets` | Solicitudes creadas por el usuario. |
| `helpdesk.sla_status` | Ninguno adicional; solo identidad propia | `HelpdeskTickets` | SLA de tickets propios. |
| `helpdesk.ticket` | `tickets.view` | `HelpdeskTickets` + scope | Detalle por `ticketId`. |
| `devices.list` | `devices.view` | `IDeviceQueryService` | Inventario, Android/Windows, estado, cumplimiento y total. |
| `devices.status` | `devices.view` | `IDeviceQueryService` + scope | Snapshot por `deviceId`. |
| `policies.list` | `policies.view` | `IPolicyService` | Politicas por plataforma/estado. |
| `policies.device` | `devices.view`, `policies.view` | Assignments + Policies + scope | Politicas asignadas por `deviceId`. |
| `applications.inventory` | `applications.view` | `IApplicationInventoryService` | Inventario de aplicaciones autorizado. |
| `reports.overview` | `reports.view` | `IReportsService` | Resumen operativo general. |
| `automation.overview` | `automation.view` | `IAutomationPlatformService` | Reglas activas, fallos 24h, cola, modulos y ultima ejecucion. |

## Enrutamiento de consultas de aceptacion

| Consulta | Tool/argumentos esperados |
|---|---|
| `¿Cuántos tickets abiertos hay?` | `helpdesk.overview` |
| `¿Cuántos dispositivos Android tenemos?` | `devices.list` con `platform=Android`, `pageSize=1`; usar `totalCount`. |
| `Muéstrame los equipos sin comunicación` | `devices.list` con `status=Offline`. |
| `¿Qué automatizaciones fallaron hoy?` | `automation.overview`; el dashboard devuelve fallos de ultimas 24h y ejecuciones recientes. |
| `¿Cuáles son mis tickets asignados?` | `helpdesk.assigned_tickets`. |
| `¿Qué políticas tiene este dispositivo?` | `policies.device` con `deviceId`. |
| `¿Qué automatizaciones están activas?` | `automation.overview`. |
| `Muéstrame las automatizaciones del módulo Android` | `automation.overview` con `module=android`. |
| `¿Cuál fue la última ejecución de esta regla?` | `automation.overview` con `ruleId`. |

## Fiorella

Fiorella usa el catalogo de `integrations/ponches/backend/app/services/fiorella_tool_registry.py`, no el registry generico de Titan.

| Tool/area | Operaciones delegadas relevantes | Fuente |
|---|---|---|
| Resumen de ponches | `attendance.read` | SQL/BioTime |
| Buscar colaborador | `collaborators.read` | SQL/BioTime |
| Buscar registros | `attendance.read` | SQL/BioTime |
| Salud de relojes | `devices.read`, `zk.read` | Servicios de dispositivos |
| Analisis/reportes | `reports.read`, `attendance.read` | SQL/BioTime |
| Exportacion | `reports.export`, `exports.read` | Generador real de archivos |
| Push/clone/delete ZKTeco | Permisos `zk.*` correspondientes | Relojes fisicos; requiere confirmacion cuando aplique |

El gateway .NET traduce permisos `ponches.*` a operaciones FastAPI. Tener `ponches.fiorella.use` permite abrir el asistente, pero no concede automaticamente acceso a datos de RR. HH. ni a relojes.

## Pendientes

- Tools Titan especializadas para ubicacion Android, errores de enrolamiento y matriz remota.
- Tools Helpdesk para disponibilidad de tecnicos y estado de autoasignacion.
- Acciones con confirmacion/auditoria; no deben registrarse por defecto.
- Pruebas E2E de Fiorella contra un dataset BioTime aislado por organizacion.
