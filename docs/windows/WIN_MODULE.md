# TitanMDM - Módulo Windows (Control Center, Agente y Soporte remoto)

Estado consolidado de la integración Windows del plan on-premise (RS-H),
verificado en la rama `feat/remote-support-rs-h`.

## Compatibilidad (WIN-COMPAT)

| Componente | Target Framework | Runtime | Sistemas soportados |
| --- | --- | --- | --- |
| `TitanMDM.WindowsAgent` | `net10.0-windows` | .NET 10 | Windows 10 1607+ / Windows Server 2016+ / Windows 11 |
| `TitanMDM.RemoteHost` | `net10.0-windows` | .NET 10 | Windows 10 1607+ |
| `TitanMDM.RemoteUiaHost` | `net10.0-windows` | .NET 10 | Windows 10 1607+ (preparación UIAccess, deshabilitado) |

El paquete de distribución se publica `self-contained win-x64`, por lo que
no requiere runtime .NET previo en los equipos gestionados.

## Estado de bloques

### A - Despacho de comandos en tiempo real
- Commit `38952b4`.
- Los comandos quedan en cola en SQL Server (fuente de verdad); SignalR
  solo notifica al agente que existen comandos pendientes.
- El agente procesa la cola y reporta estados progresivos hacia el servidor.

### B - Agente autónomo
- Commit `227521b`.
- Ejecutor de comandos completo: Windows Update, lock/restart/shutdown,
  procesos, servicios, scripts, política, software, kiosk, lost mode
  y solicitudes de ubicación.
- Auditoría productor-ejecutor: todo tipo emitido por el backend tiene
  manejo en el agente.

### C - UI sin JSON crudo
- Commit `9c4e06e`.
- Inventario (equipo, discos, red) y resúmenes de resultados se presentan
  de forma estructurada; el JSON técnico queda bajo `Ver detalle técnico`.

### D - Soporte remoto / UAC
- Commit `94c26f2`.
- `remote.elevate` como permiso separado de `remote.manage`.
- `RemoteElevationController`: fail-closed; reporta `NOT_CONFIGURED` y
  no ejecuta procesos ni entrega tokens privilegiados.
- `RemoteManagedActionsController`: solo operaciones en catálogo fijo
  (reiniciar Spooler) encoladas por la infraestructura de comandos.
- `TitanMDM.RemoteUiaHost`: solo inspección y self-test (32 escenarios
  validados, todas las solicitudes privilegiadas rechazadas).
- `scripts/Test-TitanRemoteUac.ps1`: diagnóstico de solo lectura.

### E - Seguridad
- Commit `cc9c272`.
- RBAC: `devices.commands` (crear) y `devices.view` (leer) exigidos por
  el backend y por la ruta UI.
- Site Scope: `CanAccessDeviceAsync` en todos los endpoints del módulo.
- Auditoría: filtro global `RbacAuditFilter` registra
  `RBAC.DeviceCommands.Create` (Success/Denied/Failure).
- Defensa en profundidad: gate local `devices.commands` en el Control Center.

### F - WIN-COMPAT / Instalador
- Paquete verificado con `Build-TitanMDMAgentPackage.ps1`
  (`artifacts/windows-agent/TitanMDM-WindowsAgent-x64.zip`, verificado).
- SHA-256 del paquete calculado en cada build.

## Verificaciones registradas

- Backend API: compila sin warnings/errores.
- Frontend: `npm run build` sin errores.
- Pruebas unitarias backend: 107 superadas.
- `TitanMDM.RemoteUiaHost --self-test`: OK, 32 escenarios, fail-closed.
- `TitanMDM.RemoteUiaHost --check`: reporta `BLOCKED_NOT_CONFIGURED`.

## Pendientes honestos

- Firma de código (code signing) de agentes e instaladores: sin pipeline.
- La elevación interactiva remota continúa deshabilitada hasta integrar
  autorización de servidor, firma verificada y despliegue aprobado.
- Instalador del servidor (`Setup-TitanMDM.iss`): rollback transaccional
  y validación final no verificados (auditoría F17 PARCIAL).
- Actualización de permisos en producción requiere re-ejecutar el seeder
  para materializar `remote.elevate` en roles existentes.