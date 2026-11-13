#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM - Instala TitanMDM.Api como Servicio de Windows (TitanMDMService).

.DESCRIPTION
    1. Toma el API publicado en Release:
         - del paquete (..\api junto a este script, generado por
           Publish-TitanMDM.ps1 / Deploy-To-IIS.ps1), o
         - de -SourceDir, o
         - lo publica desde el repositorio (dotnet publish -c Release).
    2. Lo copia a -InstallRoot\api (respaldo de la version anterior).
    3. Prepara Python (Ponches / Fiorella) en -InstallRoot\ponches: codigo,
       .venv y dependencias. El propio servicio lo lanza al arrancar, lo
       reinicia si cae y lo termina al detenerse (PythonServiceSupervisor).
    4. Registra el servicio con sc.exe:
         - nombre TitanMDMService, inicio automatico con Windows;
         - recuperacion: reinicio a los 5 s, 15 s y 60 s (contador diario),
           tambien si el proceso termina con error;
         - cuenta virtual NT SERVICE\TitanMDMService (minimo privilegio).
    5. Guarda los secretos como variables de entorno del servicio (registro
       con ACL solo para SYSTEM y Administradores).
    6. Arranca el servicio y verifica /api/health/ready y Python.

    Idempotente: volver a ejecutarlo actualiza la instalacion.

.EXAMPLE
    # Primera instalacion
    .\deploy\Install-WindowsService.ps1 -SecretsFile C:\TitanMDM-Secrets\titanmdm.secrets.json -OpenFirewall

.EXAMPLE
    # Comprobar requisitos sin cambiar nada
    .\deploy\Install-WindowsService.ps1 -ValidateOnly

.EXAMPLE
    # Quitar el servicio (conserva archivos y datos)
    .\deploy\Install-WindowsService.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [string]$ServiceName = "TitanMDMService",
    [string]$DisplayName = "TitanMDM Service",
    [string]$SourceDir = "",
    [string]$InstallRoot = (Join-Path $env:ProgramFiles "TitanMDM"),
    [string]$Urls = "http://0.0.0.0:8020",
    [switch]$OpenFirewall,
    [string]$SecretsFile = "",
    [string]$ServiceAccount = "",
    [ValidateSet("Automatic", "AutomaticDelayed")]
    [string]$StartupType = "Automatic",
    [ValidateSet("Supervised", "None")]
    [string]$PonchesMode = "Supervised",
    [string]$PythonExe = "",
    [int]$PonchesPort = 8127,
    [string]$Environment = "Production",
    # Servicio del instalador IIS anterior: sus secretos se migran y se deshabilita.
    [string]$LegacyServiceName = "TitanMDM.Api",
    [switch]$ValidateOnly,
    [switch]$Uninstall,
    [switch]$SkipHealthCheck
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "`n[TitanMDMService] $Message" -ForegroundColor Cyan
}

function Invoke-Native {
    param([string]$File, [string[]]$Arguments, [switch]$AllowFailure)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $File @Arguments 2>&1 | ForEach-Object { "$_" }
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "$File $($Arguments -join ' ') fallo ($LASTEXITCODE): $($output -join ' ')"
    }

    return $output
}

