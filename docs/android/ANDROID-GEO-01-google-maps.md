# ANDROID GEO-01 — Geolocalización de flota

Panel React con Google Maps, captura Kotlin periódica y backend con RBAC, auditoría e aislamiento organizacional. Sin commit. Sin validación en dispositivo físico.

## Archivos

### Frontend (`src/frontend/titanmdm-web`)

- `src/config/googleMaps.ts` — loader perezoso de Google Maps (`loadGoogleMaps()`, `isGoogleMapsConfigured()`).
- `src/api/locationApi.ts` — `getFleetLocations`, `getDeviceLocation`, `getReportingPolicy`, `updateReportingPolicy` (requiere permiso `geofencing.manage`).
- `src/pages/fleet-map/FleetMapPage.tsx` + `.css` — panel de flota con mapa, lista lateral y polling.
- `src/pages/fleet-map/FleetMapPage.test.tsx` — tests del panel (sin Google Maps, con mock).
- `src/config/workspaceNavigation.ts` — entrada "Mapa de Flota" (`/fleet-map?workspace=android`, icono `Map`).
- `src/App.tsx` — import de `FleetMapPage` (la ruta `fleet-map` ya existía con permiso `geofencing.view`).
- `.env.example` — `VITE_GOOGLE_MAPS_API_KEY` (solo frontend; nunca backend ni proveedores de IA).

### Backend (`src/backend`)

- `TitanMDM.Domain/Entities/DeviceLocationState.cs` — estado por dispositivo (`Fresh/Stale/Offline/NoLocation/PermissionDenied`, `DeviceCaptureState`).
- `TitanMDM.Domain/Entities/LocationReportingPolicy.cs` — política por organización (`IsEnabled`, `ReportingIntervalMinutes` mín. 15, `RetentionDays`).
- `TitanMDM.Infrastructure/Persistence/Configurations/` — configuraciones EF de ambas entidades.
- `TitanMDM.Infrastructure/Persistence/TitanMdmDbContext.cs` — DbSets + modelo.
- `TitanMDM.Infrastructure/Persistence/Migrations/20261018005835_AddLocationReportingPoliciesAndDeviceLocationStates.cs` — migración 100% aditiva (incluye drift preexistente de automatización ya commiteado sin migrar).
- `TitanMDM.Application/Services/DeviceLocationService.cs` — ingesta de estados, guard de política proactiva, upsert de `DeviceLocationState`, SignalR best-effort, geofencing, purga por retención.
- `TitanMDM.Application/Contracts/IDeviceLocationService.cs` — DTOs y filtros (`siteId`, `status`, `locationState`).
- `TitanMDM.Api/Controllers/LocationController.cs` — flota/detalle/políticas con RBAC (`geofencing.view` / `geofencing.manage`) y auditoría.
- `TitanMDM.Api/Controllers/DeviceCommandAgentController.cs` — `POST location` y `GET location/policy` autenticados con `X-Titan-Device-Id` / `X-Titan-Device-Secret` (NO JWT).
- `TitanMDM.Api/Realtime/LocationSignalRNotifier.cs` + registro en `Program.cs`.

### Agente Android (`agents/TitanMDMAndroidAgent`)

- `app/src/main/java/com/titanmdm/agent/location/LocationCapturePolicy.kt` — decisión pura: `ReportCoordinates` | `ReportStatusOnly(PERMISSION_DENIED|UNAVAILABLE)` | `Skip`. Prioridad: permiso → política → batería (<15% o ahorro) → intervalo (máx. entre intervalo del servidor y mínimo WorkManager de 15 min).
- `app/src/main/java/com/titanmdm/agent/workers/LocationReportingWorker.kt` — consulta política con auth de dispositivo, captura con FusedLocationProvider (timeout 10 s, sin `kotlinx-coroutines-play-services`), reporta coordenadas o solo estado, registra último reporte en SharedPreferences.
- `app/src/main/java/com/titanmdm/agent/core/network/TitanApiService.kt` — `GET api/device/commands/location/policy` y `POST api/device/commands/location` con headers de dispositivo.
- `app/src/main/java/com/titanmdm/agent/core/network/TitanApiModels.kt` — `LocationReportRequest`, `LocationReportingPolicyResponse`.
- `app/src/test/java/com/titanmdm/agent/location/LocationCapturePolicyTest.kt` — 15 tests JVM.
- `app/src/main/AndroidManifest.xml` — `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION`, `ACCESS_BACKGROUND_LOCATION`.

## Qué cambió

El mapa usa el loader perezoso de Google Maps con estados explícitos: Configuración Pendiente (sin clave), Error (fallo de red o clave inválida) y Carga. Solo se dibujan marcadores con coordenadas reales; los dispositivos sin fix aparecen en la lista lateral con su estado (`NoLocation`, `PermissionDenied`, etc.). La lista usa left-join para incluir dispositivos sin última ubicación.

La ingesta del agente actualiza `DeviceLocationState` en cada reporte. Reportes de solo estado (`PERMISSION_DENIED`, `UNAVAILABLE`) se registran aunque la política esté deshabilitada; reportes proactivos con coordenadas se descartan si `IsEnabled` es falso. Fuentes "Proactive" respetan la política; on-demand siempre se procesa.

La política del agente se obtiene con `GET api/device/commands/location/policy` autenticada con credenciales de dispositivo. El intervalo configurado por el administrador se respeta si es mayor al mínimo de WorkManager (15 min); valores menores se recortan al mínimo. La batería por debajo del 15% o el ahorro de batería bloquean la captura proactiva.

El estado de flota se resuelve con la prioridad: `PermissionDenied` → `NoLocation` → `Offline`/`Retired` → `Stale` (> 60 min) → `Fresh`.

## Validación

- Backend: `dotnet build` 0 errores; 238 unit + 106 integration OK (21 pruebas nuevas de `DeviceLocationService`).
- Frontend: `npm run build`, `npx eslint` limpio, `npm run test` 111/111 (10 nuevos).
- Agente: `:app:compileDebugKotlin`, `:app:testDebugUnitTest` (15/15), `:app:assembleDebug` — todos OK.

## Limitaciones

No se validó en un dispositivo físico ni con una clave real de Google Maps. La compilación del APK no demuestra que la captura funcione en el dispositivo. `ACCESS_BACKGROUND_LOCATION` requiere consentimiento explícito del usuario en Android 10+. El geofencing del backend es una validación básica de proximidad, no un motor de geocercas completo.

La migración `20261018005835` incluye tablas de automatización que el modelo definía sin migración previa (drift preexistente); es aditiva y no destruye datos, pero debe revisarse antes de aplicarse en producción.
