# ANDROID UX-01 — Titan Launcher Studio

Frontend únicamente. Sin commit. Checkpoint de referencia: `6e5e14e`.

## Archivos

- `src/frontend/titanmdm-web/src/pages/android-launcher/TitanLauncherPage.tsx`
- `src/frontend/titanmdm-web/src/pages/android-launcher/TitanLauncherPage.css`
- `src/frontend/titanmdm-web/src/pages/android-launcher/LauncherBrandingStudio.tsx`
- `src/frontend/titanmdm-web/src/pages/apps/components/ManagedGooglePlayPanel.tsx` (reutilizado, sin duplicar el iframe)
- `src/frontend/titanmdm-web/src/assistant/styles/titan-assistant.css`

## Qué cambió

El diseñador queda en Identidad, Apariencia, Pantalla y Publicación, con una sola vista previa del teléfono. Los controles del Studio usan el mismo borde, alto y foco. El asistente deja de tapar el final de la página mediante espacio inferior; su capa baja de 1000 a 40.

El catálogo oficial sigue siendo el iframe de Managed Google Play. Se quitaron las sugerencias locales del buscador. El alta manual exige un package name válido y no se presenta como resultado de Play. No hay iconos inventados.

La biblioteca reutiliza `POST/GET /api/android-enterprise/launcher-assets`. La vista previa local no se confunde con una imagen publicada. Bloquear una app la marca oculta; el compilador ya la publica como `BLOCKED`.

Despliegues: un error de `GET /api/policies/{policyId}/android/assignments` ya no se muestra como lista vacía. Aparece el mensaje HTTP y un reintento. Aplicado solo se marca en verde si el launcher confirmó `RestrictionsManager`.

## Validación visual

1. Abrir Titan Launcher, pestaña Diseñador.
2. Recorrer Identidad, Apariencia, Pantalla y Publicación.
3. Adjuntar un PNG y comprobar el progreso y la biblioteca.
4. En Apps, abrir el iframe solo por HTTPS. Sin iframe, usar el alta manual.
5. En Despliegues, forzar un fallo de red y confirmar que no aparece “sin dispositivos”.
6. Comprobar que el asistente no cubre Guardar.

## Contrato que falta en backend

- Metadatos e icono oficial de la app seleccionada en el iframe. El evento actual solo trae `packageName` y `productId`.
- Recorte persistente. El contrato solo tiene `imageFit`: cover, contain, center.
- Carpetas: no existen en el contrato. Pendiente.
- Si despliegues responde 500, el frontend ya muestra el error. El otro agente debe revisar `GET /api/policies/{id}/android/assignments`.

## Limitaciones

No se validó Managed Google Play en un navegador HTTPS real. No se afirma que una app esté instalada. Las pruebas automáticas no sustituyen la revisión visual.
