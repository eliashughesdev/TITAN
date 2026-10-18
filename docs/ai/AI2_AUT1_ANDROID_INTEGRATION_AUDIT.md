# AI-2 + AUT-1 + Android - Auditoria de integracion

Fecha: 2026-10-18  
Rama auditada: `feat/remote-support-rs-h`  
Alcance: inspeccion estatica del codigo real. Las pruebas de ejecucion se registraran en los informes finales.

## Criterios de estado

- **Implementada y verificada**: existe una ruta funcional completa en codigo y cuenta con pruebas o controles comprobables.
- **Implementada parcialmente**: existe una parte sustancial, pero faltan integracion, cobertura o garantias solicitadas.
- **Desconectada de la interfaz**: el backend o motor existe, pero la interfaz activa no lo consume.
- **Bloqueada por configuracion**: el codigo existe, pero necesita proveedor, credencial, Google Cloud, SQL externo o hardware.
- **Pendiente de implementar**: no existe una ruta funcional para el requisito.

## Resumen ejecutivo

| Modulo | Estado actual | Evidencia principal | Brecha prioritaria |
|---|---|---|---|
| Titan Assistant | Implementada parcialmente | `src/backend/TitanMDM.Infrastructure/Assistant/AssistantOrchestrator.cs`, `src/frontend/titanmdm-web/src/assistant/` | Solo ocho tools de lectura; sin tools Android/AUT-1/Ponches ni administracion persistente de proveedores. |
| OpenRouter | Bloqueada por configuracion | `src/backend/TitanMDM.Api/AI/OpenRouterAiService.cs` | Requiere clave segura en runtime; health actual no ejecuta una comprobacion remota dedicada. |
| Ollama | Implementada parcialmente | `src/backend/TitanMDM.Api/AI/Assistant/OllamaAssistantProvider.cs` | Tiene probe a `api/tags`, pero esta deshabilitado por defecto y no hay UI administrativa. |
| Fiorella nativa | Desconectada de sus tools | `src/frontend/titanmdm-web/src/pages/ponches/fiorella/useFiorellaChat.ts` | Usa `/api/titan-assistant/messages`; no llama al agente FastAPI de Ponches. |
| Fiorella FastAPI | Implementada parcialmente | `integrations/ponches/backend/app/api/routes/fiorella.py` | Tools reales existen, pero la UI TitanMDM no las consume y falta aislamiento organizacional verificable. |
| Robot login | Implementada parcialmente | `src/frontend/titanmdm-web/src/pages/LoginPage.tsx`, `pages/login/TitanLoginRobot.tsx` | Estados funcionales presentes; no existe manifest/registro de sprites y Entra no comparte la transicion del login clasico. |
| Android Fleet | Implementada parcialmente | `DeviceLocationService.GetFleetLocationsAsync` | Defecto confirmado: incluye Windows/Unknown por ausencia de filtro `DevicePlatform.Android`. |
| Google Maps | Bloqueada por configuracion | `src/frontend/titanmdm-web/src/config/googleMaps.ts` | Requiere `VITE_GOOGLE_MAPS_API_KEY` publica y restringida por origen/API. |
| Android Remote | Implementada parcialmente | `AndroidRemoteActionService`, `AndroidRemoteCapabilities` | Acciones AMAPI reales; vision/control interactivo no soportados y el agente no implementa lock/reboot/wipe local. |
| AUT-1 enterprise | Desconectada en runtime | `AutomationEnterpriseServiceCollectionExtensions.cs` | `AddTitanAutomationPlatform` no se invoca en `Program.cs`; worker, plataforma y seeder no arrancan. |
| AUT-1 legacy | Implementada parcialmente | `src/backend/TitanMDM.Infrastructure/Automation/AutomationService.cs` | Opera, pero la ruta legacy permite comandos sin revalidar el permiso especifico `devices.commands`. |

## Titan Assistant

### Implementado y verificado

- La UI esta montada desde `AppLayout.tsx` y contiene panel, avatar, historial, aborto de solicitud y estado de proveedor.
- El contrato real vive en `assistant/api/titanAssistantApi.ts`: capacidades, turnos, listado, apertura y borrado de conversaciones.
- `TitanAssistantController` obtiene identidad y alcance desde el servidor. No confia en permisos enviados por React.
- `AssistantPrincipalResolver` carga usuario, roles, permisos, organizacion, localidades y concesion de asistente desde la base de datos.
- `AssistantOrchestrator` limita longitud, cuota, historial y tools por turno; aplica timeout/cancelacion, sanea datos y audita turnos e invocaciones.
- `AssistantToolRegistry` vuelve a comprobar permisos al ejecutar una tool.
- Se registran ocho tools reales de solo lectura:
  - `devices.list`
  - `devices.status`
  - `policies.list`
  - `applications.inventory`
  - `reports.overview`
  - `helpdesk.my_tickets`
  - `helpdesk.sla_status`
  - `helpdesk.ticket`
