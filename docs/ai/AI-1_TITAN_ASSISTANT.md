# TitanMDM Enterprise — AI-1: Titan Assistant (núcleo fundacional)

Estado: **Bloques A–G entregados y verdes** (200 pruebas unitarias).
Alcance: backend únicamente. Sin migraciones EF, sin cambios en Android,
sin tocar `Program.cs`, `TitanMdmDbContext.cs` ni el contenedor global.

---

## 1. Objetivo

Construir el núcleo del asistente empresarial de TitanMDM:

- Catálogo de herramientas seguras con permisos RBAC revalidados en backend.
- Historial de conversaciones aislado por organización y usuario.
- Orquestación de turnos con control de errores, *timeout*, cancelación y
  límite de invocaciones.
- Abstracción de proveedores (OpenRouter / Ollama) sin filtrar credenciales.
- Trazabilidad de invocaciones por turno.

Principio rector: **el modelo nunca concede privilegios ni ejecuta acciones**.
Solo se exponen herramientas de **lectura**; el backend revalida siempre
identidad, permisos y alcance.

---

## 2. Arquitectura (capas)

```
TitanMDM.Application/Assistant/
  AssistantModels.cs        contratos, enums y DTO (sin dependencias de infra)
  IAssistantServices.cs      IAssistantTool / IToolRegistry / ISessionStore /
                             IAssistantProvider / IProviderRegistry / IOrchestrator

TitanMDM.Infrastructure/Assistant/
  AssistantOptions.cs        AssistantOptions + OllamaAssistantOptions
  AssistantDataSanitizer.cs  saneado/truncado + envoltura UNTRUSTED
  AssistantToolRegistry.cs   catálogo filtrado por permisos; sin reflexión
  InMemoryAssistantSessionStore.cs  sesiones aisladas por org/usuario + TTL/cuota
  AssistantOrchestrator.cs   motor de turno (envelope JSON + bucle de tools)
  Tools/*.cs                 herramientas de lectura

TitanMDM.Api/AI/Assistant/
  OpenRouterAssistantProvider.cs  reutiliza IOpenRouterAiService existente
  OllamaAssistantProvider.cs      cliente HTTP local (deshabilitado por defecto)
  AssistantProviderRegistry.cs    selección Auto/OpenRouter/Ollama
  AssistantPrincipalResolver.cs   identidad verificada desde token + BD
  AssistantServiceExtensions.cs   registro DI del núcleo

TitanMDM.Api/Controllers/
  TitanAssistantController.cs     superficie HTTP /api/titan-assistant
```

El registro se invoca desde `OpenRouterServiceExtensions.AddTitanOpenRouter`
(archivo de IA ya llamado por `Program.cs`) para no editar el registro global.
Ver §7.

---

## 3. Matriz de reutilización

| Necesidad                         | Reutilizado                                        | No duplicado |
|-----------------------------------|----------------------------------------------------|--------------|
| Transporte/proveedor LLM          | `IOpenRouterAiService`, `OpenRouterOptions`, `OpenRouterServiceExtensions` | nuevo transporte HTTP |
| Identidad y permisos              | Patrón de `VirtualAgentController` (claims + roles + permisos) | nuevo resolver de permisos |
| Concesión de acceso al asistente  | `HelpdeskAssistantAccess` (grant por usuario)      | nueva tabla/acceso |
| Alcance por sitio                 | `IScopeAccessService`, `AuthorizationScopeSnapshot`| nuevo scope |
| Códigos de permiso                | `PermissionCodes` (Application.Security)           | literales sueltos |
| Tickets                           | `TitanMdmDbContext.HelpdeskTickets` (filtro requester) | consultas divergentes |
| Dispositivos                      | `IDeviceQueryService`                              | SQL ad-hoc |
| Políticas                         | `IPolicyService`                                   | SQL ad-hoc |
| Inventario de apps                | `IApplicationInventoryService`                     | SQL ad-hoc |
| Reportes                          | `IReportsService`                                  | cálculo duplicado |
| Auditoría (escritura)             | `IAdministrativeAuditWriter` (disponible)          | nuevo writer |

No se creó un segundo proveedor OpenRouter ni un segundo cliente LLM: el
adaptador envuelve el servicio existente.

---

## 4. Herramientas (solo lectura)

| Nombre                    | Permiso requerido   | Fuente                         |
|---------------------------|---------------------|--------------------------------|
| `helpdesk.my_tickets`     | — (gate de acceso)  | `HelpdeskTickets` por requester|
| `helpdesk.sla_status`     | — (gate de acceso)  | `HelpdeskTickets` por requester|
| `helpdesk.ticket`         | `tickets.view`      | `CanAccessTicketAsync` + BD    |
| `devices.list`            | `devices.view`      | `IDeviceQueryService`          |
| `devices.status`          | `devices.view`      | `IDeviceQueryService` (+scope) |
| `policies.list`           | `policies.view`     | `IPolicyService`               |
| `applications.inventory`  | `apps.view`         | `IApplicationInventoryService` |
| `reports.overview`        | `reports.view`      | `IReportsService`              |

Reglas de construcción del catálogo (`AssistantToolRegistry`):

- Solo se registran herramientas con `Kind == Read`.
- `Action`/`Sensitive` **nunca** se registran (bloqueo estructural).
- `Describe(principal)` filtra por permisos efectivos.
- `ExecuteAsync` revalida nombre + permisos; herramienta desconocida o no
  autorizada devuelve fallo sin ejecutar. Sin dispatch por reflexión.

