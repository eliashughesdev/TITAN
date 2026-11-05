# Matriz verificable de capacidades Android Enterprise

Estado de implementación de TitanMDM para `ANDROID-FIX-02`. Esta matriz describe capacidades respaldadas por código y contratos actuales; no garantiza que Google, el fabricante o un dispositivo concreto acepten una operación.

## Leyenda

| Estado | Significado |
|---|---|
| Disponible | Existe flujo implementado, sujeto a RBAC, alcance, versión y respuesta del proveedor. |
| Limitado | Disponible solo dentro del ámbito administrado indicado. |
| Pendiente | Requiere datos adicionales, integración del fabricante o validación con hardware físico. |
| No compatible | El modo o la implementación actual no permite la capacidad. |

## Matriz por modo

| Capacidad | Fully Managed | Work Profile | Dedicated / Kiosk | Samsung / Knox | Evidencia o condición |
|---|---|---|---|---|---|
| Bloqueo | Disponible | Limitado al perfil de trabajo | Disponible | Disponible por Android Enterprise; Knox no requerido | `AndroidRemoteCapabilityResolver.ResolveLock` y comando AMAPI `LOCK`. |
| Reinicio remoto | Disponible desde API 24 | No compatible | Disponible desde API 24 | Pendiente de validación física por modelo; no existe conector Knox | `ResolveReboot` y comando AMAPI `REBOOT`. |
| Restablecer contraseña | Disponible | No compatible | Disponible | Pendiente de validación física; no existe conector Knox | `ResolveResetPassword`, RBAC y confirmación previa. |
| Modo perdido | Disponible desde API 24 | No compatible | Disponible desde API 24 | Pendiente de validación física | `ResolveLostMode` y comandos AMAPI `START_LOST_MODE` / `STOP_LOST_MODE`. |
| Borrado | Borrado de fábrica con confirmación reforzada | Limitado a eliminar el perfil administrado | Borrado de fábrica con confirmación reforzada | Disponible por AMAPI; capacidades Knox adicionales no implementadas | `ResolveWipe`, permiso `android.enterprise.actions`, alcance y auditoría. |
| Eliminar recurso de Google | Disponible | Disponible | Disponible | Disponible por AMAPI | HTTP DELETE de Android Management API; no confirma borrado físico. |
| Políticas y cumplimiento | Disponible | Limitado al perfil y aplicaciones de trabajo | Disponible | Disponible por Android Enterprise; extensiones Knox pendientes | `AndroidPoliciesController`, servicios de políticas y estado de aplicación reportado. |
| Managed Google Play | Disponible | Limitado al perfil de trabajo | Disponible | Disponible si el dispositivo tiene GMS autorizado | Módulos Android Enterprise y aplicaciones existentes. |
| Configuraciones administradas | Disponible para aplicaciones compatibles | Limitado a aplicaciones del perfil | Disponible para aplicaciones compatibles | Pendiente para extensiones OEM | Contratos de políticas/aplicaciones existentes; depende del esquema de cada aplicación. |
| Cámara, USB y restricciones | Según política y versión | Limitado al ámbito admitido por el perfil | Según política y versión | Extensiones Knox pendientes de integración y licencia | Debe verificarse contra la política aplicada; no se fuerza fuera de las APIs oficiales. |
| Inventario de aplicaciones | Según datos AMAPI sincronizados | Limitado al inventario expuesto por AMAPI | Según datos AMAPI sincronizados | Sin inventario Knox adicional | Sincronización Android Enterprise; no se infieren aplicaciones no reportadas. |
| Batería y conectividad | Cuando el inventario/agent los reporta | Cuando el inventario/agent los reporta | Cuando el inventario/agent los reporta | Sin telemetría Knox adicional | `DeviceDetails.batteryLevel`, `status`, `ipAddress`; la UI muestra `No reportada` si falta fuente. |
| Almacenamiento y consumo de datos | Pendiente | Pendiente | Pendiente | Pendiente de fuente autorizada | Los contratos actuales no exponen métricas verificables; la UI no inventa valores. |
| Geolocalización empresarial | Disponible con política, permiso y reporte del agente | Disponible con política, permiso y reporte del agente | Disponible con política, permiso y reporte del agente | Sin ruta Knox adicional | `LocationController`, `DeviceLocationService` y política de retención. Nunca se estima una posición. |
| Kiosk y Titan Launcher | Condicionado por política y aplicación instalada | No compatible como launcher global | Disponible como modo objetivo | Pendiente para extensiones Knox | Módulos Kiosk/Titan Launcher existentes; requiere validación física del flujo completo. |
| Diagnóstico remoto | Disponible para datos AMAPI/TitanMDM reportados | Disponible para datos del perfil/dispositivo expuestos | Disponible para datos reportados | Sin diagnóstico Knox adicional | `AndroidDiagnosticsPanel` y endpoints de detalle/capacidades. |
| Visualización de pantalla | No compatible actualmente | No compatible actualmente | No compatible actualmente | Pendiente de una integración específica autorizada | No existe MediaProjection ni transporte de pantalla Android en el repositorio. |
| Control interactivo universal | No compatible | No compatible | No compatible | Pendiente de APIs OEM, licencia y autorización | TitanMDM no eleva privilegios ni controla ajustes arbitrarios. |

## Controles obligatorios

- Lectura del Centro de Control: `workspace.android.view` y `android.enterprise.view` en la ruta frontend; los endpoints de inventario aceptan el permiso Android de lectura definido por backend.
- Sincronización: `android.enterprise.manage`.
- Acciones remotas: `android.enterprise.actions`.
- Organización y localidad: validadas por los controladores/servicios Android mediante `IScopeAccessService` y comprobación del dispositivo objetivo.
- Acciones destructivas: confirmación escribiendo el nombre del dispositivo, autorización backend y auditoría.
- Resultado: `Issued` o `DeletionRequested` significa solicitud emitida. No se traduce a “completada” sin una señal posterior del dispositivo/proveedor.

## Evidencia automatizada

- Resolución por modo/API/fabricante: `tests/TitanMDM.UnitTests/AndroidRemoteCapabilityResolverTests.cs`.
- Aislamiento, alcance, duplicados y emisión: `tests/TitanMDM.IntegrationTests/AndroidRemoteActionServiceTests.cs`.
- Inventario y auditoría: `tests/TitanMDM.IntegrationTests/AndroidEnterpriseConsoleQueryTests.cs`.
- UI, capacidades y confirmación destructiva: `src/frontend/titanmdm-web/src/pages/devices/components/remote/AndroidRemoteConsole.test.tsx`.

## Validación física pendiente

- Reinicio, modo perdido, contraseña y borrado en al menos un equipo Fully Managed y uno Dedicated.
- Eliminación exclusiva del Work Profile sin afectar el espacio personal.
- Kiosk/Titan Launcher tras reinicio y pérdida de conectividad.
- Modelos Samsung objetivo; cualquier extensión Knox requiere licencia, SDK, permisos y pruebas separadas antes de marcarla disponible.