- Las pruebas existentes cubren orquestacion, seguridad, registry, cuota, timeout, proveedor no disponible, tool desconocida y saneado.

### Implementado parcialmente

- `AssistantToolKind.Action` y `Sensitive` existen en contratos y UI, pero no se registra ninguna tool de esos tipos.
- No hay tools reales para Android especializado, automatizaciones, auditoria, geofencing, enrolamiento o configuracion.
- Las conversaciones y cuotas se guardan solo en memoria; se pierden al reiniciar la API.
- `TitanBehaviorEngine`, `TitanAnchorRegistry`, `titanContextBuilder`, `titanCapabilities` y mensajes contextuales no participan en el flujo activo.
- El acceso visual depende de `/api/helpdesk/operations/assistant/me`, acoplando Titan global a una concesion historica de Helpdesk.

### Proveedores

- `AssistantProviderRegistry` selecciona `OpenRouter`, `Ollama` o `Auto`; `Auto` prefiere OpenRouter y despues Ollama.
- OpenRouter reutiliza `IOpenRouterAiService`; no se duplico transporte ni manejo de clave.
- Ollama realiza un probe real a `api/tags` y usa `api/chat`.
- La clave OpenRouter permanece en backend mediante `AI:OpenRouter:ApiKey`/configuracion de entorno. No se expone al navegador.
- No existe pantalla administrativa para activar proveedor, seleccionar modelo por agente, probar conexion o persistir preferencias.
- No existe endpoint dedicado que exponga todos los estados devueltos por `GetStatusesAsync`.
- No se encontro circuit breaker formal. OpenRouter tiene reintentos limitados; la seleccion Auto puede elegir un proveedor alterno al resolver el turno.

## Fiorella y Ponches

### UI nativa TitanMDM

- `AppLayout.tsx` monta `FiorellaController` en rutas Ponches cuando React observa `ponches.fiorella.use`.
- La UI implementa apertura/cierre, minimizar/restaurar, historial local, estados de carga/error y reduced motion.
- `useFiorellaChat.ts` llama directamente a `titanAssistantApi.sendMessage` con el modulo Ponches como contexto.
- El backend generico no registra ninguna tool de Ponches. Por tanto, la Fiorella visible no puede resolver de forma verificable ponches del dia, relojes offline, entradas sin salida o sincronizaciones.
- El permiso `ponches.fiorella.use` no se revalida en `/api/titan-assistant/messages`; ese endpoint usa la concesion `HelpdeskAssistantAccess`.

### Integracion FastAPI existente

- `integrations/ponches/backend/app/api/routes/fiorella.py` expone el agente real en `POST /api/v1/chat`.
- `fiorella_tool_registry.py` y `fiorella_tools.py` contienen tools reales para resumen/busqueda de ponches, empleados, salud de relojes, analisis, exportacion y operaciones ZKTeco.
- Las consultas usan SQL/BioTime y servicios reales; no son datos simulados.
- El puente .NET `PonchesController`/`PonchesGateway` consume endpoints de dashboard y modulos, pero no publica la ruta Fiorella.
- La integracion Python requiere `OPENROUTER_API_KEY` y una clave de maquina para el puente.
- No se comprobaron credenciales, BioTime ni relojes fisicos durante esta auditoria.

### Riesgos

- Las tools Python consultan el esquema compartido de Ponches; debe verificarse aislamiento por organizacion/localidad antes de conectarlas a usuarios finales.
- La descarga de exportaciones y el flujo de confirmacion requieren una revision de autenticacion y propagacion del contrato.
- Existe un hook antiguo en `integrations/ponches/frontend` que apunta a rutas distintas del contrato actual.

## Robot interactivo del login

### Implementado y verificado en codigo

- `LoginPage.tsx` controla `idle`, `email`, `password`, `authenticating`, `error`, `success` y `launch`.
- `TitanLoginRobot` agrega una secuencia interna de boot y reacciones transitorias.
- El robot solo recibe `emailValue` y `showPassword`; nunca recibe la contrasena.
- El login clasico espera 620 ms para iniciar la salida y 1850 ms para navegar tras una autenticacion confirmada.
- `prefers-reduced-motion` elimina la demora de boot; el CSS contiene reglas de movimiento reducido.
- Las pruebas cubren boot, reduced motion, privacidad, cambio de campo, autenticacion, error, exito y launch a nivel de componente.

### Implementado parcialmente