---

## 5. Modelo de seguridad

- **Identidad**: `AssistantPrincipalResolver` resuelve org/usuario/roles/permisos
  desde el token y la BD. Nunca desde el texto ni desde el modelo.
- **Acceso**: se exige `HelpdeskAssistantAccess.IsEnabled` por usuario; sin ello
  el endpoint responde `403`.
- **Escritura**: no existe ninguna herramienta de escritura ni ejecución remota.
- **Confirmaciones**: reservadas para una fase posterior (§9); hoy innecesarias
  porque no hay acciones.
- **Inyección de prompt**: todo dato de herramienta se limpia
  (`AssistantDataSanitizer.Sanitize`), se trunca y se envuelve en
  `BEGIN_UNTRUSTED_TOOL_DATA ... END_UNTRUSTED_TOOL_DATA`. El prompt del sistema
  ordena tratar ese bloque como datos, jamás como instrucciones.
- **Secretos**: el prompt prohíbe solicitar/revelar contraseñas, tokens, claves
  API, secretos y MFA. Las credenciales del proveedor nunca salen del backend.
- **Trazabilidad**: cada turno guarda `AssistantToolInvocation` (herramienta,
  duración, éxito, código de error) en el historial de la conversación.

---

## 6. API (`/api/titan-assistant`)

| Método | Ruta                                  | Descripción                          |
|--------|---------------------------------------|--------------------------------------|
| GET    | `/capabilities`                       | Proveedor activo + herramientas      |
| POST   | `/messages`                           | Turno `{ conversationId?, message, module }` |
| GET    | `/conversations`                      | Resúmenes del usuario                |
| GET    | `/conversations/{id}`                 | Conversación propia                  |
| DELETE | `/conversations/{id}`                 | Elimina conversación propia          |

Respuesta de turno: `{ conversationId, answer, provider, model,
toolInvocations[], availableTools[], cancelled }`.

Errores: `401` sin identidad, `403` sin concesión de asistente, `400` mensaje
fuera de rango, `503` implícito cuando no hay proveedor disponible
(respuesta controlada en `answer`).

---

## 7. Configuración (preparada, no aplicada en `appsettings.json`)

Por restricción de trabajo en paralelo, **no** se editó `appsettings.json`.
Para habilitar y afinar el asistente, añadir:

```jsonc
"Assistant": {
  "Enabled": true,
  "Provider": "Auto",              // "Auto" | "OpenRouter" | "Ollama"
  "MinMessageLength": 2,
  "MaxMessageLength": 1500,
  "MaxHistoryMessages": 20,
  "MaxAnswerCharacters": 4000,
  "MaxToolOutputCharacters": 4000,
  "MaxToolCallsPerTurn": 4,
  "SessionTtlMinutes": 240,
  "MaxActiveSessionsPerUser": 20,
  "TitleMaxLength": 80,
  "RequestTimeoutSeconds": 60,
  "Temperature": 0.15,
  "MaxTokens": 700,
  "Ollama": {
    "Enabled": false,
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1",
    "TimeoutSeconds": 90,
    "MaxTokens": 700
  }
}
```

Sin esta sección se usan los valores por defecto (OpenRouter vía
`AI:OpenRouter`, Ollama deshabilitado).

Integración DI: `AddTitanAssistant(configuration)` se invoca desde
`OpenRouterServiceExtensions.AddTitanOpenRouter`. Si se prefiere registrar el
núcleo explícitamente, sustituir por una llamada directa en `Program.cs` es
seguro e idempotente.

---

## 8. Pruebas

`tests/TitanMDM.UnitTests/Assistant/` — 15 pruebas nuevas:

- `AssistantToolRegistryTests`: filtrado por permisos, herramienta desconocida,
  herramienta no autorizada no se ejecuta, `Sensitive` nunca se registra.
- `InMemoryAssistantSessionStoreTests`: aislamiento org/usuario, rechazo de
  conversación ajena, cuota por usuario, título desde el primer mensaje.
- `AssistantOrchestratorTests`: uso de herramienta de lectura, denegación fuera
  de permiso, envoltura de datos no confiables, *timeout* → `cancelled`,
  proveedor no disponible, validación de longitud, capacidades.

Resultado: **200/200** unitarias en verde (185 previas + 15 nuevas).
Backend compila en `Release` sin advertencias.

---

## 9. Diferido / siguientes bloques

- Persistencia de conversaciones en BD (requiere migración EF; **no** creada
  para no colisionar con migraciones Android en curso).
- Herramientas de escritura (`Action`) con confirmación del usuario + auditoría
  obligatoria vía `IAdministrativeAuditWriter`.
- Acciones sensibles (bloqueo, borrado, políticas, ejecución remota):
  permanentemente deshabilitadas hasta tener confirmaciones fuertes.
- Redacción automática de secretos en la entrada del usuario.
- UI: el panel existente (`src/frontend/titanmdm-web/src/assistant/**`) apunta a
  `/api/virtual-agent/*` (helpdesk). Migración de UI a `/api/titan-assistant`
  en una tarea de frontend separada.
- Rate limiting por usuario/organización en el borde.

## 10. Restricciones respetadas

- Sin migraciones EF nuevas; sin edición de `TitanMdmDbContext` ni `Program.cs`.
- Sin cambios en Android, Managed Google Play, Titan Launcher ni despliegues.
- Sin commit/push/merge; sin tocar `main`.
- Solución: `TitanMDM.slnx` (el enunciado citaba `TitanMDM.sln`, inexistente).
