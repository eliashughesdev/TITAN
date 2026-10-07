param(
    [string]$ServerUrl = "",
    [string]$EnrollmentToken = "",
    [string]$ConfigPath = "",
    [string]$PackageUrl = "",
    [string]$ExpectedPackageSha256 = "",
    [switch]$Silent,
    [switch]$ForceReinstall
)

$ErrorActionPreference = "Stop"

# ============================================================
# TITANMDM WINDOWS AGENT INSTALLER
# Compatible con Windows PowerShell 5.1
# ============================================================

$ServiceName = "TitanMDMWindowsAgent"
$ServiceDisplayName = "TitanMDM Windows Agent"

$InstallRoot = Join-Path $env:ProgramFiles "TitanMDM"
$AgentInstallPath = Join-Path $InstallRoot "Agent"
$RemoteHostInstallPath = Join-Path $InstallRoot "RemoteHost"

$ProgramDataRoot = Join-Path $env:ProgramData "TitanMDM"
$SettingsPath = Join-Path $ProgramDataRoot "agentsettings.json"
$IdentityPath = Join-Path $ProgramDataRoot "device.json"

$LogDirectory = Join-Path $ProgramDataRoot "logs"
$LogPath = Join-Path $LogDirectory "installer.log"

$TempRoot = Join-Path $ProgramDataRoot "Temp"
$ZipPath = Join-Path $TempRoot "TitanMDM-WindowsAgent.zip"
$ExtractPath = Join-Path $TempRoot "Package"

# ============================================================
# LOGGING
# ============================================================

function Write-TitanLog {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )

    try {
        if (!(Test-Path $LogDirectory)) {
            New-Item `
                -ItemType Directory `
                -Path $LogDirectory `
                -Force |
            Out-Null
        }

        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $line = "[$timestamp][$Level] $Message"

        if (!$Silent) {
            Write-Host $line
        }

        Add-Content `
            -Path $LogPath `
            -Value $line `
            -Encoding UTF8
    }
    catch {
        if (!$Silent) {
            Write-Host "[$Level] $Message"
        }
    }
}

# ============================================================
# ADMIN / UAC
# ============================================================

function Test-IsAdministrator {
    $identity =
        [Security.Principal.WindowsIdentity]::GetCurrent()

    $principal =
        New-Object `
            Security.Principal.WindowsPrincipal($identity)

    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )
}

function Restart-Elevated {
    if (Test-IsAdministrator) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($PSCommandPath)) {
        throw "No fue posible determinar la ruta del instalador."
    }

    $arguments = @(
        "-NoLogo",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "`"$PSCommandPath`""
    )

    if (![string]::IsNullOrWhiteSpace($ConfigPath)) {
        $arguments += @(
            "-ConfigPath",
            "`"$ConfigPath`""
        )
    }

    if (![string]::IsNullOrWhiteSpace($ServerUrl)) {
        $arguments += @(
            "-ServerUrl",
            "`"$ServerUrl`""
        )
    }

    if (![string]::IsNullOrWhiteSpace($EnrollmentToken)) {
        $arguments += @(
            "-EnrollmentToken",
            "`"$EnrollmentToken`""
        )
    }

    if (![string]::IsNullOrWhiteSpace($PackageUrl)) {
        $arguments += @(
            "-PackageUrl",
            "`"$PackageUrl`""
        )
    }

    if (![string]::IsNullOrWhiteSpace($ExpectedPackageSha256)) {
        $arguments += @(
            "-ExpectedPackageSha256",
            "`"$ExpectedPackageSha256`""
        )
    }

    if ($Silent) {
        $arguments += "-Silent"
    }

    if ($ForceReinstall) {
        $arguments += "-ForceReinstall"
    }

    Start-Process `
        -FilePath "powershell.exe" `
        -ArgumentList $arguments `
        -Verb RunAs `
        -Wait

    exit $LASTEXITCODE
}

# ============================================================
# CONFIG.JSON
# ============================================================

function Resolve-ConfigPath {
    if (![string]::IsNullOrWhiteSpace($ConfigPath)) {
        return $ConfigPath
    }

    $localConfig =
        Join-Path `
            $PSScriptRoot `
            "config.json"

    if (Test-Path $localConfig) {
        return $localConfig
    }

    return ""
}

function Load-Configuration {
    $resolvedConfig = Resolve-ConfigPath

    if ([string]::IsNullOrWhiteSpace($resolvedConfig)) {
        return
    }

    if (!(Test-Path $resolvedConfig)) {
        throw "No se encontró el archivo de configuración: $resolvedConfig"
    }

    Write-TitanLog "Leyendo configuración desde $resolvedConfig"

    $config =
        Get-Content `
            -Path $resolvedConfig `
            -Raw `
            -Encoding UTF8 |
        ConvertFrom-Json

    if (
        [string]::IsNullOrWhiteSpace($ServerUrl) -and
        $null -ne $config.serverUrl
    ) {
        $script:ServerUrl = [string]$config.serverUrl
    }

    if (
        [string]::IsNullOrWhiteSpace($EnrollmentToken) -and
        $null -ne $config.enrollmentToken
    ) {
        $script:EnrollmentToken =
            [string]$config.enrollmentToken
    }

    if (
        [string]::IsNullOrWhiteSpace($PackageUrl) -and
        $null -ne $config.packageUrl
    ) {
        $script:PackageUrl =
            [string]$config.packageUrl
    }

    if (
        [string]::IsNullOrWhiteSpace($ExpectedPackageSha256) -and
        $null -ne $config.packageSha256
    ) {
        $script:ExpectedPackageSha256 =
            [string]$config.packageSha256
    }
}

function Normalize-ServerUrl {
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "ServerUrl es obligatorio."
    }

    $normalized =
        $Value.Trim().TrimEnd("/")

    $uri = $null

    $validUri =
        [Uri]::TryCreate(
            $normalized,
            [UriKind]::Absolute,
            [ref]$uri
        )

    if (!$validUri) {
        throw "ServerUrl no es válido: $Value"
    }

    if (
        $uri.Scheme -ne "http" -and
        $uri.Scheme -ne "https"
    ) {
        throw "ServerUrl debe utilizar HTTP o HTTPS."
    }

    return $normalized
}

