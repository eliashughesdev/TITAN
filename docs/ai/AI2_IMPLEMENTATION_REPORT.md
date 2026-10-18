# AI-2 - Informe de implementacion e integracion

Fecha: 2026-10-18  
Rama: `feat/remote-support-rs-h`  
Commit/push: no realizados.

## Resultado

Esta iteracion consolida componentes existentes; no crea un segundo asistente, motor de automatizacion ni servicio Android.

- Titan Assistant conserva su orquestador, registry, sesiones, cuota, saneado y proveedores existentes.
- Se agrego health real de OpenRouter y una consola administrativa de estado no secreto para OpenRouter/Ollama.
- Titan incorpora consultas reales de resumen Helpdesk, tickets asignados, politicas por dispositivo y AUT-1.
- Fiorella nativa deja de usar el endpoint generico de Titan y consume su agente real de Ponches mediante el gateway autenticado .NET.
- El robot login incorpora manifest normalizado, fallback y preload para sprites futuros sin declarar assets inexistentes.
- Android Fleet filtra Android en backend y aplica una segunda validacion en el cliente.
- AUT-1 enterprise queda registrado en DI; worker, seeder, ingest y plataforma pueden iniciar.
- La ruta React de AUT-1 usa permisos `automation.*`, y los comandos legacy revalidan permiso y alcance.

La auditoria previa a cambios esta en `docs/ai/AI2_AUT1_ANDROID_INTEGRATION_AUDIT.md`.

## Arquitectura final del bloque

### Titan Assistant

```text
React TitanAgentChatPanel
  -> /api/titan-assistant/messages
  -> TitanAssistantController
  -> AssistantPrincipalResolver + IScopeAccessService
  -> AssistantOrchestrator
  -> AssistantToolRegistry
  -> servicios reales / EF Core
  -> OpenRouterAssistantProvider u OllamaAssistantProvider
```

Los permisos, organizacion y localidades se resuelven en servidor. Los resultados de tools se envuelven como datos no confiables y no se ejecutan acciones de escritura.

### Fiorella

```text
React Fiorella
  -> /api/ponches/fiorella/chat
  -> PonchesController
  -> IPonchesGateway (clave maquina solo servidor)
  -> FastAPI /api/v1/chat
  -> fiorella_agent_v3
  -> tools SQL/BioTime/ZKTeco autorizadas
```

El navegador no recibe la clave de integracion ni la clave OpenRouter. .NET exige `workspace.ponches.view` y `ponches.fiorella.use`; FastAPI recibe solo las operaciones traducidas desde permisos TitanMDM.

### AUT-1

`Program.cs` activa la extension ya existente `AddTitanAutomationPlatform(configuration)`. No se creo otro motor. Se registran plataforma, ingest, cola, processor, worker, autorizador y seeder de permisos sobre el modelo existente.

## Archivos principales modificados

### Backend

- `src/backend/TitanMDM.Api/AI/IOpenRouterAiService.cs`
- `src/backend/TitanMDM.Api/AI/OpenRouterAiService.cs`
- `src/backend/TitanMDM.Api/AI/Assistant/OpenRouterAssistantProvider.cs`
- `src/backend/TitanMDM.Api/AI/Assistant/OllamaAssistantProvider.cs`
- `src/backend/TitanMDM.Api/AI/Assistant/AssistantServiceExtensions.cs`
- `src/backend/TitanMDM.Api/Controllers/TitanAssistantController.cs`
- `src/backend/TitanMDM.Api/Controllers/PonchesController.cs`
- `src/backend/TitanMDM.Api/Program.cs`
- `src/backend/TitanMDM.Application/Assistant/AssistantModels.cs`
- `src/backend/TitanMDM.Application/Location/IDeviceLocationService.cs`
- `src/backend/TitanMDM.Infrastructure/Assistant/Tools/HelpdeskAssistantTools.cs`
- `src/backend/TitanMDM.Infrastructure/Assistant/Tools/PolicyApplicationReportTools.cs`
- `src/backend/TitanMDM.Infrastructure/Assistant/Tools/AutomationAssistantTools.cs`
- `src/backend/TitanMDM.Infrastructure/Automation/AutomationService.cs`
- `src/backend/TitanMDM.Infrastructure/Location/DeviceLocationService.cs`
- `integrations/ponches/backend/app/api/routes/fiorella.py`

