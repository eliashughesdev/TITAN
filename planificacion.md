# 📅 Plan Maestro de Ejecución y Saneamiento - TitanMDM

Este documento es la hoja de ruta técnica definitiva. No es un resumen, sino un **listado de tareas granulares** diseñado para que cualquier agente pueda ejecutar los cambios sin ambigüedades. El orden de ejecución es estrictamente por **Criticidad (P0 $\rightarrow$ P3)**.

---

## 🔴 PRIORIDAD P0: BLOQUEOS CRÍTICOS Y SEGURIDAD
*Tareas que impiden el funcionamiento básico o ponen en riesgo la integridad del sistema.*

### 1. Módulo ITAM (Inventario Cedi) - Construcción del Núcleo
**Estado Actual:** UI existente pero sin funcionalidad (maqueta).
- [x] ✅ **[BACKEND] Esquema de Base de Datos:** Crear tablas en SQL Server: `Assets`, `Employees`, `Assignments`, `Returns`, `Receptions`, `InventorySessions`, `InventoryCounts`.
- [x] ✅ **[BACKEND] Controladores API:** Implementar `AssetsController.cs`, `AssignmentsController.cs`, `ReceptionController.cs`, `EmployeeController.cs` e `InventoryController.cs`.
- [x] ✅ **[BACKEND] Lógica de Negocio:**
    - [x] ✅ Implementar motor de depreciación (Valor libros vs Acumulada).
    - [x] ✅ Implementar generador de QR (`CISA-TI|Tag|Id`) con descarga de PNG.
    - [x] ✅ Implementar algoritmo de conciliación física (Sobrantes vs Faltantes).
- [x] ✅ **[FRONTEND] Conexión API:** Vincular `ExecutiveDashboard.tsx` y todas las pantallas de `src/pages/inventory/` a los nuevos endpoints.
- [x] ✅ **[FRONTEND] Formularios Operativos:** Hacer funcionales los formularios de entrega, recepción y devolución (incluyendo persistencia de firmas Base64).

### 2. Seguridad y Estabilidad Global
- [x] ✅ **[BACKEND] Saneamiento de Logs:** `InventoryController` usa `GetSafeRouteForLog` para registrar solo la ruta (sin query strings ni objetos completos), evitando fuga de datos sensibles en logs.
- [x] ✅ **[BACKEND] Unificación SGBD:** No hay referencias a MongoDB en el backend; el módulo ITAM usa exclusivamente SQL Server.

---

## 🟠 PRIORIDAD P1: DEUDA TÉCNICA Y FUNCIONALIDADES CORE
*Tareas necesarias para alcanzar el estado "Enterprise" y estabilidad operativa.*

### 1. Windows MDM
- [x] ✅ **[BACKEND] Motor de Remediación Activa:** Implementar la lógica para que el servidor dispare comandos correctivos automáticamente al detectar fallos de seguridad.
    - `WindowsRemediationEngine` (invocado por `SecurityPostureService` en `SECURITY_STATUS`/`COMPLIANCE_CHECK`): Defender caído → `SERVICE_START WinDefend`, Firewall caído → `SERVICE_START mpssvc`, más `COMPLIANCE_CHECK` de re-verificación. UAC/SecureBoot/TPM/BitLocker/reinicio se informan como acciones manuales (nunca se reinicia ni cifra sin aprobación).
    - Anti-bucle por ventana de enfriamiento, actor = SuperAdmin activo (o `Remediation:Windows:ActorUserEmail`), resultado incluido en los eventos de automatización `SecurityRisk`/`DeviceNonCompliant`. Config: `Remediation:Windows`. Tests: `WindowsRemediationEngineTests` (6/6).

### 2. Android MDM
- [x] ✅ **[BACKEND/AGENT] Trigger Proactivo de Geofencing:** Cambiar el reporte de ubicación de reactivo a proactivo basado en eventos de transición de zona.
    - Backend: `GET api/device/commands/location/geofences` (auth de dispositivo) + source `AndroidAgent-GeofenceTransition` que se procesa aunque el muestreo periódico esté deshabilitado y respeta la transición reportada por el SO.
    - Agente: `GeofenceRegistrar` (GeofencingClient), `GeofenceBroadcastReceiver`, `GeofenceTransitionWorker` (envío inmediato con reintentos) y `GeofenceSyncWorker` (re-registro en arranque y en cada ciclo).
