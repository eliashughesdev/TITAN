#Requires -Version 5.1
#Requires -RunAsAdministrator

<#
.SYNOPSIS
    TitanMDM - Instala/actualiza produccion en Windows Server + IIS.

.DESCRIPTION
    1. Valida IIS, URL Rewrite, ARR y WebSockets.
    2. Copia el paquete (web + api) a -InstallRoot (con respaldo del anterior).
    3. Registra TitanMDM.Api como Servicio de Windows nativo
       (Microsoft.Extensions.Hosting.WindowsServices), inicio automatico
       retrasado, reinicio ante fallo, cuenta virtual NT SERVICE\TitanMDM.Api.
    4. Guarda secretos (ConnectionStrings, Jwt, ...) como variables de
       entorno del servicio (HKLM\...\Services\TitanMDM.Api\Environment).
    5. Crea Application Pool (No Managed Code) y sitio IIS que sirve el
       frontend y hace reverse proxy de /api y /hubs al servicio.
    6. Ponches / Fiorella (Python). Por defecto (-PonchesMode Supervised)
       el propio TitanMDM.Api lo lanza, lo reinicia si cae y lo termina al
       detenerse (PythonServiceSupervisor); este script crea el .venv en
       InstallRoot\ponches. -PonchesMode Nssm conserva el servicio NSSM.
       Los datos (adjuntos, APK, recursos del launcher) viven en
       %ProgramData%\TitanMDM y sobreviven a las actualizaciones.
    7. Verifica /api/health/ready a traves de IIS.

    Idempotente: re-ejecutarlo actualiza la version instalada.

.EXAMPLE
    .\Install-TitanMDM.ps1 -PackageDir C:\Releases\titanmdm-20261104 `
        -HostName titanmdm.empresa.local -SecretsFile C:\TitanMDM-Secrets\titanmdm.secrets.json

.EXAMPLE
    # Agentes Windows/Android que hablan directo al puerto del API
    .\Install-TitanMDM.ps1 -PackageDir ... -ApiBindAddress 0.0.0.0 -OpenApiFirewallPort
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$PackageDir,

    [string]$InstallRoot = "C:\inetpub\TitanMDM",
    [string]$SiteName = "TitanMDM",
    [string]$HostName = "",
    [int]$HttpPort = 80,
    [int]$HttpsPort = 443,
    [string]$CertificateThumbprint = "",

    [string]$ServiceName = "TitanMDMService",
    # Nombre usado por versiones anteriores: se migran sus secretos y se deshabilita.
    [string]$LegacyServiceName = "TitanMDM.Api",
    [string]$ServiceAccount = "",
    [System.Management.Automation.PSCredential]$ServiceCredential,
    [string]$ApiBindAddress = "127.0.0.1",
    [int]$ApiPort = 8020,
    [switch]$OpenApiFirewallPort,

    [string]$SecretsFile = "",
    [string]$AspNetCoreEnvironment = "Production",

    [ValidateSet("Supervised", "Nssm", "None")]
    [string]$PonchesMode = "Supervised",
    # Python 3.11+ del servidor para crear el .venv (por defecto: py -3 o python).
    [string]$PythonExe = "",
    # Solo -PonchesMode Nssm: carpeta externa con .venv propio.
    [string]$PonchesDir = "",
    [string]$NssmPath = "",
    [int]$PonchesPort = 8127,

    [switch]$SkipHealthCheck
)

$ErrorActionPreference = "Stop"

$PackageDir = [IO.Path]::GetFullPath($PackageDir)
$webSource = Join-Path $PackageDir "web"
$apiSource = Join-Path $PackageDir "api"
$webTarget = Join-Path $InstallRoot "web"
$apiTarget = Join-Path $InstallRoot "api"
$logsTarget = Join-Path $InstallRoot "logs"
$dataRoot = Join-Path $env:ProgramData "TitanMDM"
$apiExe = Join-Path $apiTarget "TitanMDM.Api.exe"
$ponchesSource = Join-Path $PackageDir "ponches"
$ponchesTarget = Join-Path $InstallRoot "ponches"
$agentSource = Join-Path $PackageDir "agent\app-debug.apk"
$agentTarget = Join-Path $InstallRoot "agent"

# Datos persistentes: fuera de api\ (robocopy /MIR la reemplaza en cada version).
$storage = [ordered]@{
    "HelpdeskAttachments__StoragePath" = (Join-Path $dataRoot "helpdesk-attachments")
    "SoftwarePackages__StoragePath" = (Join-Path $dataRoot "software")
    "AndroidManagement__LauncherAssetRoot" = (Join-Path $dataRoot "launcher-assets")
}
$legacyAppData = [ordered]@{
    "helpdesk-attachments" = "HelpdeskAttachments__StoragePath"
    "software" = "SoftwarePackages__StoragePath"
    "launcher-assets" = "AndroidManagement__LauncherAssetRoot"
}

if ([string]::IsNullOrWhiteSpace($ServiceAccount)) {
    $ServiceAccount = "NT SERVICE\$ServiceName"
}

function Write-Step([string]$Message) {
    Write-Host "`n[TitanMDM] $Message" -ForegroundColor Cyan
}

