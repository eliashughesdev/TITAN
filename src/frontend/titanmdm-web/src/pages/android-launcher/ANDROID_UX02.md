# ANDROID UX-02

Studio V3 dentro de `src/frontend/titanmdm-web`. Sin cambios de backend ni commit.

## Archivos

- `TitanLauncherStudioV3.tsx`
- `TitanLauncherStudioV3.css`
- `studioWorkflow.ts`
- `TitanLauncherPage.tsx`
- pruebas `studioWorkflow.test.ts` y `TitanLauncherStudioV3.test.tsx`

## Cambio

El diseñador deja de ser un formulario largo. La barra muestra perfil, versión, estado y las acciones Guardar, Publicar, Previsualizar y Despliegues. El inspector solo muestra la herramienta activa. El teléfono central refleja colores, fondo, logotipo, columnas e iconos de marcador, no iconos oficiales inventados.

Dashboard, perfiles, despliegues, inventario y auditoría siguen en la navegación existente. Apps abre el mismo Studio en la herramienta de aplicaciones.

## UX-03

El grid del Studio usa columnas de 180–220 px, centro flexible y inspector de 340–440 px. El teléfono crece con el alto del escenario. El catálogo Play y las filas de apps se contienen dentro del inspector, sin cambiar la página independiente de Play.

La revisión visual a 1366, 1920 y 2560 no se ejecutó en navegador: no hay automatización de viewport en este entorno.

## Limitaciones

Play solo se abre en HTTPS. El recorte libre y las carpetas no se persisten porque el contrato no los tiene. Publicado no se muestra como aplicado.
