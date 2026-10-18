# Android Fleet - QA de integracion

Fecha: 2026-10-18

## Correccion aplicada

La consulta `DeviceLocationService.GetFleetLocationsAsync` ahora exige simultaneamente:

```text
OrganizationId == organizacion autenticada
Platform == Android
IsDeleted == false
```

El filtro se ejecuta en EF/backend antes de materializar resultados. No es una ocultacion visual.

`FleetLocationDto` incluye `Platform`. `locationApi.getFleet` vuelve a aceptar solo `platform === 'Android'` como defensa ante regresiones. El mismo conjunto alimenta totales, filtros, lista y mapa de `FleetMapPage`.

## Pruebas automatizadas

- `FleetLocations_ExcludeNonAndroidDevices_FromMixedInventory`
  - Crea Android y Windows en la misma organizacion.
  - Ambos tienen coordenadas reales persistidas.
  - Comprueba que solo Android se devuelve.
  - Comprueba `Platform == Android`.
- `locationApi.test.ts`
  - Comprueba que una fila `Windows` accidental es descartada.
- Suite `DeviceLocationServiceTests`: 22/22 PASS.
- Suite frontend completa: 126/126 PASS.

## Datos y estados

El contrato conserva:

- dispositivo;
- plataforma;
- fabricante y modelo;
- estado operativo;
- departamento;
- localidad;
- ultima comunicacion;
- ultima ubicacion y recepcion;
- antiguedad de senal;
- precision;
- bateria;
- estado Fresh/Stale/Offline/NoLocation/PermissionDenied.

No se generan coordenadas. Un dispositivo sin fix conserva latitud/longitud nulas.

## Actualizacion

- Refresco manual en la UI existente.
- Polling de 60 segundos solo con la pagina visible.
- Los resultados representan el ultimo reporte del agente; no son GPS continuo.
- WorkManager y `LocationReportingWorker` respetan politica, permiso y bateria.

## Google Maps

Variable: `VITE_GOOGLE_MAPS_API_KEY`.

El SDK se carga de forma perezosa. Sin clave, la pagina presenta configuracion pendiente sin inventar mapa. La clave debe restringirse por origen y APIs permitidas. Se requiere configuracion de Google Cloud y billing/cuota segun el contrato de Google.

Estado en esta validacion: `BLOCKED` para render real de Maps porque no se valido una clave/origen autorizado.

## Permisos y privacidad

- API: `geofencing.view`.
- Agente: permiso Android de ubicacion.
- Politica corporativa: `LocationReportingPolicy`.
- No se debe presentar la frecuencia de WorkManager como tracking en tiempo real.

## Android Remote

Verificado por inspeccion y build:

- AMAPI: lock, reboot, reset password, start/stop lost mode, wipe y delete.
- El servicio rechaza no Android y dispositivos sin enrolamiento AMAPI.
- El resolver expone compatibilidad segun modo/API/OEM.
- Diagnostico/inventario existe en el canal de agente.
- `RemoteVisionSupported=false` y `RemoteControlSupported=false`; no hay boton funcional que eluda consentimiento.

## Pruebas manuales pendientes

| Caso | Estado | Motivo |
|---|---|---|
| Marcadores Google Maps | BLOCKED | Clave Google Cloud/origen no validados. |
| Samsung fully managed | BLOCKED | Sin dispositivo fisico enrolado. |
| Xiaomi/Redmi/Motorola/Honor | BLOCKED | Requiere hardware y variantes OEM. |
| Huawei sin GMS | BLOCKED | Requiere dispositivo y estrategia sin AMAPI/GMS. |
| Reboot/wipe/lost mode | BLOCKED | Acciones destructivas no se ejecutan sin dispositivo de laboratorio. |
| MediaProjection/control | PENDIENTE | No implementado; requiere consentimiento y mecanismo oficial. |

## Riesgo restante

Geofencing aun debe revisarse como producto separado si debe mostrar exclusivamente Android: su selector historico de dispositivos no forma parte del dataset `/location/fleet`. No afecta la correccion del Mapa de Flota.
