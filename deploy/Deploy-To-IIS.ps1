#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM - Compila, empaqueta y (opcionalmente) instala en IIS.

.DESCRIPTION
    Punto de entrada unico para produccion en Windows Server + IIS:

      1. Publish-TitanMDM.ps1: build del frontend (Vite, con web.config),
         dotnet publish Release del API y codigo Python de Ponches/Fiorella
         en una carpeta limpia (-OutputDir).
      2. Verifica que el paquete este completo.
      3. Con -Install (como Administrador): Install-TitanMDM.ps1 registra el
         servicio TitanMDM.Api, el sitio IIS (frontend + proxy /api y /hubs
         con WebSockets) y prepara Python, que el API supervisa.
      4. Con -Install: comprueba los permisos de lectura/escritura que
         necesitan IIS AppPool\<Sitio> y la cuenta del servicio.

    Arquitectura: el API corre como Servicio de Windows detras de IIS (ARR),
    no dentro del Application Pool. Un pool se recicla y se detiene por
    inactividad: cortaria las conexiones SignalR de agentes y chats, los
    servicios en segundo plano y el proceso de Python.

.EXAMPLE
    # Solo generar el paquete (maquina de build)
    .\deploy\Deploy-To-IIS.ps1 -OutputDir C:\Releases\TitanMDM-1.0.4

.EXAMPLE
    # Generar e instalar en el servidor
    .\deploy\Deploy-To-IIS.ps1 -Install -HostName titanmdm.empresa.local `
        -CertificateThumbprint <huella> -SecretsFile C:\TitanMDM-Secrets\titanmdm.secrets.json
#>

[CmdletBinding()]
param(
    [string]$OutputDir = "",
    [switch]$SelfContained,
    [switch]$SkipTests,

    [switch]$Install,
    [string]$InstallRoot = "C:\inetpub\TitanMDM",
    [string]$SiteName = "TitanMDM",
    [string]$ServiceName = "TitanMDMService",
    [string]$HostName = "",
    [int]$HttpPort = 80,
    [string]$PublicUrl = "",
    [int]$ApiPort = 8020,
    [int]$PonchesPort = 8127,
    [string]$CertificateThumbprint = "",
    [string]$SecretsFile = "",
    [ValidateSet("Supervised", "Nssm", "None")]
    [string]$PonchesMode = "Supervised",
    [string]$PythonExe = ""
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$iisDir = Join-Path $PSScriptRoot "iis"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root ("artifacts\titanmdm-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}
$OutputDir = [IO.Path]::GetFullPath($OutputDir)

# ------------------------------------------------------------
# 1. PAQUETE
# ------------------------------------------------------------

$publishArgs = @{ OutputDir = $OutputDir }
if ($SelfContained) { $publishArgs.SelfContained = $true }
if ($SkipTests) { $publishArgs.SkipTests = $true }

& (Join-Path $iisDir "Publish-TitanMDM.ps1") @publishArgs

# ------------------------------------------------------------
# 2. VERIFICACION DEL PAQUETE
# ------------------------------------------------------------

Write-Host "`n=== Verificando paquete ===" -ForegroundColor Cyan

$required = @(
    "web\index.html",
    "web\web.config",
    "api\TitanMDM.Api.exe",
    "api\appsettings.json",
    "api\appsettings.Production.json",
    "ponches\app\main.py",
    "ponches\requirements.txt",
    "deploy\Install-TitanMDM.ps1"
)

$missing = $required | Where-Object { -not (Test-Path (Join-Path $OutputDir $_)) }
if ($missing) {
    throw "Paquete incompleto: $($missing -join ', ')"
}

foreach ($forbidden in @("api\appsettings.Development.json", "ponches\.env", "ponches\.venv", "ponches\data")) {
    if (Test-Path (Join-Path $OutputDir $forbidden)) {
        throw "El paquete no debe incluir $forbidden."
    }
}

