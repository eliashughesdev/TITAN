# ANDROID CORE-01 - Handoff de implementacion backend

## Contexto

- Repositorio: `C:\TitanMDM`
- Rama: `feat/remote-support-rs-h`
- Checkpoint de partida: `6e5e14e`
- Alcance permitido: `src/backend/`, `tests/TitanMDM.UnitTests/`, `tests/TitanMDM.IntegrationTests/`
- Fuera de alcance: frontend (React/CSS/Kotlin) y docs de otros agentes
- Sin commit / push / merge
- Solucion real: `TitanMDM.slnx` (el enunciado menciona `TitanMDM.sln`, que no existe)

Entorno de shell: Windows PowerShell 5.1. No soporta `&&`, `||`, `head`, `grep`,
`ls -la`, rutas `/tmp` ni `/dev/null`.

## Resumen de cambios

| Bloque | Tema | Estado |
| --- | --- | --- |
| A | Error 500 al consultar despliegues (`assignments`) | Resuelto (migracion de columnas del launcher) |
| B | Managed Google Play: origen/orígenes permitidos para el Web Token | Resuelto |
| C | Catalogo corporativo de aplicaciones Managed Google Play | Implementado |
| D | Launcher: distinguir recursos cargados de publicados | Implementado |
| E | Contratos de perfiles v1/v2 y publicacion | Auditado, sin cambios necesarios |
| F | Seguridad de tokens/logs | Parcial (tokens no se registran; pendiente auditoria fina) |
| G | Pruebas | Unitarias nuevas verdes; integracion verde |
| H | Este documento | Entregado |

## Bloque A - HTTP 500 en despliegues Android

### Causa raiz

`AndroidPoliciesController` -> `GET /api/policies/{policyId}/android/assignments`
respondia 500 con «No fue posible consultar los despliegues Android.» (texto en
`src/backend/TitanMDM.Api/Controllers/AndroidPoliciesController.cs:353`).

El origen era una discrepancia modelo/BD: `AndroidDevice` y
`AndroidDeviceConfiguration` declaraban las columnas de acuse del launcher
(`LauncherProfileId`, `LauncherPublicationId`, `LauncherConfigVersion`,
`LauncherAppliedAtUtc`, `LauncherSyncStatus`, `LauncherRestrictionsRead`) pero no
existia migracion para crearlas. SQL Server devolvia "Invalid column name" y la
capa API lo traducia a 500 ocultando la causa real.

### Solucion

Migracion no destructiva (solo `AddColumn`, nullable o `bit` con
`defaultValue: false`):

```
src/backend/TitanMDM.Infrastructure/Persistence/Migrations/20261017002316_AndroidCore01LauncherAckColumns.cs
src/backend/TitanMDM.Infrastructure/Persistence/Migrations/20261017002316_AndroidCore01LauncherAckColumns.Designer.cs
```

`TitanMdmDbContextModelSnapshot.cs` actualizado con las 6 propiedades.

Verificado que era la unica diferencia pendiente de modelo en ese momento.

## Bloque B - Managed Google Play: Web Token

### Causa raiz

`CreateManagedPlayToken` (`src/backend/TitanMDM.Api/Controllers/AndroidEnterpriseController.cs`)
exigia un encabezado `Origin` con esquema HTTPS y lo comparaba contra
`AndroidManagement:ManagedPlayAllowedOrigins`, seccion que no existia en
`appsettings.json` ni `appsettings.Development.json`. Resultado: la lista de
origenes permitidos quedaba vacia y toda solicitud devolvia 403 (o 400 si el front
corre sobre HTTP, como ocurre en desarrollo con `http://localhost:3020`).

### Solucion

Controlador:
- El encabezado `Origin` se valida estructuralmente (absoluto, sin credenciales,
  ruta `/`, sin query/fragment, esquema http o https).
- La lista de origenes permitidos se resuelve primero de
  `AndroidManagement:ManagedPlayAllowedOrigins`; si no esta definida se usa como
  respaldo `Cors:AllowedOrigins` (fuente unica ya existente del frontend).