function Test-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-ServiceEnvironment([string]$Name) {
    $key = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"
    $values = [ordered]@{}
    $lines = @((Get-ItemProperty $key -Name Environment -ErrorAction SilentlyContinue).Environment)
    foreach ($line in $lines) {
        if ($line -match "^([^=]+)=(.*)$") { $values[$Matches[1]] = $Matches[2] }
    }
    return $values
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$apiTarget = Join-Path $InstallRoot "api"
$ponchesTarget = Join-Path $InstallRoot "ponches"
$logsTarget = Join-Path $InstallRoot "logs"
$dataRoot = Join-Path $env:ProgramData "TitanMDM"
$apiExe = Join-Path $apiTarget "TitanMDM.Api.exe"
$serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"

if ([string]::IsNullOrWhiteSpace($ServiceAccount)) {
    $ServiceAccount = "NT SERVICE\$ServiceName"
}

if (-not $ValidateOnly -and -not (Test-Administrator)) {
    throw "Ejecute este script en PowerShell como Administrador."
}

# ============================================================
# DESINSTALAR
# ============================================================

if ($Uninstall) {
    $existing = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if (-not $existing) {
        Write-Host "El servicio $ServiceName no existe."
        return
    }

    Write-Step "Deteniendo y eliminando $ServiceName (los archivos y datos se conservan)"
    if ($existing.Status -ne "Stopped") {
        Stop-Service $ServiceName -Force
        (Get-Service $ServiceName).WaitForStatus("Stopped", [TimeSpan]::FromSeconds(60))
    }
    Invoke-Native sc.exe @("delete", $ServiceName) | Out-Null
    Write-Host "[OK] Servicio eliminado." -ForegroundColor Green
    return
}

# ============================================================
# 1. ORIGEN DEL BINARIO (RELEASE)
# ============================================================

Write-Step "Localizando el API publicado en Release"

$packageApi = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\api"))
$packagePonches = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\ponches"))
$repoPonches = Join-Path $repoRoot "integrations\ponches\backend"

if ($SourceDir) {
    $apiSource = [IO.Path]::GetFullPath($SourceDir)
}
elseif (Test-Path (Join-Path $packageApi "TitanMDM.Api.exe")) {
    $apiSource = $packageApi
}
else {
    $apiProject = Join-Path $repoRoot "src\backend\TitanMDM.Api\TitanMDM.Api.csproj"
    if (-not (Test-Path $apiProject)) {
        throw "No hay API publicado (..\api) ni repositorio. Use -SourceDir con la carpeta de dotnet publish."
    }

    $apiSource = Join-Path $repoRoot ("artifacts\service-" + (Get-Date -Format "yyyyMMdd-HHmmss") + "\api")
    if ($ValidateOnly) {
        Write-Host "Se publicaria $apiProject (Release, win-x64) en $apiSource."
    }
    else {
        Write-Host "dotnet publish Release -> $apiSource"
        Invoke-Native dotnet @(
            "publish", $apiProject, "-c", "Release", "-r", "win-x64", "--self-contained", "false",
            "-o", $apiSource, "--nologo", "-p:DebugType=None", "-p:EnvironmentName=$Environment"
        ) | Out-Null
        Remove-Item (Join-Path $apiSource "appsettings.Development.json") -ErrorAction SilentlyContinue
        $productionSettings = Join-Path $PSScriptRoot "iis\appsettings.Production.json"
        if (Test-Path $productionSettings) {
            Copy-Item $productionSettings (Join-Path $apiSource "appsettings.Production.json") -Force
        }
    }
}

if (-not $ValidateOnly -and -not (Test-Path (Join-Path $apiSource "TitanMDM.Api.exe"))) {
    throw "No existe TitanMDM.Api.exe en $apiSource."
}

$ponchesSource = if (Test-Path (Join-Path $packagePonches "app\main.py")) { $packagePonches } else { $repoPonches }

# ============================================================
# 2. PRERREQUISITOS
# ============================================================

Write-Step "Validando prerrequisitos"

$problems = New-Object System.Collections.Generic.List[string]

$selfContained = Test-Path (Join-Path $apiSource "hostfxr.dll")
$runtimes = (& dotnet --list-runtimes 2>$null) -join "`n"
if (-not $selfContained -and $runtimes -notmatch "Microsoft.AspNetCore.App 10\.") {
    $problems.Add("Falta ASP.NET Core Runtime 10 (Hosting Bundle) o publique con -SelfContained.")
}

if ($PonchesMode -eq "Supervised") {
    if (-not (Test-Path (Join-Path $ponchesSource "app\main.py"))) {
        $problems.Add("No se encontro el codigo de Ponches (app\main.py) en $ponchesSource.")
    }

    $venvExists = Test-Path (Join-Path $ponchesTarget ".venv\Scripts\python.exe")
    $pythonFound = $PythonExe -or (Get-Command py.exe -ErrorAction SilentlyContinue) -or (Get-Command python.exe -ErrorAction SilentlyContinue)
    if (-not $venvExists -and -not $pythonFound) {
        $problems.Add("No hay Python 3 en el servidor para crear el .venv de Ponches (instalelo o use -PythonExe / -PonchesMode None).")
    }
}

$legacy = $null
if ($LegacyServiceName -and $LegacyServiceName -ne $ServiceName) {
    $legacy = Get-Service $LegacyServiceName -ErrorAction SilentlyContinue
}

$existingEnvironment = Get-ServiceEnvironment $ServiceName
$legacyEnvironment = if ($legacy) { Get-ServiceEnvironment $LegacyServiceName } else { [ordered]@{} }

$secrets = [ordered]@{}
if ($SecretsFile) {
    if (-not (Test-Path $SecretsFile)) {
        $problems.Add("No existe el archivo de secretos: $SecretsFile")
    }
    else {
        $json = Get-Content $SecretsFile -Raw | ConvertFrom-Json
        foreach ($property in $json.PSObject.Properties) {
            if ($property.Name.StartsWith("_")) { continue }
            if ([string]::IsNullOrWhiteSpace([string]$property.Value)) { continue }
            if ([string]$property.Value -match "^<.*>$") {
                $problems.Add("El secreto $($property.Name) conserva el valor de ejemplo.")
                continue
            }
            $secrets[$property.Name] = [string]$property.Value
        }
    }
}

foreach ($required in @("ConnectionStrings__TitanMdmDatabase", "Jwt__SigningKey", "Jwt__Issuer", "Jwt__Audience")) {
    if (-not ($secrets.Contains($required) -or $existingEnvironment.Contains($required) -or $legacyEnvironment.Contains($required))) {
        $problems.Add("Falta ${required}: use -SecretsFile (ver deploy\iis\titanmdm.secrets.example.json).")
    }
}

$port = ([Uri]($Urls -replace "0\.0\.0\.0|\*|\+", "localhost")).Port
$owner = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -First 1
if ($owner) {
    $process = Get-Process -Id $owner.OwningProcess -ErrorAction SilentlyContinue
    $ours = $process -and $process.Path -and (
        $process.Path -ieq $apiExe -or
        ($legacy -and $process.ProcessName -eq "TitanMDM.Api"))
    if (-not $ours) {
        $problems.Add("El puerto $port esta en uso por $($process.ProcessName) (PID $($owner.OwningProcess)). Detengalo o use -Urls con otro puerto.")
    }
}

foreach ($problem in $problems) { Write-Host "  [!] $problem" -ForegroundColor Yellow }

if ($ValidateOnly) {
    Write-Host ""
    Write-Host "Origen del API : $apiSource"
    Write-Host "Origen Python : $ponchesSource ($PonchesMode)"
    Write-Host "Destino       : $InstallRoot (servicio $ServiceName, $StartupType, $Urls)"
    if ($legacy) { Write-Host "Servicio previo ${LegacyServiceName}: se migraran sus secretos y se deshabilitara." }
    if ($problems.Count -eq 0) { Write-Host "[OK] Listo para instalar." -ForegroundColor Green }
    else { Write-Host "Corrija los puntos anteriores antes de instalar." -ForegroundColor Yellow }
    return
}

if ($problems.Count -gt 0) {
    throw "Prerrequisitos pendientes (ver arriba)."
}

# ============================================================
# 3. DETENER, RESPALDAR Y COPIAR
# ============================================================

$service = Get-Service $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne "Stopped") {
    Write-Step "Deteniendo $ServiceName (Python se detiene con el)"
    Stop-Service $ServiceName -Force
    (Get-Service $ServiceName).WaitForStatus("Stopped", [TimeSpan]::FromSeconds(60))
}