- [x] ✅ **[FRONTEND/CONFIG] Limpieza de Google Maps:** Eliminar todas las referencias, claves y dependencias de Google Maps en el frontend y config, asegurando el uso exclusivo de Leaflet/OSM.

### 3. Helpdesk (Mesa de Ayuda)
- [x] ✅ **[BACKEND] Validación de Cierre de Tickets:** Implementar validación en el servicio de cierre para asegurar que no existan tareas pendientes asociadas.
    - `HelpdeskService.ClosureValidation.cs`: Resolver/Cerrar se rechaza (HTTP 409) si hay sesión de soporte remoto activa o comandos MDM del dispositivo del ticket aún en curso. Tests: `HelpdeskClosureValidationTests` (4/4).
- [x] ✅ **[FRONTEND/BACKEND] Reportes Exportables:** Implementar la funcionalidad de exportación de reportes de Helpdesk a PDF y Excel.
    - `HelpdeskReportExportActions.tsx` (ExcelJS multi-hoja con gráficos + jsPDF/autotable) ya estaba implementado; verificado con `HelpdeskReportExportActions.test.tsx` (3/3: genera Excel, genera PDF, bloquea rango inválido).

### 4. Identidad y RBAC
- [x] ✅ **[FRONTEND] Sincronización de UI con Permisos:** Implementar la lógica para ocultar/mostrar botones y menús en tiempo real basados en los claims del JWT (evitar errores 403 en el backend).
    - `auth/jwtClaims.ts` decodifica los claims `permission`/rol del JWT. `AuthContext` re-sincroniza `user.permissions` (que alimenta `PermissionGuard`, `PermissionRoute` y la navegación) al renovar el token, ante cualquier 403 (`apiClient` emite `titanmdm:permissions-stale`), al recuperar el foco y cada 5 min, con throttle de 15 s. Tests: `jwtClaims.test.ts`.
    - Corregido `InventoryDataTable.tsx` (`useState` usado como efecto rompía `tsc -b` y la persistencia del selector de columnas).

---

## 🟡 PRIORIDAD P2: MEJORAS DE UX/UI Y EFICIENCIA
*Mejoras que elevan la calidad del producto y la experiencia del usuario.*

### 1. Módulo ITAM - Experiencia de Usuario
- [x] ✅ **[FRONTEND] Normalización de Estilos:** Aplicar la paleta **Azul Cielo** y el diseño moderno del Dashboard a todas las pantallas (`AssetsPage`, `DeliveriesPage`, `ReturnsPage`, `ReceptionPage`, `EmployeeManagementPage`, `ReportsPage`).
- [x] ✅ **[FRONTEND] Implementación de IframeModal:** Sustituir la navegación de formularios por un sistema de Modales con Iframe para mantener el contexto de la página principal.
- [x] ✅ **[FRONTEND] Pantalla de Reportes:** Rediseñar la pantalla de reportes para que sea consistente con la UI del sistema y habilitar la exportación real a PDF/Excel.
    - `inventoryReportExport.ts`: Excel multi-hoja (Resumen, Depreciación por activo, Activos y distribuciones) y PDF A4 horizontal, paleta Azul Cielo; datos 100% de `/api/itam` (nuevo `GET /api/itam/assets/depreciation`). Tests: `inventoryReportExport.test.ts` (3/3).
    - Motor de depreciación: ahora usa la vida útil de la categoría cuando el activo no define la suya, y el Dashboard aplica la misma regla (antes esos activos quedaban sin depreciar y los totales divergían).