function Invoke-Native {
    param([string]$File, [string[]]$Arguments, [switch]$AllowFailure)

    $output = & $File @Arguments 2>&1

    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "$File $($Arguments -join ' ') fallo ($LASTEXITCODE): $output"
    }

    return $output
}

# ============================================================
# 1. PRERREQUISITOS
# ============================================================

Write-Step "Validando paquete y prerrequisitos"

foreach ($path in @(
    (Join-Path $webSource "index.html"),
    (Join-Path $webSource "web.config"),
    (Join-Path $apiSource "TitanMDM.Api.exe"),
    (Join-Path $apiSource "appsettings.json")
)) {
    if (-not (Test-Path $path)) {
        throw "Paquete incompleto, falta: $path (genere con Publish-TitanMDM.ps1)."
    }
}

if (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue) {
    $features = @("Web-Server", "Web-WebSockets", "Web-Static-Content", "Web-Http-Errors", "Web-Stat-Compression")
    $missing = $features | Where-Object { -not (Get-WindowsFeature $_).Installed }

    if ($missing) {
        Write-Host "Instalando caracteristicas IIS: $($missing -join ', ')"
        Install-WindowsFeature -Name $missing -IncludeManagementTools | Out-Null
    }
}

Import-Module WebAdministration -ErrorAction Stop

$appcmd = Join-Path $env:windir "System32\inetsrv\appcmd.exe"
$globalModules = (& $appcmd list config -section:system.webServer/globalModules) -join "`n"

if ($globalModules -notmatch "RewriteModule") {
    throw "Falta IIS URL Rewrite 2.1: https://www.iis.net/downloads/microsoft/url-rewrite"
}

if ($globalModules -notmatch "ApplicationRequestRouting") {
    throw "Falta Application Request Routing 3.0: https://www.iis.net/downloads/microsoft/application-request-routing"
}

$dotnetRuntimes = (& dotnet --list-runtimes 2>$null) -join "`n"

if (-not (Test-Path (Join-Path $apiSource "hostfxr.dll")) -and $dotnetRuntimes -notmatch "Microsoft.AspNetCore.App 10\.") {
    throw "Falta ASP.NET Core Runtime 10 (Hosting Bundle) o publique con -SelfContained."
}

# ARR: proxy habilitado a nivel servidor, sin reescribir Location/Host.
Invoke-Native $appcmd @(
    "set", "config", "-section:system.webServer/proxy",
    "/enabled:True", "/preserveHostHeader:True", "/reverseRewriteHostInResponseHeaders:False",
    "/timeout:00:05:00", "/commit:apphost"
) | Out-Null

