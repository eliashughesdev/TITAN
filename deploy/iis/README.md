# TitanMDM: despliegue en Windows Server + IIS

## Arquitectura

```
Navegador / Agentes ──HTTPS──► IIS (sitio TitanMDM)
                                 ├─ /            → web\ (build Vite, SPA)
                                 ├─ /api/*       → ARR proxy → 127.0.0.1:8020
                                 └─ /hubs/*      → ARR proxy (WebSockets) → 127.0.0.1:8020
                                                     │
                       Servicio Windows TitanMDM.Api (Kestrel, .NET 10)
                                                     │
                                SQL Server (TitanMdmDatabase / ItamDatabase)
                       Servicio Windows TitanMDM.Ponches (NSSM, uvicorn :8127, opcional)
```

El API escucha solo en loopback. IIS es el único punto de entrada público y se encarga del TLS, la compresión y las cabeceras de seguridad. Si los agentes ya instalados apuntan a `http://servidor:8020`, instale con `-ApiBindAddress 0.0.0.0 -OpenApiFirewallPort` o reconfigúrelos con la URL pública de IIS.

## Prerrequisitos del servidor

| Componente | Notas |
|---|---|
| IIS con Static Content, HTTP Errors, Static Compression y **WebSocket Protocol** | El instalador agrega las características que falten |
| **URL Rewrite 2.1** | https://www.iis.net/downloads/microsoft/url-rewrite |
| **Application Request Routing 3.0** | El instalador habilita el proxy a nivel servidor |
| ASP.NET Core Runtime 10 (Hosting Bundle) | No hace falta si publica con `-SelfContained` |
| NSSM (opcional) | Solo para registrar Ponches como servicio |

## 1. Generar el paquete (en la máquina de build)

```powershell
.\deploy\iis\Publish-TitanMDM.ps1 -OutputDir C:\Releases\TitanMDM-1.0.0
```

El script verifica los tipos del frontend, construye Vite (incluye `public\web.config`) y ejecuta `dotnet publish` (Release, win-x64). Además excluye `appsettings.Development.json` y agrega `appsettings.Production.json`.

## 2. Secretos

Copie `titanmdm.secrets.example.json` **fuera del repositorio**, por ejemplo a `C:\TitanMDM-Secrets\titanmdm.secrets.json`, y complételo. El instalador guarda esos valores como variables de entorno del servicio, en `HKLM\SYSTEM\CurrentControlSet\Services\TitanMDM.Api\Environment`, con ACL restringida a SYSTEM y Administradores. Nunca quedan en `appsettings`.

Claves obligatorias: `ConnectionStrings__TitanMdmDatabase`, `Jwt__SigningKey` (al menos 32 caracteres), `Jwt__Issuer` y `Jwt__Audience`.

## 3. Instalar o actualizar (en el servidor, como Administrador)

```powershell
.\deploy\Install-TitanMDM.ps1 `
    -PackageDir C:\Releases\TitanMDM-1.0.0 `
    -HostName titanmdm.empresa.local `
    -CertificateThumbprint <huella del certificado en LocalMachine\My> `
    -SecretsFile C:\TitanMDM-Secrets\titanmdm.secrets.json `
    -PonchesDir C:\TitanMDM-Ponches\backend -NssmPath C:\Tools\nssm.exe
```

El script es idempotente: vuelva a ejecutarlo con un paquete nuevo para actualizar. Antes de copiar respalda la versión anterior en `InstallRoot\backup\` y conserva los 5 respaldos más recientes. En una actualización `-SecretsFile` es opcional, porque se conservan los valores ya guardados.

### Servicio `TitanMDM.Api`

- Usa el soporte nativo de .NET (`UseWindowsService`). El content root es la carpeta publicada.
- Inicio automático retrasado. Ante un fallo se reinicia a los 10 s, 30 s y 60 s.
- Corre con la cuenta virtual `NT SERVICE\TitanMDM.Api`. Para SQL con autenticación integrada:
  - SQL en otro servidor: conceda acceso a la cuenta de equipo `DOMINIO\SERVIDOR$`.
  - SQL local: conceda acceso al login `NT SERVICE\TitanMDM.Api`.
  - También puede usar `-ServiceCredential` con una gMSA o cuenta de servicio.
- Los logs van al Visor de eventos, en *Aplicación*, con origen `TitanMDM.Api`.
- Si no hay SQL disponible al arrancar, el servicio termina con un mensaje explícito y el SCM lo reintenta.

### CORS

En producción el frontend y el API comparten origen gracias al proxy de IIS, así que CORS solo aplica a orígenes externos. El instalador fija `Cors__AllowedOrigins__0` con la URL pública derivada de `-HostName`.

## Verificación

```powershell
Invoke-WebRequest http://127.0.0.1:8020/api/health/ready      # directo
Invoke-WebRequest https://titanmdm.empresa.local/api/health/ready  # vía IIS
Get-Service TitanMDM.Api, TitanMDM.Ponches
```

## Rollback

```powershell
Stop-Service TitanMDM.Api
robocopy C:\inetpub\TitanMDM\backup\<fecha>\api C:\inetpub\TitanMDM\api /MIR
robocopy C:\inetpub\TitanMDM\backup\<fecha>\web C:\inetpub\TitanMDM\web /MIR
Start-Service TitanMDM.Api
```