- Los nombres solicitados `Greeting`, `Attention`, `Failure`, `Exit` y `Reduced motion` no son estados normalizados del contrato; se representan mediante boot/reacciones, `error`, `launch` y CSS.
- No existe manifest de recursos, registro de sprites, preload ni fallback para sprites definitivos.
- El enlace Entra `/api/auth/entra/start` sale directamente del documento y no ejecuta la maquina visual de autenticacion/exito.
- Falta una prueba de integracion de `LoginPage` con login real/mocked que verifique la navegacion y los temporizadores completos.

## Android Fleet y geolocalizacion

### Defecto confirmado

`DeviceLocationService.GetFleetLocationsAsync` construye la consulta desde `_db.Devices` con estas condiciones:

```csharp
d.OrganizationId == organizationId && !d.IsDeleted
```

No aplica `d.Platform == DevicePlatform.Android`. El DTO y el cliente tampoco contienen plataforma. La interfaz se presenta como Android, pero recibe Windows y Unknown desde el backend.

### Implementado y verificado en codigo

- La plataforma se almacena en `Device.Platform` con enum `Unknown`, `Android`, `Windows` y conversion a texto en SQL Server.
- El mapa usa ubicaciones persistidas, no inventa coordenadas, y distingue Fresh, Stale, Offline, NoLocation, PermissionDenied y DisabledByPolicy.
- El agente Android `LocationReportingWorker` respeta politica, permisos y bateria; WorkManager no se presenta como GPS continuo.
- `locationApi.ts` usa `/api/location/fleet`; el mapa tiene refresco manual y polling visible de 60 segundos.
- Google Maps se carga de forma perezosa desde `VITE_GOOGLE_MAPS_API_KEY` y ofrece un estado de configuracion faltante.
- Hay pruebas de servicio para estados, aislamiento organizacional, filtros, permisos y geocercas.

### Implementado parcialmente

- No existe prueba de inventario mixto Android/Windows; las fixtures actuales crean Android exclusivamente.
- Geofencing carga todos los dispositivos mediante `devicesApi.getDevices` sin filtro de plataforma.
- El mapa no recibe un campo `platform`, por lo que no puede ejecutar una defensa adicional verificable en frontend.
- La ruta depende de `geofencing.view`; su entrada de navegacion esta en Android, pero la API no esta acotada por workspace.

## Android Remote

### Implementado y verificado en codigo

- La ruta administrativa Android real usa `AndroidRemoteActionsController` y `AndroidRemoteActionService`.
- El servicio rechaza dispositivos no Android y exige enrolamiento AMAPI con `GoogleDeviceName`.
- Acciones implementadas contra Android Management API: lock, reboot, reset password, start/stop lost mode, wipe y delete.
- `AndroidRemoteCapabilities` calcula compatibilidad por modo de administracion, API level y condiciones Huawei/GMS.
- Inventario, seguridad, cumplimiento y solicitud de ubicacion existen mediante el canal de comandos del agente.
- El backend declara expresamente `RemoteVisionSupported=false` y `RemoteControlSupported=false`; no afirma soporte interactivo inexistente.

### Implementado parcialmente o bloqueado

- El agente Kotlin no maneja localmente `LOCK_DEVICE`, `REBOOT` o `WIPE`; esas operaciones dependen de AMAPI.
- `LostModeCommandHandler` local solo persiste estado; no sustituye Device Owner ni Android Device Policy.
- Visualizacion/control interactivo y desatendido estan pendientes y requieren mecanismo oficial, consentimiento y pruebas fisicas.
- Huawei sin GMS y diferencias OEM requieren validacion fisica; la matriz declarativa no sustituye esa prueba.

## Automatizaciones AUT-1

### Implementacion existente

- Se conserva el motor legacy `AutomationService`.
- El motor enterprise incluye definiciones versionadas, validador, evaluador de condiciones, cola persistente, procesador, scheduler, worker, reintentos, timeout cooperativo, anti-ciclos, auditoria, notificaciones y catalogos.
- La consola React incluye dashboard, listado enterprise, constructor, simulador y notificaciones; el cliente API declara cola, ejecuciones, retry y schedules.
- Existen tablas/columnas enterprise en una migracion ya presente y el bootstrap aplica migraciones.
- Existen pruebas unitarias y de integracion para dominio, validacion, calculo de schedules, permisos y atributos del controlador.

### Desconexion critica

- `AddTitanAutomationPlatform(configuration)` esta definido, pero `Program.cs` no lo invoca.
- En consecuencia no se registran `IAutomationPlatformService`, `IAutomationEventIngest`, `IAutomationQueueProcessor`, `AutomationQueueWorker` ni `AutomationPermissionSeeder`.
- Los endpoints `platform/*` pueden devolver 503; los eventos enterprise no se ingieren y el worker no procesa la cola.
- La ruta React de automatizaciones exige `devices.commands` en lugar de permisos `automation.*`.

