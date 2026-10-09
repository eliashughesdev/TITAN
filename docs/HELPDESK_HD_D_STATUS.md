# Helpdesk HD-D: recuperación de contexto y validación

## Línea base — 2026-10-08

- Rama: `feat/helpdesk-hd-d-routing`; commit: `a2fd70c`.
- Cambios previos: `HelpdeskRoutingController.cs`, `HelpdeskService.RoutingCore.cs`.
- Documentos de raíz previos sin seguimiento: auditoría y plan maestro.
- Infrastructure, API y solución: build correcto, 0 warnings/errores.
- Unit tests: 102 correctos. Frontend: build correcto; advertencia de chunk >500 kB.
- Estas comprobaciones no prueban el funcionamiento del routing ni el tenant.

## Documentación leída íntegramente

`plan_maestro_on_premise_titanmdm.md`, `auditoria_titanmdm.md`,
`docs/PRD.md`, `docs/architecture/ARCHITECTURE.md`,
`docs/security/{SECURITY_ARCHITECTURE,THREAT_MODEL,RBAC_MATRIX,BREAK_GLASS}.md`,
`docs/release/RELEASE_CHECKLIST.md`, `docs/ARTIFACT_POLICY.md`,
`docs/adr/ADR-001-on-premise-first.md`, `ADR-002-modular-monolith.md`,
`ADR-003-ponches-python-service.md`, `ADR-004-local-ai-ollama.md`,
`src/frontend/titanmdm-web/README.md` (14 archivos).

No existe un plan Markdown granular HD-A/B/C/D en el árbol inspeccionado.
Los scripts de aplicación y los comentarios llegan a D8.8: son evidencia de
intención, no evidencia de aceptación. La auditoría describe otro commit.

## Diagnóstico inicial A–G

| Bloque | Estado inicial | Evidencia / diferencia |
|---|---|---|
| HD-A: tickets, numeración, correo, adjuntos | FUNCIONAL PERO INCOMPLETO | Dominio y tests; creación agrupada usa otra numeración. Adjuntos POSPUESTOS para HD-D. |
| HD-B | IMPLEMENTADO SIN VALIDAR | Sin definición de alcance HD-B recuperable; no se certifica por inferencia. |
| HD-C: foundation enterprise | FUNCIONAL PERO INCOMPLETO | Commit `1bc8103`; grupos/cobertura/turnos/SLA/UI presentes; no suite SQL de routing. |
| HD-D: automatización | ROTO | DI del worker pide implementación concreta no registrada; rutas health/retry duplicadas. |
| GENERAL/cobertura | FUNCIONAL PERO INCOMPLETO | GENERAL admite grupos especializados, pero ausencia de filas no genera cobertura global. |
| Fairness/concurrencia | FUNCIONAL PERO INCOMPLETO | Ranking existe; historia atribuida al responsable actual, no al destinatario original. Creación inmediata no usa escritura enterprise. |
| Fallback | ROTO | Permite exceder capacidad y asignar fuera de turno/cobertura, contradiciendo requisitos actuales. |
| Requester intelligence | FUNCIONAL PERO INCOMPLETO | Titan/Entra/dispositivos presentes; no reasocia RequesterUserId; acepta nombre externo para correlación; workers independientes no garantizan orden. |
| OpenRouter | FUNCIONAL PERO INCOMPLETO | Proveedor con retry/token; validación de catálogo/confianza; falta contrato estricto de hints y captura cancelación como error. |
| Notificaciones | FUNCIONAL PERO INCOMPLETO | Seguimiento basado en eventos; no notifica auto_assigned en ese feed. |
| Correo saliente | FUNCIONAL PERO INCOMPLETO | Outbox, retry y worker para respuestas públicas; faltan eventos de ciclo de vida. |
| Performance | PENDIENTE | Routing carga todos los abiertos por ciclo; workers auxiliares pueden monopolizar primeros N. |

E. Deuda: caminos de asignación duplicados; registros DI duplicados; diagnóstico
con reglas parciales distintas; lógica de negocio en API; archivos grandes.

F. Último hito identificable: HD-C en Git. **Ningún cierre enterprise HD-D está
demostrado** por el checkpoint o por compilar.

G. Próximo bloque: reparar ejecución real y unificar invariantes de asignación;
probar contra SQL Server. Después coordinar enriquecimiento, notificaciones y
procesamiento acotado. Se conserva numeración D9–D15 como propuesta, no histórica.

## Entra y adjuntos (sin bloqueo del routing)

- Login: authorization code + PKCE S256; scopes `openid profile email User.Read`.
- Directory: client credentials y `https://graph.microsoft.com/.default`;
  endpoints `/users` y `/groups/{id}/members/microsoft.graph.user`.
  Requiere consentimiento de permisos Graph de lectura adecuados al endpoint.
- Correo usa Graph y requiere permisos del buzón para lectura/envío; outbox
  no acredita que exista consentimiento `Mail.Send` real.
- Tenant/client/secret protegido provienen de EntraIdSettings. No se inspeccionan
  ni publican credenciales. No hay evidencia runtime para atribuir el login fallido
  a una causa concreta: revisar coincidencia del redirect construido con el
  registrado, state/cookie/PKCE, secret/key ring, consentimiento y vínculo local.
- Adjuntos: existen endpoints de carga/descarga, importación desde correo y UI
  de preview/inline; Content-ID, firma, almacenamiento y permisos necesitan E2E.
  Estado: POSPUESTO, sin certificar cierre por el commit HD-A5.

## Decisiones

- La IA clasifica; nunca selecciona técnicos ni concede permisos.
- Capacidad, disponibilidad, turno, categoría específica y cobertura son
  restricciones también en fallback. Sin candidato se conserva cola auditada.
- Sin filas de cobertura significa todos los sitios activos; filas desactivadas
  no deben convertirse accidentalmente en permiso global.
- SQL de pruebas usa exclusivamente una base efímera `TitanMDM_RoutingTests_*`.
  No se aplica ninguna migración ni se ejecutan pruebas sobre la base operativa.
