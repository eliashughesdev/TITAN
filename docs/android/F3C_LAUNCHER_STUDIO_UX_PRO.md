# F3-C Titan Launcher Studio UX Pro

Extiende el Studio y el contrato F3-B. No hay una segunda pantalla ni un agente paralelo.

## Archivos

- `LauncherBrandingStudio.tsx`, `TitanLauncherStudioPro.css`, `launcherModel.ts`
- `TitanLauncherPage.tsx`
- `ManagedGooglePlayPanel.tsx`
- `LauncherAssetStore.cs`, `AndroidEnterpriseController.cs`, `DeviceLauncherAssetsController.cs`
- `AndroidPolicyCompiler.cs`, `AndroidPolicyPublisher.cs`
- `TitanLauncherPolicy.kt`, `TitanLauncherScreen.kt`

## Imagenes

El administrador adjunta PNG, JPEG o WebP. El backend valida bytes, tamaño y tipo, y guarda el archivo fuera del web root como `{organizacion}/{guid}.bin`. La respuesta solo devuelve id, token `titan-asset:{guid}`, formato y hash. No devuelve ruta de servidor ni Base64.

Al publicar, el token se convierte en `https://{LauncherAssetBaseUrl}/api/device/launcher-assets/{guid}.{ext}` si la base es HTTPS pública. El dispositivo descarga con `X-Titan-Device-Id` y `X-Titan-Device-Secret`, y solo recibe recursos de su organización. `POST /api/android-enterprise/launcher-assets/cleanup` borra recursos que ninguna versión de política referencia.

## Managed Google Play

Se reutiliza el iframe oficial y `POST /api/android-enterprise/managed-play/web-token`. Seleccionar una app solo la agrega al borrador. No significa aprobada, publicada, asignada ni instalada.

Si el iframe no está disponible, hay sugerencias locales etiquetadas como tales y un alta manual con package name validado. No se inventan resultados de Play ni se hace scraping.

## Tema

El perfil conserva los campos anteriores y añade colores, radio, opacidad, sombra, columnas, espaciado, tamaño de icono y ajuste de imagen. El contraste WCAG se calcula en el diseñador y advierte por debajo de 4.5:1. Esquema 1 sigue compilando.

## Prueba

Android 13/15: guardar, publicar y asignar un perfil con recurso corporativo. Confirmar en Despliegues que Google `APPLIED` no equivale a launcher aplicado. El iframe de Play requiere HTTPS y un origen permitido; sin eso el flujo manual sigue disponible.

## Limitaciones

- `LauncherAssetBaseUrl` vacío impide publicar un token de imagen. La vista previa local sí funciona.
- El catálogo local no es el catálogo en vivo de Google.
- No se marcó ninguna prueba real de Managed Google Play como superada.
- La migración SQL de las columnas de acuse del launcher de F3-B sigue pendiente si la base no se crea con el modelo actual.

## Pendientes externos

- Origen HTTPS aprobado para el iframe.
- Base pública HTTPS para distribuir imágenes al teléfono.
- Confirmación en un dispositivo inscrito de que Android Device Policy entregó el managed configuration.
