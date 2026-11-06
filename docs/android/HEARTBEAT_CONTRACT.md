# Contrato de heartbeat y alarma remota

## Endpoint

`POST /api/device/heartbeat`

Autenticacion por `deviceId` + `deviceSecret` dentro del cuerpo. Respuesta
`200 OK` con `HeartbeatResponse`.

## Cuerpo actualizado

```json
{
  "deviceId": "11111111-1111-1111-1111-111111111111",
  "deviceSecret": "secreto-del-dispositivo",
  "ipAddress": "172.21.20.55",
  "batteryLevel": 82,
  "agentVersion": "1.0.0",
  "operatingSystemVersion": "Android 13",
  "latitude": 18.4861,
  "longitude": -69.9312,
  "locationAccuracyMeters": 12.5,
  "locationProvider": "fused",
  "bssid": "aa:bb:cc:dd:ee:ff",
  "rssi": -57,
  "locationCapturedAtUtc": "2026-11-06T14:22:31Z"
}
```

Los campos de ubicacion son opcionales. El agente los omite cuando no hay
lectura disponible. `bssid`/`rssi` se envian aunque no exista fix GPS.

## Resolucion en backend

1. Si hay `latitude` y `longitude`, se registra la posicion directamente.
2. Si no hay coordenadas pero si `bssid`, se busca en
   `Inventory:WifiAccessPoints` (configuracion de la API).
3. Si no hay coordenadas ni BSSID conocido, se registra estado
   `Unavailable`/`WIFI_ONLY`; nunca se inventa una posicion.

Ejemplo de configuracion:

```json
"Inventory": {
  "TimeZoneId": "America/Santo_Domingo",
  "WifiAccessPoints": [
    {
      "Bssid": "AA:BB:CC:DD:EE:FF",
      "Latitude": 18.4861,
      "Longitude": -69.9312,
      "Label": "Nave principal - AP1"
    }
  ]
}
```

## Alarma remota

La UI crea el comando mediante:

`POST /api/device-commands/{deviceId}/ring?durationSeconds=30`

El agente recibe `PLAY_ALARM` (o `RING_DEVICE`), eleva `STREAM_ALARM` al
maximo, reproduce el tono predeterminado en bucle con `MediaPlayer`,
activa vibracion y detiene la alarma a los 30 segundos o al tocar la
pantalla.

## Persistencia del agente

- Foreground service `specialUse|dataSync|location` con `START_STICKY`.
- Notificacion persistente de alta prioridad (canal silencioso).
- Wake lock parcial y `NetworkCallback` para despertar el ciclo.
- Reintentos con backoff exponencial (2 s a 60 s, jitter de 1 s).
- Watchdog de `AlarmManager` cada 15 minutos.
- Exencion de optimizacion de bateria solicitada por
  `ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`.
