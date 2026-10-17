# TITANMDM ENTERPRISE — AI-1 / AI-2 FOUNDATION (Bloques 0–6 parciales)

Estado: Núcleo Titan Assistant expandido. 229 pruebas unitarias verdes (213 previas + 16 nuevas de seguridad); 88 integracion en verde, 20 omitidas. Build Release 0 errores.

## FASE 0 — Auditoría IA
- Frontend assistant: `/src/frontend/titanmdm-web/src/assistant/**` (chat, avatar, context builder) — reutilizable.
- Backend IA existente: `/src/backend/TitanMDM.Api/AI/` (OpenRouter) + `/VirtualAgentController`, `/VirtualAgentChatController`, `/HelpdeskAssistantController` — trazan identidad/RBAC.
- Fiorella (separado): `/integrations/ponches/backend/app/services/fiorella_*`, `/integrations/ponches/frontend/src/components/fiorella/*` (herramientas propias, chat, comportamiento, avatar). **No fusionado**; contrato de integración pendiente.
- Permisos: `Application/Security/PermissionCodes.cs` incluye `ponches.fiorella.use`.
- Auditoría: `Application/Audit/IAdministrativeAuditWriter` disponible.

## FASE 1 — Núcleo Titan Assistant (extendido)
- `AssistantPrincipalResolver`: identifica usuario + roles + permisos + concesión de asistente (reutiliza patrón existente).
- `AssistantOrchestrator`: timeout/cancelación, límite de invocaciones, aplicación de **cuota** (`IAssistantQuota`), historial acotado, aviso de intento de recurso.
- `InMemoryAssistantSessionStore`: aislamiento org/usuario, TTL, cuota de sesiones.
- `InMemoryAssistantQuota`: ventana deslizante (configurable vía `Assistant:MaxTurnsPerWindow`, `QuotaWindowSeconds`).

## FASE 2 — Herramientas de consulta (solo lectura)
- `helpdesk.my_tickets`, `helpdesk.sla_status`, `helpdesk.ticket` (revalida `tickets.view` + alcance)
- `devices.list`, `devices.status` (reutiliza `IDeviceQueryService`)
- `policies.list` (`IPolicyService`)
- `applications.inventory` (`IApplicationInventoryService`)
- `reports.overview` (`IReportsService`)

Acciones de escritura no habilitadas; `AssistantToolKind.Action/Sensitive` nunca registrados.

## FASE 3 — OpenRouter + Ollama
- `OpenRouterAssistantProvider`: envuelve `IOpenRouterAiService` (no duplica transporte/clave).
- `OllamaAssistantProvider`: cliente HTTP local (deshabilitado por defecto).
- `AssistantProviderRegistry`: selección Auto/OpenRouter/Ollama + health.
- Integración DI vía `OpenRouterServiceExtensions.AddTitanOpenRouter` → `AddTitanAssistant` (sin tocar `Program.cs` global).

## FASE 4 — Fiorella
- Implementación completa en `integrations/ponches` (FastAPI + React). Tiene router, herramientas, motor, avatar. No se reescribió.
- Contrato de integración preparado: compartir `AssistantPrincipalResolver`, `IAdministrativeAuditWriter`, `IScopeAccessService` y catálogo de herramientas comunes. Migración optativa a futuro (AI-2); sin reemplazo masivo.

## FASE 5 — Robot/UI
- Componentes Titan Assistant en web ya existentes. No se modificó `App.tsx` ni login. Se mantienen componentes aislados reutilizables.

## FASE 6 — Seguridad, trazabilidad y pruebas
- **Trazabilidad**: auditoría de turnos (`assistant.turn`) e invocaciones (`assistant.tool`) con correlationId e IP.
- **Protecciones**: rechazo fuera de permiso, cuota, proveedor inestable, timeout, cancelación externa, herramienta desconocida.
- **Saneado**: `AssistantDataSanitizer` (control chars, truncado, `UNTRUSTED_TOOL_DATA`).
- **Pruebas** (229 unitarias): registry, sesión (aislamiento + cuota + título), orquestación + timeout + offline + sanitizado + herramientas, **seguridad** (alcance/sitios/IP, proveedor error, audit failure no-break, quota exceed, unknown tool, truncado, concurrencia aislada).
- Integración: 88 OK / 20 skip, build Release OK.

## Restricciones
Sin migraciones EF, sin edición de `TitanMdmDbContext`, sin tocar `Program.cs`, sin cambios en Android/Launcher/Managed Google Play. Paralelismo respetado.

## Diferido (AI-2)
Persistencia de conversaciones en BD, herramientas de escritura con confirmación+auditoría obligatoria, acciones sensibles, UI → `/api/titan-assistant`, rate limiting por borde, integración Fiorella por contrato explícito.