if ($legacy) {
    Write-Step "Deshabilitando el servicio anterior $LegacyServiceName (sus secretos pasan a $ServiceName)"
    if ($legacy.Status -ne "Stopped") {
        Stop-Service $LegacyServiceName -Force
        (Get-Service $LegacyServiceName).WaitForStatus("Stopped", [TimeSpan]::FromSeconds(60))
    }
    Set-Service $LegacyServiceName -StartupType Disabled
}

New-Item -ItemType Directory -Path $apiTarget, $logsTarget, $dataRoot -Force | Out-Null

if (Get-ChildItem $apiTarget -Force -ErrorAction SilentlyContinue) {
    $backup = Join-Path $InstallRoot ("backup\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    Write-Step "Respaldando la version actual en $backup"
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item $apiTarget (Join-Path $backup "api") -Recurse -Force

    Get-ChildItem (Join-Path $InstallRoot "backup") -Directory |
        Sort-Object Name -Descending |
        Select-Object -Skip 5 |
        Remove-Item -Recurse -Force
}

# Datos persistentes fuera de api\ (que se reemplaza en cada version).
$storage = [ordered]@{
    "HelpdeskAttachments__StoragePath" = (Join-Path $dataRoot "helpdesk-attachments")
    "SoftwarePackages__StoragePath" = (Join-Path $dataRoot "software")
    "AndroidManagement__LauncherAssetRoot" = (Join-Path $dataRoot "launcher-assets")
}
foreach ($pair in @(
    @("helpdesk-attachments", "HelpdeskAttachments__StoragePath"),
    @("software", "SoftwarePackages__StoragePath"),
    @("launcher-assets", "AndroidManagement__LauncherAssetRoot")
)) {
    $target = $storage[$pair[1]]
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $old = Join-Path $apiTarget "App_Data\$($pair[0])"
    if ((Test-Path $old) -and -not (Get-ChildItem $target -Force -ErrorAction SilentlyContinue)) {
        & robocopy.exe $old $target /E /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy fallo migrando $old (codigo $LASTEXITCODE)." }
    }
}

Write-Step "Copiando el API a $apiTarget"
& robocopy.exe $apiSource $apiTarget /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy fallo copiando el API (codigo $LASTEXITCODE)." }

# ============================================================
# 4. PYTHON (PONCHES / FIORELLA)
# ============================================================

if ($PonchesMode -eq "Supervised") {
    Write-Step "Preparando Python en $ponchesTarget (lo supervisa $ServiceName)"

    New-Item -ItemType Directory -Path $ponchesTarget, (Join-Path $ponchesTarget "data") -Force | Out-Null
    & robocopy.exe (Join-Path $ponchesSource "app") (Join-Path $ponchesTarget "app") /MIR /XD __pycache__ /XF *.pyc /NFL /NDL /NJH /NJS /NP /R:2 /W:2 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy fallo copiando Ponches (codigo $LASTEXITCODE)." }
    Copy-Item (Join-Path $ponchesSource "requirements.txt") $ponchesTarget -Force

    $venvDir = Join-Path $ponchesTarget ".venv"
    $venvPython = Join-Path $venvDir "Scripts\python.exe"
    if (-not (Test-Path $venvPython)) {
        if ($PythonExe) { Invoke-Native $PythonExe @("-m", "venv", $venvDir) | Out-Null }
        elseif (Get-Command py.exe -ErrorAction SilentlyContinue) { Invoke-Native "py.exe" @("-3", "-m", "venv", $venvDir) | Out-Null }
        else { Invoke-Native "python.exe" @("-m", "venv", $venvDir) | Out-Null }
    }

    Invoke-Native $venvPython @("-m", "pip", "install", "--disable-pip-version-check", "-q", "-r", (Join-Path $ponchesTarget "requirements.txt")) | Out-Null

    # El .venv usa el Python base indicado en pyvenv.cfg: si esta en el perfil
    # de un usuario, la cuenta del servicio no puede ejecutarlo.
    $venvConfig = Get-Content (Join-Path $venvDir "pyvenv.cfg") -ErrorAction SilentlyContinue |
        Where-Object { $_ -match "^\s*home\s*=" } |
        Select-Object -First 1
    if ($venvConfig -and ($venvConfig -split "=", 2)[1].Trim() -like "$env:SystemDrive\Users\*") {
        throw "El .venv apunta a un Python instalado por usuario ($(($venvConfig -split '=', 2)[1].Trim())). Instale Python 'para todos los usuarios', borre $venvDir y repita, o use -PythonExe."
    }

    if (-not (Test-Path (Join-Path $ponchesTarget ".env"))) {
        Write-Warning "Falta $ponchesTarget\.env (DB_SERVER, DB_NAME, DB_AUTH... de BioTime). Fiorella responde, pero los ponches no hasta crearlo."
    }
}

# ============================================================
# 5. REGISTRO DEL SERVICIO
# ============================================================

Write-Step "Registrando $ServiceName ($ServiceAccount, inicio $StartupType)"

$binPath = "`"$apiExe`" --urls $Urls"
$start = if ($StartupType -eq "AutomaticDelayed") { "delayed-auto" } else { "auto" }

if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
    Invoke-Native sc.exe @("create", $ServiceName, "binPath=", $binPath, "start=", $start, "DisplayName=", $DisplayName) | Out-Null
}
else {
    Invoke-Native sc.exe @("config", $ServiceName, "binPath=", $binPath, "start=", $start, "DisplayName=", $DisplayName) | Out-Null
}

Invoke-Native sc.exe @("description", $ServiceName, "TitanMDM - API .NET (MDM, Helpdesk, ITAM, asistentes Titan y Fiorella). Supervisa el servicio Python de Ponches.") | Out-Null

# Recuperacion: reinicio a los 5 s, 15 s y 60 s; el contador se reinicia
# cada 24 h. failureflag 1: tambien si el proceso sale con codigo de error.
Invoke-Native sc.exe @("failure", $ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/15000/restart/60000") | Out-Null
Invoke-Native sc.exe @("failureflag", $ServiceName, "1") | Out-Null

if ($ServiceAccount -notmatch "^NT SERVICE\\" -and $ServiceAccount -notmatch "^(LocalSystem|NT AUTHORITY\\)") {
    throw "Use una cuenta virtual (NT SERVICE\...) o configure la gMSA con sc.exe config obj=."
}
Invoke-Native sc.exe @("config", $ServiceName, "obj=", $ServiceAccount) | Out-Null

foreach ($grant in @(
    @($apiTarget, "(OI)(CI)RX"),
    @($logsTarget, "(OI)(CI)M"),
    @($dataRoot, "(OI)(CI)M")
)) {
    Invoke-Native icacls.exe @($grant[0], "/grant", "${ServiceAccount}:$($grant[1])", "/T", "/Q") | Out-Null
}

if ($PonchesMode -eq "Supervised") {
    Invoke-Native icacls.exe @($ponchesTarget, "/grant", "${ServiceAccount}:(OI)(CI)RX", "/T", "/Q") | Out-Null
    Invoke-Native icacls.exe @((Join-Path $ponchesTarget "data"), "/grant", "${ServiceAccount}:(OI)(CI)M", "/T", "/Q") | Out-Null
}

# ============================================================
# 6. VARIABLES DE ENTORNO (SECRETOS)
# ============================================================

# Prioridad: archivo de secretos > valores ya guardados > servicio anterior.
$merged = [ordered]@{}
foreach ($source in @($legacyEnvironment, $existingEnvironment)) {
    foreach ($name in $source.Keys) { $merged[$name] = $source[$name] }
}

$merged["ASPNETCORE_ENVIRONMENT"] = $Environment
$merged["DOTNET_ENVIRONMENT"] = $Environment
$merged["DataProtection__KeyRingPath"] = (Join-Path $dataRoot "DataProtectionKeys")
$merged["Ponches__BaseUrl"] = "http://127.0.0.1:$PonchesPort"
$merged["PythonService__Enabled"] = if ($PonchesMode -eq "Supervised") { "true" } else { "false" }
$merged["PythonService__WorkingDirectory"] = $ponchesTarget
foreach ($name in $storage.Keys) { $merged[$name] = $storage[$name] }
foreach ($name in $secrets.Keys) { $merged[$name] = $secrets[$name] }

if ($merged["Jwt__SigningKey"].Length -lt 32) {
    throw "Jwt__SigningKey debe tener al menos 32 caracteres."
}

Set-ItemProperty -Path $serviceKey -Name Environment -Type MultiString `
    -Value @($merged.Keys | ForEach-Object { "$_=$($merged[$_])" })

$acl = Get-Acl $serviceKey
$acl.SetAccessRuleProtection($true, $false)
foreach ($identity in @("NT AUTHORITY\SYSTEM", "BUILTIN\Administrators")) {
    $acl.AddAccessRule((New-Object Security.AccessControl.RegistryAccessRule(
        $identity, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")))
}
Set-Acl -Path $serviceKey -AclObject $acl

if ($OpenFirewall -and $Urls -notmatch "127\.0\.0\.1|localhost") {
    $ruleName = "TitanMDM $port"
    if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow | Out-Null
    }
}

# ============================================================
# 7. ARRANQUE Y VERIFICACION
# ============================================================

Write-Step "Iniciando $ServiceName"
Start-Service $ServiceName
(Get-Service $ServiceName).WaitForStatus("Running", [TimeSpan]::FromSeconds(60))

if (-not $SkipHealthCheck) {
    $healthUrl = "http://127.0.0.1:$port/api/health/ready"
    $healthy = $false
    for ($i = 0; $i -lt 30 -and -not $healthy; $i++) {
        try {
            $response = Invoke-WebRequest $healthUrl -UseBasicParsing -TimeoutSec 5
            $healthy = $response.StatusCode -eq 200
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    if ($healthy) { Write-Host "[OK] API responde en $healthUrl" -ForegroundColor Green }
    else { Write-Warning "El API no respondio en 60 s. Revise el Visor de eventos (Aplicacion, origen TitanMDM.Api)." }

    if ($PonchesMode -eq "Supervised") {
        $pythonUp = $false
        for ($i = 0; $i -lt 20 -and -not $pythonUp; $i++) {
            $pythonUp = [bool](Get-NetTCPConnection -State Listen -LocalPort $PonchesPort -ErrorAction SilentlyContinue)
            if (-not $pythonUp) { Start-Sleep -Seconds 2 }
        }
        if ($pythonUp) { Write-Host "[OK] Python (Ponches/Fiorella) escucha en 127.0.0.1:$PonchesPort, lanzado por $ServiceName" -ForegroundColor Green }
        else { Write-Warning "Python no escucha en $PonchesPort. El servicio lo reintentara; revise los eventos [python] en el Visor de eventos." }
    }
}

$config = (Invoke-Native sc.exe @("qc", $ServiceName)) -join "`n"
$recovery = (Invoke-Native sc.exe @("qfailure", $ServiceName)) -join "`n"
Write-Host "`n[OK] $ServiceName instalado." -ForegroundColor Green
Write-Host ($config -split "`n" | Where-Object { $_ -match "START_TYPE|SERVICE_START_NAME|BINARY_PATH_NAME" }) -Separator "`n"
Write-Host ($recovery -split "`n" | Where-Object { $_ -match "RESET_PERIOD|RESTART" }) -Separator "`n"