- [x] ✅ **[FRONTEND] Estilización de IframeModal:** las reglas CSS apuntaban a `.inv-form-page > header` pero el header vive en `.inv-form-shell` (se veía plano). Corregido; dentro del modal se oculta el encabezado duplicado y el fondo se integra (`inv-form-page--embedded`).
- [x] ✅ **[FRONTEND] Sin datos ficticios:** retirados del código `inventoryData.json` (volcado estático de 327 KB con hostnames/IP/MAC reales) y `inventoryStore.ts` (persistencia en localStorage); ambos sin uso, respaldados en `.titan-local/backups/itam-dead-code-20261104/`.
- [x] ✅ **[BACKEND] Invalidación de caché de inventario:** `InventoryController` disparaba la invalidación sin `await` con el token de la petición (se cancelaba al responder → datos obsoletos tras guardar). Ahora se espera y sus errores no afectan la escritura.
- [x] ✅ **[FRONTEND] Dashboard Ejecutivo:** Añadir KPIs de Valor Total, Activos en Riesgo y Gráficos de Distribución por Departamento.

### 2. Optimización de AI Agents
- [ ] **[BACKEND] Capa de Caché:** Implementar Redis o caché en memoria para respuestas comunes del asistente virtual para reducir latencia.

### 3. Estandarización de API
- [ ] **[BACKEND] ApiResponse Unificado:** Normalizar todas las respuestas de los controladores bajo una estructura única `ApiResponse<T>` para evitar fallos de parseo en el frontend.

---

## 🟢 PRIORIDAD P3: LIMPIEZA Y DETALLES FINALES
*Tareas de mantenimiento y pulido estético.*

- [x] ✅ **[FRONTEND] Limpieza de Código Muerto:** Revisada `inventoryNav.ts`; no hay rutas ni etiquetas obsoletas, todos los iconos y secciones están en uso.
- [x] ✅ **[UI/UX] Densidad de Datos:** Selector de columnas implementado en `InventoryDataTable.tsx` (mostrar/ocultar columnas con persistencia en localStorage).
- [x] ✅ **[BACKEND] Auditoría Detallada:** `AdministrativeAuditEvent`, `AdministrativeAuditEntry`, writer y DTOs ahora incluyen `BeforeJson` y `AfterJson` para capturar estado antes/después de cada cambio.

---

## 🏗️ OPERACIÓN: ARRANQUE DE DESARROLLO Y DESPLIEGUE IIS

- [x] ✅ **[DEVOPS] Script de arranque `scripts/dev/Start-TitanMDM-Stack.ps1`:** El arranque se abortaba por procesos huérfanos (Vite en :3020, `TitanMDM.Api.exe` hijo de `dotnet run`, intérprete Python hijo del venv) que el cierre no terminaba. Ahora: Job Object `KILL_ON_JOB_CLOSE` (cerrar la consola mata todo), cierre por árbol, recuperación automática de puertos ocupados solo por procesos de este repositorio, health check `/api/health/live`, Python no bloqueante, `-SkipPython`/`-SkipBuild`/`-NoReclaim`. Verificado: arranque completo + cierre forzado sin puertos huérfanos.
- [x] ✅ **[DEVOPS] Frontend en IIS:** `public/web.config` (SPA fallback, proxy ARR de `/api` y `/hubs` con WebSockets, `X-Forwarded-Proto`, caché inmutable de `/assets`, `index.html` sin caché, cabeceras de seguridad, 50 MB de upload).
- [x] ✅ **[DEVOPS] Backend como Servicio de Windows:** `UseWindowsService` nativo (content root = carpeta publicada), registro con `sc.exe` (inicio retrasado, reinicio ante fallo, cuenta virtual), secretos como variables de entorno del servicio con ACL restringida.
- [x] ✅ **[DEVOPS] Scripts de producción:** `deploy/iis/Publish-TitanMDM.ps1` (verificado: genera paquete web+api) e `Install-TitanMDM.ps1` (IIS + ARR + servicio + NSSM Ponches + health check), plantillas `appsettings.Production.json` / `titanmdm.secrets.example.json` y `deploy/iis/README.md`. Pendiente: ejecución de `Install-TitanMDM.ps1` en el Windows Server destino.

---

## 🌐 NÚCLEO UNIFICADO: NOTIFICACIONES Y ASISTENTES