function Validate-Configuration {
    $script:ServerUrl =
        Normalize-ServerUrl `
            $ServerUrl

    if ([string]::IsNullOrWhiteSpace($EnrollmentToken)) {
        throw "EnrollmentToken es obligatorio."
    }

    if ([string]::IsNullOrWhiteSpace($PackageUrl)) {
        $script:PackageUrl =
            "$ServerUrl/api/enrollment/windows/package"
    }
}

# ============================================================
# PREFLIGHT
# ============================================================

function Test-TitanServer {
    Write-TitanLog "Comprobando conexión con TitanMDM Server."

    try {
        $health =
            Invoke-RestMethod `
                -Uri "$ServerUrl/api/health" `
                -Method Get `
                -TimeoutSec 20

        if ($health.status -ne "Healthy") {
            throw "TitanMDM API no reporta estado Healthy."
        }

        Write-TitanLog "TitanMDM API disponible."
    }
    catch {
        throw "No fue posible conectar con $ServerUrl. $($_.Exception.Message)"
    }
}

function Write-SecurityReadiness {
    Write-TitanLog "Ejecutando diagnóstico de seguridad del endpoint."

    try {
        $computer =
            Get-CimInstance `
                Win32_ComputerSystem `
                -ErrorAction Stop

        Write-TitanLog "Equipo=$($computer.Name)"
        Write-TitanLog "Dominio=$($computer.Domain)"
        Write-TitanLog "DomainJoined=$($computer.PartOfDomain)"
    }
    catch {
        Write-TitanLog `
            "No fue posible consultar información del dominio." `
            "WARN"
    }

    try {
        $defender =
            Get-MpComputerStatus `
                -ErrorAction Stop

        Write-TitanLog `
            "Defender AntivirusEnabled=$($defender.AntivirusEnabled)"

        Write-TitanLog `
            "Defender RealTimeProtectionEnabled=$($defender.RealTimeProtectionEnabled)"
    }
    catch {
        Write-TitanLog `
            "Microsoft Defender no está disponible o su consulta está restringida." `
            "WARN"
    }

    try {
        $appLocker =
            Get-AppLockerPolicy `
                -Effective `
                -ErrorAction Stop

        if ($null -ne $appLocker) {
            Write-TitanLog "Política AppLocker efectiva detectada."
        }
    }
    catch {
        Write-TitanLog `
            "AppLocker no está habilitado o no fue posible consultarlo."
    }
}

# ============================================================
# EXISTING INSTALLATION
# ============================================================

function Test-ExistingInstallation {
    $service =
        Get-Service `
            -Name $ServiceName `
            -ErrorAction SilentlyContinue

    $agentExe =
        Join-Path `
            $AgentInstallPath `
            "TitanMDM.WindowsAgent.exe"

    if (
        ($null -ne $service) -and
        (Test-Path $agentExe) -and
        (Test-Path $IdentityPath)
    ) {
        return $true
    }

    return $false
}