### Defectos de seguridad y confiabilidad auditados

- La ejecucion legacy de comandos comprueba `automation.execute`, pero no revalida `devices.commands` ni scope del dispositivo como lo hace el procesador enterprise.
- La idempotencia usa una clave determinista sin ocurrencia temporal y un indice unico que incluye items terminales; eventos legitimos repetidos pueden colisionar.
- `Policies.MaxRetries` no se propaga a `AutomationQueueItem`; se usa el default de tres intentos.
- Claim y limites de concurrencia son read-then-write sin token de concurrencia, inseguros con varias instancias.
- El lease fijo puede vencer antes del timeout maximo y no se renueva.
- `ConcurrencyPolicy.Queue` se valida pero no tiene semantica distinta de Parallel.
- El catalogo de eventos y algunos payloads emitidos no coinciden en nombres de campos.
- El constructor React no ejecuta `validateDefinition`, no edita grupos anidados y no aplica RBAC por control.
- Metodos cliente para detalle, retry, schedules, integraciones y tool contracts no tienen una interfaz completa.

## Matriz detallada de aceptacion inicial

| Funcionalidad | Clasificacion | Observacion verificable |
|---|---|---|
| Titan chat visible | Implementada y verificada | Montado en `AppLayout`; contrato API y pruebas presentes. |
| Identidad/RBAC Titan desde backend | Implementada y verificada | `AssistantPrincipalResolver` y registry revalidan servidor. |
| Titan Helpdesk | Implementada parcialmente | Mis tickets/SLA/ticket existen; faltan agregados obligatorios como abiertos/sin asignar/equipo. |
| Titan Android | Implementada parcialmente | Tools genericas de devices; faltan queries Android especializadas. |
| Titan AUT-1 | Pendiente de implementar | No hay tools de automatizacion. |
| Titan acciones sensibles | Pendiente de implementar | Solo lectura registrada. |
| OpenRouter configurable | Implementada parcialmente | Config backend existente; sin UI ni probe dedicado. |
| Ollama configurable | Implementada parcialmente | Provider/probe existente; sin UI y deshabilitado por defecto. |
| Fiorella visible | Implementada y verificada | Avatar/panel montados con permiso React. |
| Fiorella con datos reales | Desconectada de la interfaz | Tools FastAPI existen, UI Titan llama al asistente generico. |
| Login robot clasico | Implementada y verificada | Estados y pruebas de componente presentes. |
| Login robot Entra | Implementada parcialmente | El enlace no participa en la transicion visual. |
| Manifest de sprites | Pendiente de implementar | No existe registro/preload normalizado. |
| Fleet solo Android | Pendiente de corregir | Backend devuelve cualquier plataforma. |
| Fleet estados sin coordenadas | Implementada y verificada | DTO/servicio/UI contemplan estados sin inventar ubicacion. |
| Google Maps | Bloqueada por configuracion | Necesita clave Vite restringida. |
| Android comandos administrativos | Implementada parcialmente | AMAPI real, condicionado por enrolamiento/capacidad. |
| Android vision/control | Pendiente de implementar | Backend declara soporte falso. |
| AUT-1 runtime enterprise | Desconectada de la interfaz/runtime | Extension DI no activada. |
| AUT-1 simulacion | Implementada parcialmente | Servicio existe; runtime no registrado y UI no valida definicion. |
| AUT-1 RBAC | Implementada parcialmente | Buen control enterprise; bypass legacy y ruta React incorrecta. |

## Cambios concurrentes detectados

Al iniciar la auditoria existian cambios no confirmados en `TitanMdmSeeder.cs`, `PermissionRoute.tsx`, `AppLayout.tsx`, navegacion/workspace, `FleetMapPage.tsx/.css` y nuevos recursos/componentes Fiorella. Se consideran trabajo ajeno/en curso y no deben sobrescribirse. Toda integracion posterior debe aplicar cambios minimos y revisar el diff antes de editar esos archivos.

## Orden de correccion derivado

1. Filtrar Android en `DeviceLocationService` y crear regresion con inventario mixto.
2. Activar AUT-1 por su extension existente y corregir el permiso de entrada React, verificando primero el arranque.
3. Exponer estado/health de proveedores sin secretos y completar configuracion administrativa solo sobre la infraestructura existente.
4. Conectar Fiorella mediante el gateway seguro de Ponches, con permiso servidor y scope; no llamar FastAPI directamente desde el navegador.
5. Ampliar tools Titan para Android y AUT-1 reutilizando servicios de lectura reales.
6. Normalizar manifest/estados de animacion sin afirmar que existen sprites definitivos.
7. Ejecutar suites completas y registrar bloqueos externos.