- [x] ✅ **[FULL-STACK] Centro de notificaciones real:** el Header era un placeholder estático (siempre "0" y punto rojo fijo). Ahora `GET /api/notifications` (`NotificationFeedService`) agrega alertas en vivo desde SQL Server, filtradas por RBAC y alcance de sede: seguridad/cumplimiento, geocercas (24 h), comandos fallidos, SLA vencido y tickets sin asignar de Helpdesk, garantías/recepciones/riesgo de ITAM. Una fuente caída se informa sin romper el feed. Tests: `NotificationFeedServiceTests` (4/4).
- [x] ✅ **[FRONTEND] Toasts y errores unificados:** `NotificationProvider` global (polling 30 s + al volver a la pestaña, estado leído por usuario), toasts para alertas críticas nuevas y para fallos 5xx/red de escrituras (`titanmdm:api-error` emitido por `apiClient`).
- [x] ✅ **[FULL-STACK] Asistentes virtuales:** rutas verificadas extremo a extremo: Asistente Titan (`/api/titan-assistant/*`), asistente Helpdesk (`/api/my/helpdesk/assistant/suggest`) y Fiorella (`/api/ponches/fiorella/chat` → Python `routes/fiorella.py`, permiso `ponches.fiorella.use`). Pendiente: prueba conversacional autenticada en el entorno del usuario.

## 🖥️ ESCRITORIO REMOTO WINDOWS (NIVEL COMERCIAL)

- [x] ✅ **[AGENT] Rendimiento del canal:** el RemoteHost enviaba ~30 frames/s aunque la pantalla no cambiara. Ahora omite frames idénticos (hash) con keyframe cada 1,5 s y envío forzado al cambiar de monitor, de perfil, de escritorio o al reconectar.
- [x] ✅ **[FULL-STACK] Perfil de calidad elegido por el técnico:** Automático / Velocidad / Equilibrado / Calidad + resolución máxima + FPS máximos (`SetStreamProfile`); la adaptación automática opera dentro de esos límites. Tests: `RemoteVideoPerformanceProfileTests`.
- [x] ✅ **[FULL-STACK] Ctrl+Alt+Supr real:** `SendSecureAttention` → comando `REMOTE_SEND_SAS` → `WindowsSecureAttentionService` (sas.dll!SendSAS como SYSTEM, habilita `SoftwareSASGeneration` para servicios). Atajos adicionales: Win, Alt+Tab, Ctrl+Shift+Esc, Win+R, Win+D, Win+E.
- [x] ✅ **[FULL-STACK] Transferencia de archivos:** técnico → equipo por bloques de 256 KB con acuse del host (contrapresión), SHA-256, límite 200 MB, cancelación; se guarda en `Descargas\TitanMDM` del usuario sin sobrescribir y el indicador de sesión lo informa. Requiere `AllowFileTransfer` + control activo. Tests: `RemoteFileTransferReceiverTests`.
- [x] ✅ **[FRONTEND] Barra de soporte remoto:** `RemoteEnterpriseToolbar` (visible también en pantalla completa) con calidad, resolución, FPS, Ctrl+Alt+Supr, atajos, envío de archivo con progreso y RTT real en ms (`Ping` al hub; la cifra anterior dependía del reloj del equipo remoto). Tests: `RemoteEnterpriseToolbar.test.tsx` (7/7).
- [ ] **[AGENT] Pendientes para paridad total con AnyDesk:** códec de video por diferencias (H.264/WebRTC) en lugar de JPEG por frame, transferencia equipo → técnico, portapapeles bidireccional y captura del escritorio seguro (Winlogon). Requiere validación en equipos de laboratorio.

## 📦 ITAM: REPORTES OPERATIVOS Y TABLAS

- [x] ✅ **[FRONTEND] Entregas del día y devoluciones pendientes:** filtros rápidos (Todas / Entregas de hoy / Devoluciones pendientes / Vencidas) con conteos, columna de días vencidos, sección "Devoluciones pendientes" con atraso y exportación Excel/PDF del filtro activo. Tests: `inventoryOperational.test.ts` (4/4).
- [x] ✅ **[FRONTEND] Tablas:** paginación (25/50/100), encabezado fijo, scroll adaptativo y vista de tarjetas en móvil. Corregidos dos bugs del selector de columnas (una columna oculta desaparecía del panel y no podía volver a mostrarse; el estado del panel se guardaba como columna falsa). Tests: `InventoryDataTable.test.tsx` (4/4).
- [x] ✅ **[FRONTEND] IframeModal:** cabecera Azul Cielo con icono, animación de apertura y cierre, indicador de carga del formulario, cierre al hacer clic fuera, devolución del foco y respeto de `prefers-reduced-motion`.