# Variable de servidor usada por web.config (X-Forwarded-Proto).
$allowed = (& $appcmd list config -section:system.webServer/rewrite/allowedServerVariables) -join "`n"

if ($allowed -notmatch "HTTP_X_FORWARDED_PROTO") {
    Invoke-Native $appcmd @(
        "set", "config", "-section:system.webServer/rewrite/allowedServerVariables",
        "/+[name='HTTP_X_FORWARDED_PROTO']", "/commit:apphost"
    ) | Out-Null
}

# ============================================================
# 2. DETENER Y RESPALDAR
# ============================================================

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

# Instalaciones anteriores registraban el servicio como TitanMDM.Api.
$legacyService = $null
if ($LegacyServiceName -and $LegacyServiceName -ne $ServiceName) {
    $legacyService = Get-Service -Name $LegacyServiceName -ErrorAction SilentlyContinue
}

if ($legacyService) {
    Write-Step "Deshabilitando el servicio anterior $LegacyServiceName (sus secretos pasan a $ServiceName)"
    if ($legacyService.Status -ne "Stopped") {
        Stop-Service $LegacyServiceName -Force
        (Get-Service $LegacyServiceName).WaitForStatus("Stopped", [TimeSpan]::FromSeconds(60))
    }
    Set-Service $LegacyServiceName -StartupType Disabled
}

if ($existingService -and $existingService.Status -ne "Stopped") {
    Write-Step "Deteniendo servicio $ServiceName"
    Stop-Service $ServiceName -Force
    (Get-Service $ServiceName).WaitForStatus("Stopped", [TimeSpan]::FromSeconds(60))
}

if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
}