[xml]$webConfig = Get-Content (Join-Path $OutputDir "web\web.config") -Raw
$rules = @($webConfig.configuration.'system.webServer'.rewrite.rules.rule | ForEach-Object { $_.name })
foreach ($rule in @("TitanMDM API", "TitanMDM SignalR", "TitanMDM SPA")) {
    if ($rules -notcontains $rule) { throw "web.config no contiene la regla '$rule'." }
}

Write-Host "[OK] Paquete completo en $OutputDir" -ForegroundColor Green

if (-not $Install) {
    Write-Host "`nPara instalar en el servidor (como Administrador):"
    Write-Host "  .\deploy\Install-TitanMDM.ps1 -PackageDir `"$OutputDir`" -HostName <host> -SecretsFile <archivo de secretos>"
    return
}

# ------------------------------------------------------------
# 3. INSTALACION
# ------------------------------------------------------------

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "-Install requiere una consola de PowerShell como Administrador."
}

$installArgs = @{
    PackageDir = $OutputDir
    InstallRoot = $InstallRoot
    SiteName = $SiteName
    ServiceName = $ServiceName
    PonchesMode = $PonchesMode
}
if ($HostName) { $installArgs.HostName = $HostName }
if ($PublicUrl) { $installArgs.PublicUrl = $PublicUrl }
$installArgs.HttpPort = $HttpPort
$installArgs.ApiPort = $ApiPort
$installArgs.PonchesPort = $PonchesPort
if ($CertificateThumbprint) { $installArgs.CertificateThumbprint = $CertificateThumbprint }
if ($SecretsFile) { $installArgs.SecretsFile = $SecretsFile }
if ($PythonExe) { $installArgs.PythonExe = $PythonExe }

& (Join-Path $OutputDir "deploy\Install-TitanMDM.ps1") @installArgs

# ------------------------------------------------------------
# 4. PERMISOS
# ------------------------------------------------------------

Write-Host "`n=== Verificando permisos ===" -ForegroundColor Cyan

$serviceAccount = "NT SERVICE\$ServiceName"
$serviceWmi = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
if ($serviceWmi -and $serviceWmi.StartName -and $serviceWmi.StartName -notmatch "^LocalSystem$") {
    $serviceAccount = $serviceWmi.StartName
}

$dataRoot = Join-Path $env:ProgramData "TitanMDM"

function Test-Access {
    param([string]$Path, [string]$Identity, [Security.AccessControl.FileSystemRights]$Rights)

    if (-not (Test-Path $Path)) {
        return "FALTA"
    }

    $acl = Get-Acl $Path
    $granted = $acl.Access | Where-Object {
        $_.AccessControlType -eq "Allow" -and
        $_.IdentityReference.Value -ieq $Identity -and
        (($_.FileSystemRights -band $Rights) -eq $Rights)
    }

    if ($granted) { "OK" } else { "SIN PERMISO" }
}

$read = [Security.AccessControl.FileSystemRights]::ReadAndExecute
$modify = [Security.AccessControl.FileSystemRights]::Modify

$checks = @(
    @("IIS AppPool\$SiteName", (Join-Path $InstallRoot "web"), $read, "lectura"),
    @($serviceAccount, (Join-Path $InstallRoot "api"), $read, "lectura"),
    @($serviceAccount, (Join-Path $InstallRoot "logs"), $modify, "escritura"),
    @($serviceAccount, $dataRoot, $modify, "escritura")
)

if ($PonchesMode -eq "Supervised") {
    $checks += , @($serviceAccount, (Join-Path $InstallRoot "ponches"), $read, "lectura")
    $checks += , @($serviceAccount, (Join-Path $InstallRoot "ponches\data"), $modify, "escritura")
}

$failed = $false
foreach ($check in $checks) {
    $result = Test-Access -Identity $check[0] -Path $check[1] -Rights $check[2]
    $color = if ($result -eq "OK") { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1} -> {2} ({3})" -f $result, $check[0], $check[1], $check[3]) -ForegroundColor $color
    if ($result -ne "OK") { $failed = $true }
}

if ($failed) {
    throw "Hay permisos pendientes: revise la salida anterior."
}

Write-Host "`n[OK] TitanMDM desplegado en IIS ($SiteName)." -ForegroundColor Green
