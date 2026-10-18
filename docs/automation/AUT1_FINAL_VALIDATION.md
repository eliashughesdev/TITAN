# AUT-1 - Validacion final 1.8/1.9/1.10

Fecha: 2026-10-18

## Estado runtime

`AddTitanAutomationPlatform(configuration)` esta registrado en `Program.cs`. Quedan activos sobre la implementacion existente:

- `IAutomationQueueService`;
- `IAutomationActionRegistry`;
- `IAutomationQueueProcessor`;
- `IAutomationEventIngest`;
- `IAutomationPlatformService`;
- `IAutomationAuthorizationService`;
- `AutomationQueueWorker`;
- `AutomationPermissionSeeder`.

No se creo un segundo motor. Las reglas legacy permanecen en `AutomationService`; las enterprise usan `DefinitionJson` y cola persistente.

## Seguridad

- La ruta React `/automation` exige ahora `automation.view`, `automation.manage` o `automation.admin`, no `devices.commands`.
- Endpoints mantienen permisos independientes para view/create/edit/manage/simulate/execute/admin.
- Worker enterprise revalida permisos y scope desde DB.
- Los comandos legacy ahora exigen `devices.commands` y scope actual del dispositivo mediante `IAutomationAuthorizationService`.
- Si el autorizador no esta registrado, un comando legacy falla cerrado y queda registrado como ejecucion fallida.
- Se agrego regresion `LegacyDeviceCommand_FailsClosedWithoutAuthorizationService`.

## Titan

La tool de lectura `automation.overview` usa `IAutomationPlatformService` y exige `automation.view`. Responde:

- reglas totales/activas;
- ejecuciones y fallos de ultimas 24 horas;
- cola pendiente/dead letter;
- tasa de exito;
- reglas filtradas por modulo o `ruleId`;
- ultima ejecucion y fallos consecutivos.

No crea, edita, reintenta ni ejecuta reglas.

## Pruebas

| Validacion | Resultado |
|---|---|
| Unit tests completos | 245/245 PASS |
| Integration tests completos | 129 PASS, 20 SKIPPED, 0 FAIL |
| Automation unit subset | 30/30 PASS |
| Authorization/legacy integration subset | 11/11 PASS |
| Build Release | PASS |
| React build | PASS |
| Vitest completo | 126/126 PASS |

Las 20 omisiones corresponden a pruebas Helpdesk Routing que requieren el entorno SQL especifico de la suite; no son fallos AUT-1.

## Integraciones reales conservadas

- Enrollment.
- Online/offline y telemetria.
- Geofence/localizacion.
- Lost mode.
- Security posture, compliance y bateria.
- Acciones reales de comandos de dispositivo y escalamiento Helpdesk donde el catalogo las habilita.

Los contratos Windows Update, Android Enterprise especifico, Helpdesk adicional y Ponches marcados como preparados siguen sin emisor real y no deben presentarse como conectados.

## Hallazgos pendientes antes de produccion

1. La clave de idempotencia determinista puede colisionar con eventos legitimos repetidos despues de estados terminales.
2. `MaxRetries` de la definicion no se propaga completamente al numero de intentos de cola.
3. Claim y limites de concurrencia no usan actualizacion atomica/row version para despliegues multi-instancia.
4. El lease puede ser menor que el timeout de regla y no tiene renovacion.
5. `ConcurrencyPolicy.Queue` no tiene semantica propia completa.
6. Algunos campos del catalogo de eventos no coinciden con payloads emitidos.
7. El scheduler interval sin ancla, `CatchUp` y validacion de timezone requieren cierre.
8. El builder React no edita todos los grupos anidados ni invoca validacion antes de guardar.
9. Retry enterprise permite revisar reglas de elegibilidad/idempotencia antes de efectos reales.
10. Falta prueba SQL Server multi-instancia del processor/worker y side effects.

## Dictamen

AUT-1 ya no esta desconectado y su brecha legacy de permisos de comandos quedo cerrada. El motor es apto para continuar QA controlado, no para declarar alta disponibilidad multi-instancia ni ejecucion desatendida de alto impacto hasta resolver los riesgos anteriores.