### Frontend

- `src/frontend/titanmdm-web/src/App.tsx`
- `src/frontend/titanmdm-web/src/assistant/api/titanAssistantApi.ts`
- `src/frontend/titanmdm-web/src/api/locationApi.ts`
- `src/frontend/titanmdm-web/src/pages/settings/AssistantProviderSettingsPage.tsx`
- `src/frontend/titanmdm-web/src/pages/settings/AssistantProviderSettingsPage.css`
- `src/frontend/titanmdm-web/src/pages/settings/SettingsPage.tsx`
- `src/frontend/titanmdm-web/src/pages/ponches/fiorella/fiorellaApi.ts`
- `src/frontend/titanmdm-web/src/pages/ponches/fiorella/useFiorellaChat.ts`
- `src/frontend/titanmdm-web/src/pages/ponches/fiorella/Fiorella.tsx`
- `src/frontend/titanmdm-web/src/pages/ponches/fiorella/FiorellaController.tsx`
- `src/frontend/titanmdm-web/src/pages/ponches/fiorella/Fiorella.css`
- `src/frontend/titanmdm-web/src/pages/login/TitanLoginAnimationManifest.ts`
- `src/frontend/titanmdm-web/src/pages/login/TitanLoginRobot.tsx`

### Pruebas

- `tests/TitanMDM.IntegrationTests/DeviceLocationServiceTests.cs`
- `tests/TitanMDM.IntegrationTests/Automation/AutomationLegacySecurityTests.cs`
- `src/frontend/titanmdm-web/src/api/locationApi.test.ts`
- `src/frontend/titanmdm-web/src/assistant/api/titanAssistantApi.test.ts`
- `src/frontend/titanmdm-web/src/pages/fleet-map/FleetMapPage.test.tsx`
- `src/frontend/titanmdm-web/src/pages/login/TitanLoginRobot.test.tsx`
- `src/frontend/titanmdm-web/src/pages/ponches/fiorella/fiorellaApi.test.ts`

## Funcionalidad conservada

- Orquestador y controles de seguridad Titan existentes.
- Acceso historico de Helpdesk Assistant.
- OpenRouter compartido por clasificacion y asistentes.
- Ollama local opcional.
- UI/animaciones Titan y Fiorella en curso.
- Pantallas, mapa y polling Android existentes.
- Acciones Android Management API y matriz de capacidades.
- Motor legacy y modelo enterprise AUT-1.
- Autenticacion propia de FastAPI Ponches para su aplicacion independiente.

## Funcionalidad completada

- `GET /api/titan-assistant/providers` con `settings.view`, sin secretos.
- Probe OpenRouter real contra `models`, timeout de 10 segundos, latencia y error sanitizado.
- Estado Ollama con fecha y latencia de `api/tags`.
- Pantalla `/settings/ai-providers` y tarjeta en Configuracion.
- Tools Titan:
  - `helpdesk.overview`
  - `helpdesk.assigned_tickets`
  - `policies.device`
  - `automation.overview`
- Endpoint autenticado `POST /api/ponches/fiorella/chat`.
- Contrato frontend real Fiorella, cancelacion y estado de conexion.
- Manifest de animacion del login con estados solicitados y fallback CSS.
- Filtro Android en SQL/EF, plataforma en DTO y defensa en `locationApi`.
- Prueba mixta Android/Windows.
- Registro runtime de AUT-1 y permiso de ruta React corregido.
- Revalidacion DB-backed de `devices.commands` y scope en comandos legacy.

## Verificacion ejecutada