if (Test-Path $apiTarget) {
    $backup = Join-Path $InstallRoot ("backup\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    Write-Step "Respaldando version actual en $backup"
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item $apiTarget (Join-Path $backup "api") -Recurse -Force
    if (Test-Path $webTarget) { Copy-Item $webTarget (Join-Path $backup "web") -Recurse -Force }

    # Conservar solo los 5 respaldos mas recientes.
    Get-ChildItem (Join-Path $InstallRoot "backup") -Directory |
        Sort-Object Name -Descending |
        Select-Object -Skip 5 |
        Remove-Item -Recurse -Force
}

# ============================================================
# 3. COPIAR ARCHIVOS
# ============================================================

Write-Step "Copiando archivos a $InstallRoot"

New-Item -ItemType Directory -Path $webTarget, $apiTarget, $logsTarget, $dataRoot -Force | Out-Null
foreach ($path in $storage.Values) { New-Item -ItemType Directory -Path $path -Force | Out-Null }

# Versiones anteriores guardaban los datos en api\App_Data: se migran una
# vez, antes de que /MIR los elimine.
foreach ($folder in $legacyAppData.Keys) {
    $legacy = Join-Path $apiTarget "App_Data\$folder"
    $target = $storage[$legacyAppData[$folder]]
    if ((Test-Path $legacy) -and -not (Get-ChildItem $target -Force -ErrorAction SilentlyContinue)) {
        Write-Host "Migrando $legacy -> $target"
        & robocopy.exe $legacy $target /E /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy fallo migrando $legacy (codigo $LASTEXITCODE)." }
    }
}

foreach ($pair in @(@($webSource, $webTarget), @($apiSource, $apiTarget))) {
    # /MIR elimina archivos obsoletos; robocopy < 8 = exito.
    & robocopy.exe $pair[0] $pair[1] /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null

    if ($LASTEXITCODE -ge 8) {
        throw "robocopy fallo copiando $($pair[0]) (codigo $LASTEXITCODE)."
    }
}

if (Test-Path $agentSource) {
    New-Item -ItemType Directory -Path $agentTarget -Force | Out-Null
    Copy-Item $agentSource $agentTarget -Force
}

# Ponches supervisado: codigo nuevo; .venv, .env y data\ se conservan.
if ($PonchesMode -eq "Supervised") {
    if (-not (Test-Path (Join-Path $ponchesSource "app\main.py"))) {
        throw "El paquete no contiene ponches\app (genere con Publish-TitanMDM.ps1) o use -PonchesMode None."
    }

    New-Item -ItemType Directory -Path $ponchesTarget, (Join-Path $ponchesTarget "data") -Force | Out-Null
    & robocopy.exe (Join-Path $ponchesSource "app") (Join-Path $ponchesTarget "app") /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy fallo copiando Ponches (codigo $LASTEXITCODE)." }
    Copy-Item (Join-Path $ponchesSource "requirements.txt") $ponchesTarget -Force

    $venvDir = Join-Path $ponchesTarget ".venv"
    $venvPython = Join-Path $venvDir "Scripts\python.exe"
    if (-not (Test-Path $venvPython)) {
        Write-Step "Creando entorno virtual de Python en $venvDir"
        if ($PythonExe) {
            Invoke-Native $PythonExe @("-m", "venv", $venvDir) | Out-Null
        }
        elseif (Get-Command py.exe -ErrorAction SilentlyContinue) {
            Invoke-Native "py.exe" @("-3", "-m", "venv", $venvDir) | Out-Null
        }
        elseif (Get-Command python.exe -ErrorAction SilentlyContinue) {
            Invoke-Native "python.exe" @("-m", "venv", $venvDir) | Out-Null
        }
        else {
            throw "No se encontro Python 3 en el servidor. Instalelo para todos los usuarios o use -PythonExe."
        }
    }

    Write-Step "Instalando dependencias de Ponches (pip)"
    Invoke-Native $venvPython @("-m", "pip", "install", "--disable-pip-version-check", "-q", "-r", (Join-Path $ponchesTarget "requirements.txt")) | Out-Null

    if (-not (Test-Path (Join-Path $ponchesTarget ".env"))) {
        Write-Warning "Falta $ponchesTarget\.env (DB_SERVER, DB_NAME, DB_AUTH... de BioTime). Fiorella responde, pero los ponches no hasta crearlo."
    }

    # Un servicio NSSM previo competiria por el mismo puerto.
    $legacyPonches = Get-Service "TitanMDM.Ponches" -ErrorAction SilentlyContinue
    if ($legacyPonches) {
        Write-Host "Deshabilitando el servicio NSSM TitanMDM.Ponches: ahora lo supervisa $ServiceName."
        Stop-Service $legacyPonches.Name -Force -ErrorAction SilentlyContinue
        Set-Service $legacyPonches.Name -StartupType Disabled
    }
}

# El destino del proxy en web.config sigue al puerto del API.
$webConfigPath = Join-Path $webTarget "web.config"
$webConfig = Get-Content $webConfigPath -Raw
$webConfig = $webConfig -replace "http://127\.0\.0\.1:\d+/", "http://127.0.0.1:$ApiPort/"
[IO.File]::WriteAllText($webConfigPath, $webConfig, (New-Object Text.UTF8Encoding($false)))

# ============================================================
# 4. SERVICIO DE WINDOWS
# ============================================================

Write-Step "Registrando servicio $ServiceName ($ServiceAccount)"

$binPath = "`"$apiExe`" --urls http://${ApiBindAddress}:$ApiPort"

if (-not $existingService) {
    Invoke-Native sc.exe @("create", $ServiceName, "binPath=", $binPath, "start=", "delayed-auto", "DisplayName=", "TitanMDM API") | Out-Null
}
else {
    Invoke-Native sc.exe @("config", $ServiceName, "binPath=", $binPath, "start=", "delayed-auto") | Out-Null
}

Invoke-Native sc.exe @("description", $ServiceName, "TitanMDM - API .NET (MDM, Helpdesk, ITAM). Publicado tras IIS en http://${ApiBindAddress}:$ApiPort.") | Out-Null

# Reinicio automatico: 10 s, 30 s, 60 s; contador se reinicia cada 24 h.
Invoke-Native sc.exe @("failure", $ServiceName, "reset=", "86400", "actions=", "restart/10000/restart/30000/restart/60000") | Out-Null
Invoke-Native sc.exe @("failureflag", $ServiceName, "1") | Out-Null

if ($ServiceCredential) {
    $user = $ServiceCredential.UserName
    $password = $ServiceCredential.GetNetworkCredential().Password
    Invoke-Native sc.exe @("config", $ServiceName, "obj=", $user, "password=", $password) | Out-Null
    $ServiceAccount = $user
}
else {
    # Cuentas virtuales / integradas no llevan contrasena.
    Invoke-Native sc.exe @("config", $ServiceName, "obj=", $ServiceAccount) | Out-Null
}

# Permisos minimos: lectura del binario, escritura en logs y llaves.
foreach ($grant in @(
    @($apiTarget, "(OI)(CI)RX"),
    @($logsTarget, "(OI)(CI)M"),
    @($dataRoot, "(OI)(CI)M")
)) {
    Invoke-Native icacls.exe @($grant[0], "/grant", "${ServiceAccount}:$($grant[1])", "/T", "/Q") | Out-Null
}

if ($PonchesMode -eq "Supervised") {
    # Python corre con la cuenta del servicio: lee su codigo y escribe en data\.
    Invoke-Native icacls.exe @($ponchesTarget, "/grant", "${ServiceAccount}:(OI)(CI)RX", "/T", "/Q") | Out-Null
    Invoke-Native icacls.exe @((Join-Path $ponchesTarget "data"), "/grant", "${ServiceAccount}:(OI)(CI)M", "/T", "/Q") | Out-Null
}

if (Test-Path $agentTarget) {
    Invoke-Native icacls.exe @($agentTarget, "/grant", "${ServiceAccount}:(OI)(CI)RX", "/T", "/Q") | Out-Null
}

# Variables de entorno del servicio (secretos fuera de appsettings).
$environmentValues = [ordered]@{
    "ASPNETCORE_ENVIRONMENT" = $AspNetCoreEnvironment
    "DOTNET_ENVIRONMENT" = $AspNetCoreEnvironment
    "DataProtection__KeyRingPath" = (Join-Path $dataRoot "DataProtectionKeys")
    "Ponches__BaseUrl" = "http://127.0.0.1:$PonchesPort"
    "PythonService__Enabled" = $(if ($PonchesMode -eq "Supervised") { "true" } else { "false" })
    "PythonService__WorkingDirectory" = $ponchesTarget
}

foreach ($name in $storage.Keys) { $environmentValues[$name] = $storage[$name] }

if (Test-Path (Join-Path $agentTarget "app-debug.apk")) {
    $environmentValues["AndroidProvisioning__ApkPath"] = (Join-Path $agentTarget "app-debug.apk")
}

if ($HostName) {
    $scheme = if ($CertificateThumbprint) { "https" } else { "http" }
    $publicUrl = "${scheme}://$HostName"
    $environmentValues["Application__FrontendUrl"] = $publicUrl
    $environmentValues["Cors__AllowedOrigins__0"] = $publicUrl
    $environmentValues["WindowsAgentDistribution__PublicServerUrl"] = $publicUrl
}

if ($SecretsFile) {
    if (-not (Test-Path $SecretsFile)) {
        throw "No existe el archivo de secretos: $SecretsFile"
    }

    $secrets = Get-Content $SecretsFile -Raw | ConvertFrom-Json

    foreach ($property in $secrets.PSObject.Properties) {
        if ($property.Name.StartsWith("_")) { continue }
        if ([string]::IsNullOrWhiteSpace([string]$property.Value)) { continue }
        if ([string]$property.Value -match "^<.*>$") {
            throw "El secreto $($property.Name) conserva el valor de ejemplo."
        }

        $environmentValues[$property.Name] = [string]$property.Value
    }
}

$serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$existingEnvironment = @((Get-ItemProperty $serviceKey -Name Environment -ErrorAction SilentlyContinue).Environment)

$legacyEnvironment = @()
if ($legacyService) {
    $legacyEnvironment = @((Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$LegacyServiceName" -Name Environment -ErrorAction SilentlyContinue).Environment)
}

# Conservar valores previos no redefinidos (actualizaciones sin -SecretsFile).
# Orden: servicio anterior < servicio actual < valores de esta instalacion.
$merged = [ordered]@{}
foreach ($line in @($legacyEnvironment) + @($existingEnvironment)) {
    if ($line -match "^([^=]+)=(.*)$") { $merged[$Matches[1]] = $Matches[2] }
}
foreach ($name in $environmentValues.Keys) { $merged[$name] = $environmentValues[$name] }

foreach ($required in @("ConnectionStrings__TitanMdmDatabase", "Jwt__SigningKey", "Jwt__Issuer", "Jwt__Audience")) {
    if (-not $merged.Contains($required)) {
        throw "Falta $required. Proporcione -SecretsFile (ver titanmdm.secrets.example.json)."
    }
}

if ($merged["Jwt__SigningKey"].Length -lt 32) {
    throw "Jwt__SigningKey debe tener al menos 32 caracteres."
}

Set-ItemProperty -Path $serviceKey -Name Environment -Type MultiString `
    -Value @($merged.Keys | ForEach-Object { "$_=$($merged[$_])" })

# La clave del servicio solo debe ser legible por SYSTEM y Administradores.
$acl = Get-Acl $serviceKey
$acl.SetAccessRuleProtection($true, $false)
foreach ($identity in @("NT AUTHORITY\SYSTEM", "BUILTIN\Administrators")) {
    $acl.AddAccessRule((New-Object Security.AccessControl.RegistryAccessRule(
        $identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")))
}
Set-Acl -Path $serviceKey -AclObject $acl

if ($OpenApiFirewallPort -and $ApiBindAddress -ne "127.0.0.1") {
    $ruleName = "TitanMDM API $ApiPort"
    if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $ApiPort -Action Allow | Out-Null
    }
}

Write-Step "Iniciando $ServiceName"
Start-Service $ServiceName
(Get-Service $ServiceName).WaitForStatus("Running", [TimeSpan]::FromSeconds(60))

# ============================================================
# 5. PONCHES (opcional, NSSM)
# ============================================================

if ($PonchesMode -eq "Nssm") {
    if (-not $PonchesDir) {
        throw "-PonchesMode Nssm requiere -PonchesDir (carpeta con .venv propio)."
    }

    $pythonExe = Join-Path $PonchesDir ".venv\Scripts\python.exe"

    if (-not (Test-Path $pythonExe)) {
        throw "No existe $pythonExe. Cree el venv de Ponches e instale requirements.txt."
    }

    if (-not $NssmPath) {
        $NssmPath = (Get-Command nssm.exe -ErrorAction SilentlyContinue).Source
    }

    if (-not $NssmPath -or -not (Test-Path $NssmPath)) {
        throw "NSSM no encontrado. Descargue https://nssm.cc y use -NssmPath."
    }

    $ponchesService = "TitanMDM.Ponches"
    Write-Step "Registrando $ponchesService con NSSM"

    if (-not (Get-Service $ponchesService -ErrorAction SilentlyContinue)) {
        Invoke-Native $NssmPath @("install", $ponchesService, $pythonExe) | Out-Null
    }
    else {
        Invoke-Native $NssmPath @("stop", $ponchesService) -AllowFailure | Out-Null
    }

    $integrationKey = $merged["Ponches__IntegrationKey"]

    foreach ($setting in @(
        @("Application", $pythonExe),
        @("AppParameters", "-m uvicorn app.main:app --host 127.0.0.1 --port $PonchesPort"),
        @("AppDirectory", $PonchesDir),
        @("DisplayName", "TitanMDM Ponches (BioTime)"),
        @("Start", "SERVICE_DELAYED_AUTO_START"),
        @("AppStdout", (Join-Path $logsTarget "ponches.stdout.log")),
        @("AppStderr", (Join-Path $logsTarget "ponches.stderr.log")),
        @("AppRotateFiles", "1"),
        @("AppRotateBytes", "10485760"),
        @("AppExit", "Default Restart"),
        @("AppRestartDelay", "10000"),
        @("AppEnvironmentExtra", "PYTHONIOENCODING=utf-8 TITAN_PONCHES_INTEGRATION_KEY=$integrationKey")
    )) {
        Invoke-Native $NssmPath (@("set", $ponchesService) + $setting) | Out-Null
    }

    Invoke-Native $NssmPath @("start", $ponchesService) | Out-Null
}

# ============================================================
# 6. IIS
# ============================================================

Write-Step "Configurando IIS ($SiteName)"

$poolPath = "IIS:\AppPools\$SiteName"

if (-not (Test-Path $poolPath)) {
    New-WebAppPool -Name $SiteName | Out-Null
}

# Sitio estatico + proxy: sin CLR.
Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ""
Set-ItemProperty $poolPath -Name startMode -Value "AlwaysRunning"
Set-ItemProperty $poolPath -Name processModel.identityType -Value "ApplicationPoolIdentity"

Invoke-Native icacls.exe @($webTarget, "/grant", "IIS AppPool\${SiteName}:(OI)(CI)RX", "/T", "/Q") | Out-Null

if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    New-Website -Name $SiteName -PhysicalPath $webTarget -ApplicationPool $SiteName `
        -Port $HttpPort -HostHeader $HostName -Force | Out-Null
}
else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $webTarget
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $SiteName
}

if ($CertificateThumbprint) {
    if (-not (Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName)) {
        New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -SslFlags 1
    }

    $binding = Get-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName
    $binding.AddSslCertificate($CertificateThumbprint, "My")
}

# SignalR usa WebSockets; tiempo de inactividad amplio para sesiones remotas.
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" -Filter "system.webServer/webSocket" -Name enabled -Value $true

Start-Website -Name $SiteName

# ============================================================
# 7. VERIFICACION
# ============================================================

if (-not $SkipHealthCheck) {
    Write-Step "Verificando salud del API"

    $direct = "http://127.0.0.1:$ApiPort/api/health/ready"
    $hostForCheck = if ($HostName) { $HostName } else { "localhost" }
    $viaIis = "http://127.0.0.1:$HttpPort/api/health/ready"
    $deadline = (Get-Date).AddSeconds(90)
    $ok = $false

    while ((Get-Date) -lt $deadline -and -not $ok) {
        try {
            Invoke-WebRequest $direct -UseBasicParsing -TimeoutSec 5 | Out-Null
            Invoke-WebRequest $viaIis -UseBasicParsing -TimeoutSec 5 -Headers @{ Host = $hostForCheck } | Out-Null
            $ok = $true
        }
        catch {
            Start-Sleep -Seconds 3
        }
    }

    if (-not $ok) {
        throw "El API no respondio en $direct / $viaIis. Revise el Visor de eventos (Aplicacion, origen $ServiceName)."
    }

    Write-Host "[OK] API directo y via IIS saludables." -ForegroundColor Green
}

Write-Host "`nTitanMDM instalado." -ForegroundColor Green
Write-Host "  Sitio   : $SiteName -> $webTarget"
Write-Host "  Servicio: $ServiceName -> http://${ApiBindAddress}:$ApiPort ($ServiceAccount)"
if ($ServiceAccount -like "NT SERVICE\*") {
    Write-Host "  SQL     : conceda acceso a [$env:USERDOMAIN\$env:COMPUTERNAME`$] (o al login '$ServiceAccount' si SQL es local)." -ForegroundColor Yellow
}
