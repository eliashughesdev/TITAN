# F3-B Titan Launcher Studio Pro

Integración del Launcher corporativo sobre el compilador, el publicador y el agente existentes. No hay un segundo backend ni un agente paralelo.

## Archivos modificados

- `src/frontend/titanmdm-web/src/pages/android-launcher/launcherModel.ts`
- `src/frontend/titanmdm-web/src/pages/android-launcher/launcherModel.test.ts`
- `src/frontend/titanmdm-web/src/pages/android-launcher/LauncherBrandingStudio.tsx`
- `src/frontend/titanmdm-web/src/pages/android-launcher/TitanLauncherPage.tsx`
- `src/frontend/titanmdm-web/src/api/policiesApi.ts`
- `src/backend/TitanMDM.Infrastructure/Android/Policies/AndroidPolicyCompiler.cs`
- `src/backend/TitanMDM.Infrastructure/Android/Policies/LauncherResourceUrlRules.cs`
- `src/backend/TitanMDM.Infrastructure/Android/Policies/AndroidPolicyPublisher.cs`
- `src/backend/TitanMDM.Infrastructure/Android/AndroidManagementOptions.cs`
- `src/backend/TitanMDM.Domain/Entities/AndroidDevice.cs`
- `src/backend/TitanMDM.Infrastructure/Persistence/Configurations/AndroidDeviceConfiguration.cs`
- `src/backend/TitanMDM.Infrastructure/Devices/Agent/DeviceAgentService.cs`
- `src/backend/TitanMDM.Application/Devices/Agent/DeviceHeartbeatRequest.cs`
- `src/backend/TitanMDM.Application/Android/Policies/AndroidPolicyAssignmentDto.cs`
- `src/backend/TitanMDM.Application/Android/Policies/AndroidPolicyAssignmentPageDto.cs`
- `src/backend/TitanMDM.Infrastructure/Android/Policies/AndroidPolicyAssignmentService.cs`
- `agents/TitanMDMAndroidAgent/app/src/main/java/com/titanmdm/agent/launcher/`
- `agents/TitanMDMAndroidAgent/app/src/main/res/xml/app_restrictions.xml`

## Arquitectura

1. Titan Launcher Studio guarda el perfil en la política Android existente (`android.launcher`).
2. `AndroidPolicyCompiler` lo traduce a `managedConfiguration` del paquete `com.titanmdm.agent`.
3. `AndroidPolicyPublisher` sella `profileId`, `publicationId`, `configVersion` y `updatedAt` y hace upsert en Android Management API.
4. Android Device Policy entrega las restricciones. El agente las lee con `RestrictionsManager`.
5. El heartbeat informa la versión leída. Google `APPLIED` no marca el launcher como aplicado.

## Contrato

Claves administradas, además de las ya existentes:

| Campo | Clave | Notas |
| --- | --- | --- |
| Fondo | `titan_wallpaper_url` | HTTPS público PNG, JPEG o WebP. No Base64. |
| Logotipo | `titan_logo_url` | Igual que el fondo. |
| Columnas | `titan_grid_columns` | 3, 4 o 5. Ausente = 4. |
| Esquema | `titan_launcher_schema_version` | 1 o 2. Ausente = legado. |
| Versión | `titan_config_version` | Versión de política sellada al publicar. |
| Perfil | `titan_profile_id` | Id de la política. |
| Publicación | `titan_publication_id` | Id de `AndroidPolicyPublication`. |
| Actualización | `titan_updated_at` | UTC ISO-8601. |

Se conservan empresa, mensaje, colores, apps, instalación obligatoria, HOME y laboratorio debug.

## Publicar y asignar

1. Guardar el perfil en Titan Launcher. El guardado no publica.
2. Publicar. El estado queda `Published` en Google, no aplicado en el dispositivo.
3. Asignar dispositivos del ámbito de sitio. La asignación queda pendiente.
4. Sincronizar inventario. `appliedPolicyState` de Google es independiente.
5. El launcher confirma `RestrictionsManager` por heartbeat. La columna Launcher muestra esa confirmación.

Hosts corporativos: `AndroidManagement:LauncherResourceHosts` y `Organization.PrimaryDomain`. Si alguno está configurado, una URL fuera de esos hosts se rechaza. Si no hay lista, se permite HTTPS público ya validado y se registra una advertencia.

## Emulador Android 13 API 33

Las pruebas con `titan_lab_policy.json` son laboratorio debug. No demuestran Android Device Policy.

1. Instalar el APK debug.
2. Perfil inicial: sin restricciones, pantalla en espera.
3. Laboratorio: copiar JSON a `files/titan_lab_policy.json` y reabrir HOME. No se reporta como administrado.
4. Cambiar fondo, logotipo, columnas y apps en ese JSON. Reiniciar la actividad y comprobar caché en `files/launcher-cache`.
5. URL `http:`, `file:` o IP interna: la política de apps se mantiene si es válida; el recurso se rechaza.
6. `titan_launcher_enabled=false` revoca el perfil. No se reutiliza la versión anterior.

Para Enterprise real hace falta un dispositivo inscrito, publicación AMAPI y `ACTION_APPLICATION_RESTRICTIONS_CHANGED`. Un archivo local no sustituye esa prueba.

## Teléfono físico

Misma secuencia, con el dispositivo inscrito en la empresa de la organización. Comprobar en Despliegues que Launcher muestra `Applied` y la versión sellada. Samsung, Xiaomi, Redmi, Motorola y Honor dependen de Android Device Policy, no de un Device Owner falso. Huawei sin GMS queda fuera.

## Limitaciones

- El fondo del launcher es de la actividad HOME. No cambia el wallpaper del sistema.
- La confirmación de RestrictionsManager requiere heartbeat del agente inscrito.
- Sin lista de hosts corporativos, el compilador solo bloquea destinos inseguros.
- La migración SQL de las columnas nuevas debe aplicarse antes de usar el acuse en una base existente.

## Pruebas

Ejecutar desde la raíz, y Gradle solo desde `agents/TitanMDMAndroidAgent`. Las pruebas SQL que exijan `TITAN_TEST_SQL` no se declaran como pasadas si se omiten.

## Riesgos

- Un `PrimaryDomain` mal configurado bloquea CDN legítimos hasta añadirlo a `LauncherResourceHosts`.
- Google puede reportar la política aplicada antes de que el launcher lea las restricciones.
- La caché sobrevive a un fallo de red de la misma URL, no a una revocación.