- La pertenencia a la lista es lo que autoriza; no se confia en el encabezado por
  si solo.

Servicio (`AndroidEnterpriseService.CreateManagedPlayWebTokenAsync`): se relaja la
exigencia de HTTPS a http(s) (el controlador ya aplica la allowlist) conservando la
validacion estructural.

### Contratos nuevos

- No se agregaron DTOs de peticion: el emisor sigue siendo el servidor.
- Respuesta existente `AndroidWebTokenDto(Token, Url, ...)` sin cambios; el front
  (`ManagedGooglePlayPanel.tsx`) espera `{ token, url }`.

## Bloque C - Catalogo corporativo Managed Google Play

No existia entidad de catalogo. Se agrego una entidad propia, unica por
organizacion + paquete.

### Modelo

- `src/backend/TitanMDM.Domain/Enums/AndroidManagedPlayApplicationSource.cs`
- `src/backend/TitanMDM.Domain/Enums/AndroidManagedPlayApprovalState.cs`
- `src/backend/TitanMDM.Domain/Entities/AndroidManagedPlayApplication.cs`
- `src/backend/TitanMDM.Infrastructure/Persistence/Configurations/AndroidManagedPlayApplicationConfiguration.cs`
- `TitanMdmDbContext.AndroidManagedPlayApplications`

Campos: `PackageName` (normalizado a minusculas, validado con regex de paquete
Android), `Source`, `ApprovalState`, `DisplayTitle`, `IconUrl`, `Description`,
`LatestVersionName`, `ProductId`, `InstallationType`, `MetadataError`,
`MetadataFetchedAtUtc`, `CreatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`,
`RemovedAtUtc` (borrado logico + `Reactivate`).

Indices: unico `(OrganizationId, PackageName)`, no unico
`(OrganizationId, ApprovalState)`, e indice `CreatedByUserId`.

### Metadatos confiables

`RegisterManagedPlayApplicationAsync` intenta obtener metadatos oficiales con
`AndroidManagementClient.GetApplicationAsync` (`applications.get`):

- 200 -> `ApplyOfficialMetadata` (titulo, icono, descripcion, version) y estado
  `Available`.
- 404 -> `MarkMetadataUnavailable` (estado `Unavailable`) sin inventar datos.
- Otro fallo -> se registra `LogWarning` y la entrada queda `Pending` (reintentable)
  sin fabricar valores.

Idempotente: si ya existe (org, paquete) se refresca; si estaba eliminado se
reactiva. Carrera sobre el indice unico capturada con `DbUpdateException` y recarga.

### Endpoints

- `GET  /api/android-enterprise/managed-play/applications?search=`
  permiso `android.enterprise.view` o `workspace.android.view`
- `POST /api/android-enterprise/managed-play/applications`
  permiso `android.enterprise.manage`
- `DELETE /api/android-enterprise/managed-play/applications/{applicationId}`
  permiso `android.enterprise.manage`

Todas las escrituras pasan por `RbacAuditFilter` (auditoria administrativa; GET no
audita).

### Migracion

```
src/backend/TitanMDM.Infrastructure/Persistence/Migrations/20261017003131_AndroidCore01ManagedPlayCatalog.cs
src/backend/TitanMDM.Infrastructure/Persistence/Migrations/20261017003131_AndroidCore01ManagedPlayCatalog.Designer.cs
```

Solo `CreateTable("AndroidManagedPlayApplications")` (no destructiva).

## Bloque D - Recursos del launcher

`LauncherAssetStore.ListWithUsageAsync` devuelve `IsReferenced` (referenciado por
alguna version de politica) e `IsPublished` (referenciado por una version con
publicacion `Published`). `CleanupAsync` reutiliza el mismo calculo.

`GET /api/android-enterprise/launcher-assets` ahora es asincrono e incluye
`isReferenced` e `isPublished` por recurso.

## Bloque E - Perfiles v1/v2