---

## 🗄️ ITAM: DATOS, FECHAS Y PERMISOS (2026-11-05)

- [x] ✅ **[DATOS] Migración MongoDB → SQL Server al 100%:** la app anterior (`C:\inventario Cedi`) seguía escribiendo en MongoDB (registros del 3/11 19:16 al 4/11 12:33 solo existían ahí; por eso "Entregas de hoy" salía vacío). `db/sync_inventory_from_mongo.py` (incremental, sin borrados, simulación por defecto, respaldo de filas) igualó las tablas: assets 42, employees 54, deliveries 103, returns 84, asset_history 238, audit_logs 522; recuperó columnas perdidas (hardware de activos, employee_id/mac_address/serial_number…) y el detalle before/after del historial. Respaldo previo `cisa_ti_itam-pre-sync-20261104-131843.bak`. Wrapper: `scripts/dev/Sync-ItamFromMongo.ps1`.
- [x] ✅ **[DATOS] Script destructivo bloqueado:** `migrate_inventory_from_mongo.py` borraba todas las tablas antes de reinsertar; ahora exige `TITAN_ALLOW_DESTRUCTIVE_MIGRATION=1`.
- [x] ✅ **[BACKEND/FRONTEND] Fechas por zona del negocio:** "hoy" se evalúa en `America/Santo_Domingo` (configurable `Inventory:TimeZoneId` / `VITE_BUSINESS_TIMEZONE`), no en UTC ni en la zona del navegador. `GET /api/itam/deliveries?period=today|pending|overdue` y `returns?period=today`; orden cronológico real (las fechas son texto). Tests: `ItamDateRulesTests` (12) y `inventoryOperational.test.ts` (5, también con TZ=UTC).
- [x] ✅ **[FRONTEND] Búsqueda instantánea:** índice precalculado por fila, `useDeferredValue`, búsqueda multi-término sin tildes.
- [x] ✅ **[RBAC] Inventario en Roles y Permisos:** 10 permisos granulares sembrados (dashboard, activos ver/gestionar, entregas, devoluciones, recepciones, colaboradores, sesiones, conteos, historial) bajo "Inventario (ITAM)". La ruta y la tarjeta del módulo aceptaban `dashboard.view`/`workspace.*.view` (cualquier usuario entraba): ahora solo permisos de inventario. Menú superior y botones de acción filtrados según `ItamController`. Tests: `inventoryAccess.test.ts` (5).
- [x] ✅ **[UI] Microinteracciones ITAM:** menú fijo con desenfoque, entrada escalonada por sección, elevación al pasar el cursor, foco visible, `prefers-reduced-motion`.
- [ ] **[OPERACIÓN] Corte definitivo de la app anterior:** mientras `C:\inventario Cedi` siga en uso hay que ejecutar `Sync-ItamFromMongo.ps1 -Apply` (manual o tarea programada).

---

## 📱 ITAM CATÁLOGOS, ANDROID INTERNO Y COMPILACIÓN LIMPIA (2026-11-05)