function Stop-ExistingInstallation {
    $service =
        Get-Service `
            -Name $ServiceName `
            -ErrorAction SilentlyContinue

    if ($null -eq $service) {
        return
    }

    Write-TitanLog "Instalación TitanMDM existente detectada."

    if ($service.Status -ne "Stopped") {
        try {
            Stop-Service `
                -Name $ServiceName `
                -Force `
                -ErrorAction Stop

            $service.WaitForStatus(
                "Stopped",
                [TimeSpan]::FromSeconds(20)
            )
        }
        catch {
            Write-TitanLog `
                "No fue posible detener completamente el servicio." `
                "WARN"
        }
    }

    sc.exe delete $ServiceName |
        Out-Null

    Start-Sleep -Seconds 2
}

# ============================================================
# DOWNLOAD PACKAGE
# ============================================================

function Download-Package {
    if (Test-Path $TempRoot) {
        Remove-Item `
            $TempRoot `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }

    New-Item `
        -ItemType Directory `
        -Path $TempRoot `
        -Force |
    Out-Null

    Write-TitanLog "Descargando paquete desde $PackageUrl"

    Invoke-WebRequest `
        -Uri $PackageUrl `
        -OutFile $ZipPath `
        -UseBasicParsing `
        -TimeoutSec 300

    if (!(Test-Path $ZipPath)) {
        throw "No fue posible descargar TitanMDM Windows Agent."
    }

    $fileInfo =
        Get-Item `
            $ZipPath

    if ($fileInfo.Length -le 0) {
        throw "El paquete descargado está vacío."
    }

    Write-TitanLog `
        "Paquete descargado. Bytes=$($fileInfo.Length)"

    if (![string]::IsNullOrWhiteSpace($ExpectedPackageSha256)) {
        $actualHash =
            (
                Get-FileHash `
                    -Path $ZipPath `
                    -Algorithm SHA256
            ).Hash

        if (
            $actualHash.ToUpperInvariant() -ne
            $ExpectedPackageSha256.ToUpperInvariant()
        ) {
            throw "El SHA-256 del paquete descargado no coincide."
        }

        Write-TitanLog "SHA256 validado: $actualHash"
    }
}

function Expand-Package {
    if (Test-Path $ExtractPath) {
        Remove-Item `
            $ExtractPath `
            -Recurse `
            -Force
    }

    Write-TitanLog "Extrayendo paquete TitanMDM."

    Expand-Archive `
        -Path $ZipPath `
        -DestinationPath $ExtractPath `
        -Force

    $agentSource =
        Join-Path `
            $ExtractPath `
            "Agent"

    $remoteSource =
        Join-Path `
            $ExtractPath `
            "RemoteHost"

    if (!(Test-Path $agentSource)) {
        throw "El paquete no contiene la carpeta Agent."
    }

    if (!(Test-Path $remoteSource)) {
        throw "El paquete no contiene la carpeta RemoteHost."
    }
}

# ============================================================
# INSTALL BINARIES
# ============================================================

function Install-Binaries {
    $agentSource =
        Join-Path `
            $ExtractPath `
            "Agent"

    $remoteSource =
        Join-Path `
            $ExtractPath `
            "RemoteHost"

    New-Item `
        -ItemType Directory `
        -Path $AgentInstallPath `
        -Force |
    Out-Null

    New-Item `
        -ItemType Directory `
        -Path $RemoteHostInstallPath `
        -Force |
    Out-Null

    Write-TitanLog "Instalando Windows Agent."

    Copy-Item `
        (Join-Path $agentSource "*") `
        $AgentInstallPath `
        -Recurse `
        -Force

    Write-TitanLog "Instalando RemoteHost."

    Copy-Item `
        (Join-Path $remoteSource "*") `
        $RemoteHostInstallPath `
        -Recurse `
        -Force

    $agentExe =
        Join-Path `
            $AgentInstallPath `
            "TitanMDM.WindowsAgent.exe"

    $remoteExe =
        Join-Path `
            $RemoteHostInstallPath `
            "TitanMDM.RemoteHost.exe"

    if (!(Test-Path $agentExe)) {
        throw "TitanMDM.WindowsAgent.exe no fue instalado."
    }

    if (!(Test-Path $remoteExe)) {
        throw "TitanMDM.RemoteHost.exe no fue instalado."
    }

    Write-TitanLog "Agent y RemoteHost instalados."
}

# ============================================================
# AGENT CONFIGURATION
# ============================================================

function Write-AgentSettings {
    New-Item `
        -ItemType Directory `
        -Path $ProgramDataRoot `
        -Force |
    Out-Null

    $settings =
        [ordered]@{
            serverUrl = $ServerUrl
            enrollmentToken = $EnrollmentToken
            heartbeatIntervalSeconds = 60
            commandPollingIntervalSeconds = 10
            requestTimeoutSeconds = 30
        }

    $json =
        $settings |
        ConvertTo-Json `
            -Depth 5

    [System.IO.File]::WriteAllText(
        $SettingsPath,
        $json,
        (New-Object System.Text.UTF8Encoding($false))
    )

    Write-TitanLog "Configuración creada: $SettingsPath"
}

# ============================================================
# WINDOWS SERVICE
# ============================================================

function Install-WindowsService {
    $agentExe =
        Join-Path `
            $AgentInstallPath `
            "TitanMDM.WindowsAgent.exe"

    $quotedBinary =
        "`"$agentExe`""

    Write-TitanLog "Creando servicio Windows."

    sc.exe create `
        $ServiceName `
        binPath= $quotedBinary `
        start= auto `
        obj= LocalSystem `
        DisplayName= $ServiceDisplayName |
    Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "No fue posible crear $ServiceName. Código=$LASTEXITCODE"
    }

    sc.exe description `
        $ServiceName `
        "TitanMDM Enterprise Windows Management Agent" |
    Out-Null

    sc.exe failure `
        $ServiceName `
        reset= 86400 `
        actions= restart/5000/restart/15000/restart/30000 |
    Out-Null

    sc.exe failureflag `
        $ServiceName `
        1 |
    Out-Null

    Write-TitanLog "Servicio Windows creado."
}

function Start-TitanAgent {
    Write-TitanLog "Iniciando TitanMDM Windows Agent."

    Start-Service `
        -Name $ServiceName `
        -ErrorAction Stop

    $service =
        Get-Service `
            -Name $ServiceName

    $service.WaitForStatus(
        "Running",
        [TimeSpan]::FromSeconds(30)
    )

    $service.Refresh()

    if ($service.Status -ne "Running") {
        throw "TitanMDM Windows Agent no quedó Running."
    }

    Write-TitanLog "TitanMDMWindowsAgent está Running."
}

# ============================================================
# ENROLLMENT
# ============================================================

function Wait-Enrollment {
    Write-TitanLog "Esperando enrolamiento del dispositivo."

    $deadline =
        (Get-Date).AddSeconds(120)

    while ((Get-Date) -lt $deadline) {
        if (Test-Path $IdentityPath) {
            Write-TitanLog "Dispositivo enrolado correctamente."
            return $true
        }

        Start-Sleep `
            -Seconds 3
    }

    Write-TitanLog `
        "El servicio está activo pero device.json todavía no apareció." `
        "WARN"

    return $false
}

# ============================================================
# CLEANUP
# ============================================================

function Cleanup-Installation {
    try {
        if (Test-Path $TempRoot) {
            Remove-Item `
                $TempRoot `
                -Recurse `
                -Force `
                -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-TitanLog `
            "No fue posible eliminar todos los archivos temporales." `
            "WARN"
    }
}

# ============================================================
# MAIN
# ============================================================

try {
    Restart-Elevated

    Load-Configuration

    Validate-Configuration

    Write-TitanLog "============================================="
    Write-TitanLog "TitanMDM Windows Agent Installer"
    Write-TitanLog "============================================="
    Write-TitanLog "Servidor=$ServerUrl"

    Write-SecurityReadiness

    if (
        (Test-ExistingInstallation) -and
        (!$ForceReinstall)
    ) {
        Write-TitanLog "TitanMDM ya está instalado y enrolado."
        exit 0
    }

    Test-TitanServer

    Stop-ExistingInstallation

    Download-Package

    Expand-Package

    Install-Binaries

    Write-AgentSettings

    Install-WindowsService

    Start-TitanAgent

    $enrolled =
        Wait-Enrollment

    Cleanup-Installation

    if ($enrolled) {
        Write-TitanLog "Instalación TitanMDM completada correctamente."
    }
    else {
        Write-TitanLog `
            "Instalación completada, pero el enrolamiento continúa pendiente." `
            "WARN"
    }

    if (!$Silent) {
        Write-Host ""
        Write-Host "=============================================" -ForegroundColor Green
        Write-Host " TITANMDM INSTALADO" -ForegroundColor Green
        Write-Host "=============================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Servidor : $ServerUrl"
        Write-Host "Servicio : $ServiceName"
        Write-Host "Agent    : $AgentInstallPath"
        Write-Host "Remote   : $RemoteHostInstallPath"
        Write-Host "Log      : $LogPath"
        Write-Host ""
    }

    exit 0
}
catch {
    $message =
        $_.Exception.Message

    try {
        Write-TitanLog `
            $message `
            "ERROR"
    }
    catch {
        Write-Host `
            $message `
            -ForegroundColor Red
    }

    exit 1
}