`AndroidPolicyCompiler` ya valida y limita `schemaVersion` del launcher al rango
`1..2` y `StampLauncherPublication` mantiene `titan_config_version`. La publicacion
mapea `applicationId`->`packageName` y valida hosts corporativos de recursos
(`LauncherResourceUrlRules` + `AndroidPolicyPublisher.EnsureLauncherResourcesAuthorizedAsync`).
No se requirieron cambios de contrato.

## Bloque F - Seguridad

- El access token de Google solo se coloca en la cabecera `Authorization`; no se
  registra en logs (`AndroidManagementClient`).
- Pendiente: auditoria fina de `ThrowApiExceptionAsync` (que el cuerpo de error
  remoto no se propague con datos sensibles) y limites de tasa por organizacion en
  los endpoints de emision de Web Token.

## Verificacion ejecutada

```
dotnet build TitanMDM.slnx -c Release
# -> Compilacion correcta. 2 advertencias (NU1510 en TitanMDM.WindowsAgent). 0 errores.

dotnet ef migrations add AndroidCore01ManagedPlayCatalog \
  --project src/backend/TitanMDM.Infrastructure \
  --startup-project src/backend/TitanMDM.Api --configuration Release --no-build
# -> solo CreateTable AndroidManagedPlayApplications

dotnet ef migrations has-pending-model-changes \
  --project src/backend/TitanMDM.Infrastructure \
  --startup-project src/backend/TitanMDM.Api
# -> sin cambios de modelo pendientes

dotnet test tests/TitanMDM.UnitTests/TitanMDM.UnitTests.csproj -c Release
# -> 185 superadas, 0 con error

dotnet test tests/TitanMDM.IntegrationTests/TitanMDM.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Android"
# -> superadas, 0 con error
```

Pruebas nuevas: `tests/TitanMDM.UnitTests/AndroidManagedPlayApplicationTests.cs`
(21 casos: normalizacion, validacion de paquete, metadatos oficiales, estado
`Unavailable`, borrado/reactivacion, truncado).

## Notas de operacion

- `dotnet ef` por defecto usa la configuracion Debug. Si se compila en Release, hay
  que pasar `--configuration Release` junto con `--no-build`; de lo contrario EF
  usa ensamblados obsoletos y genera migraciones incorrectas (observado: una
  migracion que re-generaba columnas del launcher).
- Tests de integracion construian `AndroidEnterpriseService` directamente; se
  actualizaron para pasar `NullLogger<AndroidEnterpriseService>.Instance` tras
  agregar `ILogger` al constructor.

## Pendiente (no bloqueante)

- Bloque F: auditoria fina de errores remotos y limites de tasa.
- Aplicar las dos migraciones a la base corporativa real (no se ejecuto contra
  ninguna BD en esta sesion).

## Archivos modificados / creados

Modificados:
- `src/backend/TitanMDM.Api/Controllers/AndroidEnterpriseController.cs`
- `src/backend/TitanMDM.Application/AndroidEnterprise/AndroidEnterpriseContracts.cs`
- `src/backend/TitanMDM.Application/AndroidEnterprise/IAndroidEnterpriseService.cs`
- `src/backend/TitanMDM.Infrastructure/Android/AndroidEnterpriseService.cs`
- `src/backend/TitanMDM.Infrastructure/Android/Policies/LauncherAssetStore.cs`
- `src/backend/TitanMDM.Infrastructure/Persistence/TitanMdmDbContext.cs`
- `src/backend/TitanMDM.Infrastructure/Persistence/Migrations/TitanMdmDbContextModelSnapshot.cs`
- `tests/TitanMDM.IntegrationTests/AndroidEnterpriseConsoleQueryTests.cs`
- `tests/TitanMDM.IntegrationTests/AndroidEnterpriseEnrollmentPolicyTests.cs`

Nuevos:
- entidad, enums y configuracion del catalogo (ver Bloque C)
- migracion `20261017002316_AndroidCore01LauncherAckColumns`
- migracion `20261017003131_AndroidCore01ManagedPlayCatalog`
- `tests/TitanMDM.UnitTests/AndroidManagedPlayApplicationTests.cs`
- `docs/android/ANDROID_CORE01_HANDOFF.md`