- [x] ✅ **[ITAM] Catálogos con alta/edición/baja:** Sucursales, Departamentos, Ubicaciones, Fabricantes, Modelos, Proveedores y Categorías tienen botón "+ Nuevo", Editar y Eliminar (modal celeste). API `POST/PUT/DELETE /api/itam/catalogs/{tipo}` sobre SQL Server `cisa_ti_itam`: nombre obligatorio, duplicados rechazados (modelo por fabricante, ubicación por sucursal), referencias validadas, baja lógica bloqueada si está en uso. Tests: `InventoryCatalogServiceTests` (12) y `CatalogItemForm.test.tsx` (2).
- [x] ✅ **[RBAC] Permisos `inventory.catalogs.view` / `inventory.catalogs.manage`** sembrados (seeder + `db/seed_inventory_catalog_permissions.sql` aplicado en caliente, idempotente). Lectura de catálogos abierta a quien ve activos/colaboradores/recepciones/sesiones.
- [x] ✅ **[BUILD] .NET 0 errores / 0 advertencias** (solución completa): paquetes vulnerables actualizados (SignalR MessagePack/Client 10.0.14 → MessagePack 2.5.303, System.Security.Cryptography.Pkcs 10.0.14), `using` duplicado, nulo en `InventoryGateway`, paquete redundante `System.IO.Pipes.AccessControl`.
- [x] ✅ **[BUILD] `npm run build` sin advertencias:** páginas con `React.lazy` (bundle inicial 2,2 MB → 358 KB), jsPDF dinámico en Helpdesk/Ponches, import estático de `inventoryApi`. ExcelJS (930 KB, solo al exportar) documentado en `chunkSizeWarningLimit`.
- [x] ✅ **[ANDROID] Inscripción interna por QR (sin Android Enterprise/DUNS):** la consola genera QR `titanmdm://enroll?server=…&token=…`; el agente lo lee con cámara (ZXing, sin Google Play) o con el lector Honeywell (Intent API o modo teclado), guarda el servidor en tiempo de ejecución y se registra en SQL Server con sus metadatos de hardware. HTTP solo hacia hosts LAN privados (`ServerEndpointPolicy`). Tests: `EnrollmentPayloadParserTest` (6), `agentEnrollmentQr.test.ts` (3).
- [x] ✅ **[ANDROID] Lector Honeywell → MDM:** `DEVICE_INFO` reporta lector integrado, versión del Data Collection Service, conteo y última lectura (sin contenido leído).
- [x] ✅ **[AMAPI] Error `GOOGLE_AMAPI_POLICY_PUBLISH_FAILED` corregido:** `printingPolicy` = `PRINTING_DISALLOWED` y `microphoneAccess` = `MICROPHONE_ACCESS_DISABLED` (enums v1). Tests `AndroidPolicyCompilerRestrictionsTests`.
- [x] ✅ **[ANDROID] Error 500 del Centro de Control:** faltaban `BeforeJson/AfterJson` en `AdministrativeAuditEvents` (migración `AddAuditEventBeforeAfterJson`, aplicada). Se retiraron del `TitanMdmDbContext` los DbSet sin uso de catálogo que generaban tablas ITAM en la BD TitanMDM.
- [x] ✅ **[ANDROID] Flota unificada AMAPI + TitanMDM Agent:** Centro de Control, resumen y Despliegues del Titan Launcher incluyen equipos inscritos por el agente Kotlin (LEFT JOIN, modo `TITAN_AGENT`). Asignar a un equipo del agente encola `APPLY_LAUNCHER_POLICY` en SQL; el agente guarda la configuración, el launcher la aplica y el acuse marca la asignación `Applied`. Tests `AndroidAgentFleetTests` (5) y `TitanServerLauncherPolicyTest` (4).
- [x] ✅ **[ANDROID] Kiosco on-premise (Device Owner, sin Google):** `TitanDeviceAdminReceiver` + `KioskController`; bloqueos configurables en la política web del launcher (HOME fijo, barra de estado, teclas del sistema/lock task) vía claves `titan_persistent_home`, `titan_lock_status_bar`, `titan_lock_system_keys`. Comandos `LOCK_DEVICE`, `KIOSK_MODE`, `GEOFENCE_SYNC`. Tests `KioskSettingsTest` (4), `Launcher_EmitsDeviceLockKeys`, `launcherModel.test.ts`.
- [x] ✅ **[ANDROID] Ubicación sin Google:** solicitud de permisos en tiempo de ejecución (precisa + segundo plano), concesión automática con Device Owner y respaldo `LocationManager` nativo. Cada sincronización reporta ubicación; el servidor evalúa geocercas (regla GeofenceExit → Bloquear equipo).
- [x] ✅ **[ANDROID] Servicio en primer plano:** `AgentForegroundService` (START_STICKY) con heartbeat + comandos cada 60 s y ubicación cada 5 min; exclusión de optimización de batería; arranque tras inscripción, al iniciar el equipo y al abrir el launcher.
- [x] ✅ **[ANDROID] Launcher:** batería/red en cabecera, apps faltantes con aviso y acceso a instalación. Lint: corregidos 3 errores previos (URLDecoder API 33, BATTERY_STATS, Result.Success).
- [x] ✅ **[ANDROID] QR de aprovisionamiento Device Owner:** `scripts/android/New-ProvisioningQr.ps1` genera `provisioning-qr.json` en una línea ASCII sin BOM, con `PACKAGE_DOWNLOAD_LOCATION` (antes `…_URL`, clave inválida), `SIGNATURE_CHECKSUM` calculado del APK y componente completo. El agente responde `GET_PROVISIONING_MODE` y `ADMIN_POLICY_COMPLIANCE` (`ProvisioningActivity`), exigidos desde Android 10.
- [x] ✅ **[INVENTARIO] Exportar Activos a Excel y PDF:** `GET /api/inventory/assets/export/excel|pdf` con los filtros de pantalla (búsqueda, estado, garantía, tipo de equipo, sucursal; la búsqueda usa la misma regla que la tabla). Tests `InventoryAssetExportTests`, `InventoryAssetExportControllerTests`.
- [x] ✅ **[INVENTARIO] Entregas con impresora Honeywell RP4 opcional:** columnas `include_printer`, `printer_id` (FK a `assets`), `printer_serial_number` en `deliveries` (aplicadas en `cisa_ti_itam`); switch en Nueva entrega; la RP4 queda asignada y se libera con la devolución. Tests `DeliveryPrinterTests` (9), `deliveryPrinter.test.ts` (3).
- [x] ✅ **[ANDROID] Inscripción sin intervención (QR de fábrica):** Inscripción → Android Agent genera el QR de 6 toques (APK raw de GitHub + checksum calculado del APK real por `GET /api/enrollment/android/provisioning`, Wi-Fi, servidor y `enrollment_token` en `ADMIN_EXTRAS_BUNDLE`). La credencial puede llevar una política (`EnrollmentTokens.AndroidPolicyId`, migración `AddEnrollmentTokenAndroidPolicy`); el agente se registra solo (`AutoEnrollment`, reintentos con espera exponencial, sobrevive a reinicios) y el servidor encola la política al inscribirse.
- [x] ✅ **[ANDROID] Apps de la política sin Google Play:** `APPLY_LAUNCHER_POLICY` incluye `applications` resueltas contra los APK del repositorio de software (subida `.apk` validada por manifiesto y firma); el agente descarga (SHA-256 verificado) e instala en silencio como Device Owner. `SOFTWARE_INSTALL` también despliega APK a equipos o grupos Android.
- [x] ✅ **[ANDROID] Prueba E2E en emulador Android 13** (API aislada + base `titanmdm_e2e`): inscripción automática, política `Applied`, app instalada con `installReason=POLICY`, despliegue manual `SOFTWARE_INSTALL` y servicio + heartbeat tras reinicio.
- [ ] **[ANDROID] Validar en Honeywell físico** el QR de fábrica completo (el asistente de 6 toques no se puede emular; se probó el mismo camino de extras por `adb` con `ShellProvisioningProvider`, solo debug y solo UID shell).
- [ ] **[CALIDAD] ESLint:** 112 hallazgos previos en otros módulos (reglas nuevas de React Compiler); los archivos tocados en esta fase están limpios.
- [ ] **[OPERACIÓN] Reiniciar el stack** para que el API sirva los endpoints nuevos de catálogos.

---

## 🚀 CRITERIOS DE ACEPTACIÓN FINAL (DEFINITION OF DONE)

1. **Persistencia Real:** Todas las pantallas de ITAM guardan y leen datos de SQL Server.
2. **Flujo Cerrado:** Se puede hacer una Recepción $\rightarrow$ Entrega $\rightarrow$ Devolución $\rightarrow$ Conciliación sin errores.
3. **Cero Errores:** El proyecto compila sin warnings y no hay errores 403/500 en los flujos principales.
4. **Estética Unificada:** No hay pantallas con estilos básicos; todo el módulo ITAM sigue la línea Azul Cielo.
5. **SGBD Unificado:** Cero dependencias de MongoDB en todo el proyecto.