| Comando | Resultado |
|---|---|
| `dotnet restore TitanMDM.slnx` | PASS; una advertencia NU1510 preexistente del Windows Agent. |
| `dotnet build TitanMDM.slnx -c Release --no-restore` | PASS; 0 errores. |
| `dotnet test tests/TitanMDM.UnitTests/TitanMDM.UnitTests.csproj -c Release --no-restore` | PASS; 245/245. |
| `dotnet test tests/TitanMDM.IntegrationTests/TitanMDM.IntegrationTests.csproj -c Release --no-restore` | PASS; 129 superadas, 20 omitidas, 0 fallos. |
| `npm run build` | PASS; TypeScript y Vite. |
| `npm run test -- --run --pool=threads --maxWorkers=1` | PASS; 126/126 en 27 archivos. |
| `python -m compileall .../fiorella.py` | PASS. |
| `GET http://localhost:8020/api/health` | PASS 200; API healthy y database available. |
| `GET http://localhost:3020` | PASS 200. |
| `GET http://localhost:11434/api/tags` | PASS 200; Ollama local responde. |

El repositorio usa `TitanMDM.slnx`; no existe `TitanMDM.sln`.

## Verificacion funcional de interfaz

- Login: estados, reduced motion y transiciones verificados por Vitest; recorrido visual humano no ejecutado.
- Titan: contrato, permisos y tools verificados por pruebas; consultas contra sesion real bloqueadas por falta de una sesion autenticada de prueba.
- Fiorella: UI y contrato gateway verificados; respuesta BioTime/OpenRouter real bloqueada por credenciales y datos externos.
- Android Fleet: filtro mixto verificado en backend y frontend; render Google Maps bloqueado por clave/origen de Google Cloud.
- Android Remote: codigo y capacidades auditados; comandos destructivos no se ejecutaron sin dispositivo fisico enrolado.
- AUT-1: DI, builds y suites pasaron; no se dispararon efectos reales de automatizaciones sobre dispositivos/tickets.

No se dispone de herramienta de navegador automatizado en esta sesion. No se usaron credenciales inventadas ni se afirmo una operacion remota no confirmada.

## Pendientes reales y riesgos

- La pantalla IA es de observabilidad. La edicion persistente de proveedor/modelo/timeout desde UI requiere un modelo seguro de configuracion no secreta y recarga coordinada; no se simulo persistencia.
- OpenRouter requiere credencial valida; el probe no se valido contra una cuenta real en esta sesion.
- Fiorella necesita validar aislamiento organizacional del esquema BioTime antes de produccion multi-tenant.
- El flujo de confirmaciones/descargas de Fiorella requiere cierre de contrato y pruebas E2E.
- AUT-1 mantiene riesgos auditados de idempotencia terminal, claim multi-instancia, lease renewal y schedules; ver `docs/automation/AUT1_FINAL_VALIDATION.md`.
- Las 20 pruebas SQL Server de routing fueron omitidas por su propia condicion de entorno.
- Google Maps requiere clave publica restringida, billing/configuracion de Google Cloud y prueba por origen.
- Vision/control Android interactivo no existe; no debe publicarse como funcional.
- Huawei sin GMS, comportamiento OEM, wipe, reboot, lost mode y consentimiento de MediaProjection requieren dispositivos fisicos.
- Los sprites definitivos de Titan/login/Fiorella siguen pendientes de arte aprobado.

## Matriz de aceptacion

| Modulo | Implementado | Prueba automatizada | Prueba manual | Pendiente |
|---|---|---|---|---|
| Titan IA | Parcial: tools reales, proveedores y UI de estado | PASS | Bloqueada por sesion/credencial | Acciones sensibles, persistencia de chat/config, mas tools Android. |
| Fiorella | Parcial: puente real y UI conectada | PASS contrato/UI | Bloqueada por OpenRouter/BioTime | E2E de datos, confirmaciones, aislamiento multi-tenant. |
| Login Robot | Si, con manifest/fallback | PASS | No ejecutada en navegador | Sprites finales y flujo visual Entra. |
| Android Fleet | Si para filtro Android | PASS backend/frontend | Maps bloqueado por clave | Google Cloud y flota fisica. |
| Android Remote | Parcial AMAPI/diagnostico | Cobertura existente/build PASS | Bloqueada por hardware | Vision/control y validacion OEM. |
| AUT-1 | Runtime conectado y bypass legacy cerrado | PASS | Sin efectos reales | Concurrencia/idempotencia/scheduler y E2E SQL multi-instancia. |
