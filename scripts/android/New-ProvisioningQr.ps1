<#
.SYNOPSIS
    Genera provisioning-qr.json para el QR de aprovisionamiento Device Owner
    (pantalla de bienvenida de Android: tocar 6 veces).

.DESCRIPTION
    Android rechaza el QR ("Código QR incorrecto" / "No se puede configurar")
    cuando el JSON no es válido o falta el checksum del APK. Este script:
      - calcula PROVISIONING_DEVICE_ADMIN_SIGNATURE_CHECKSUM (SHA-256 del
        certificado de firma, base64 URL-safe sin relleno) con apksigner;
      - escribe el JSON en una sola línea, ASCII, sin BOM ni comillas curvas;
      - apunta la descarga al APK raw de GitHub.

    Los valores no indicados (Wi-Fi, servidor, cuenta) se toman del
    provisioning-qr.json existente. El archivo está en .gitignore porque
    contiene la contraseña Wi-Fi.

.EXAMPLE
    .\scripts\android\New-ProvisioningQr.ps1 -ServerUrl https://mdm.cisa.local
#>
[CmdletBinding()]
param(
    [string]$ApkPath,
    [string]$OutputPath,
    [string]$DownloadUrl = 'https://raw.githubusercontent.com/eliashughesdev/TITAN/main/deploy/app-debug.apk',
    [string]$ServerUrl,
    [string]$WifiSsid,
    [string]$WifiPassword,
    [string]$WifiSecurityType = 'WPA',
    [string]$AccountEmail,
    [string]$Locale = 'es_DO'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path (Split-Path (Split-Path $MyInvocation.MyCommand.Path -Parent) -Parent) -Parent
if (-not $ApkPath) { $ApkPath = Join-Path $root 'deploy/app-debug.apk' }
if (-not $OutputPath) { $OutputPath = Join-Path $root 'provisioning-qr.json' }

$prefix = 'android.app.extra.PROVISIONING_'

function Get-ApkSigner {
    $sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
    $tool = Get-ChildItem (Join-Path $sdk 'build-tools') -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [version]($_.Name -replace '[^0-9.].*$', '') } -Descending |
        ForEach-Object { Join-Path $_.FullName 'apksigner.bat' } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
    if (-not $tool) { throw "No se encontró apksigner en $sdk\build-tools." }
    if (-not $env:JAVA_HOME) {
        $jbr = 'C:\Program Files\Android\Android Studio\jbr'
        if (Test-Path $jbr) { $env:JAVA_HOME = $jbr }
    }
    return $tool
}

function Get-SignatureChecksum([string]$apk) {
    $signer = Get-ApkSigner
    # apksigner escribe avisos de la JVM en stderr: no deben abortar el script.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & $signer verify --print-certs $apk 2>$null }
    finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw "apksigner no pudo verificar $apk." }
    $line = $output | Where-Object { $_ -match 'Signer #?1?: certificate SHA-256 digest: ([0-9a-f]{64})' } | Select-Object -First 1
    if (-not $line) {
        $line = $output | Where-Object { $_ -match 'certificate SHA-256 digest: ([0-9a-f]{64})' } | Select-Object -First 1
    }
    if (-not $line -or $line -notmatch '([0-9a-f]{64})') { throw 'No se obtuvo el SHA-256 del certificado de firma.' }
    $hex = $Matches[1]
    $bytes = [byte[]]::new(32)
    for ($i = 0; $i -lt 32; $i++) { $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16) }
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-Existing([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    $raw = [IO.File]::ReadAllText((Resolve-Path $path))
    # Normaliza comillas curvas que rompen el JSON.
    $raw = $raw.Replace([char]0x201C, '"').Replace([char]0x201D, '"').Replace([char]0x2018, "'").Replace([char]0x2019, "'").TrimStart([char]0xFEFF)
    return $raw | ConvertFrom-Json
}

function Read-Value($existing, [string]$name) {
    if ($null -eq $existing) { return $null }
    $prop = $existing.PSObject.Properties[$name]
    if ($prop) { return [string]$prop.Value } else { return $null }
}

function Assert-Ascii([string]$name, [string]$value) {
    if ($value -match '[^\x20-\x7E]') { throw "$name contiene caracteres no ASCII: '$value'." }
}

$apk = (Resolve-Path $ApkPath).Path
$existing = Get-Existing $OutputPath
$existingExtras = if ($existing -and $existing.PSObject.Properties["${prefix}ADMIN_EXTRAS_BUNDLE"]) { $existing."${prefix}ADMIN_EXTRAS_BUNDLE" } else { $null }

if (-not $WifiSsid) { $WifiSsid = Read-Value $existing "${prefix}WIFI_SSID" }
if (-not $WifiPassword) { $WifiPassword = Read-Value $existing "${prefix}WIFI_PASSWORD" }
if (-not $ServerUrl) { $ServerUrl = Read-Value $existingExtras 'server_url' }
if (-not $AccountEmail) { $AccountEmail = Read-Value $existingExtras 'account_email' }

if (-not $ServerUrl) { throw 'Indique -ServerUrl (URL del servidor TitanMDM).' }
if ($ServerUrl -notmatch '^https?://[^\s/]+') { throw "ServerUrl no es una URL válida: $ServerUrl" }
if ($ServerUrl -match 'tu-servidor') { Write-Warning "ServerUrl parece un valor de ejemplo: $ServerUrl" }

$payload = [ordered]@{
    "${prefix}DEVICE_ADMIN_COMPONENT_NAME"       = 'com.titanmdm.agent/com.titanmdm.agent.admin.TitanDeviceAdminReceiver'
    "${prefix}DEVICE_ADMIN_PACKAGE_DOWNLOAD_LOCATION" = $DownloadUrl
    "${prefix}DEVICE_ADMIN_SIGNATURE_CHECKSUM"   = (Get-SignatureChecksum $apk)
    "${prefix}LEAVE_ALL_SYSTEM_APPS_ENABLED"     = $true
    "${prefix}LOCALE"                            = $Locale
}
if ($WifiSsid) {
    $payload["${prefix}WIFI_SSID"] = $WifiSsid
    $payload["${prefix}WIFI_SECURITY_TYPE"] = $WifiSecurityType
    if ($WifiPassword) { $payload["${prefix}WIFI_PASSWORD"] = $WifiPassword }
}
$extras = [ordered]@{ server_url = $ServerUrl.TrimEnd('/') }
if ($AccountEmail) { $extras.account_email = $AccountEmail }
$payload["${prefix}ADMIN_EXTRAS_BUNDLE"] = $extras

foreach ($key in @($payload.Keys)) {
    if ($payload[$key] -is [string]) { Assert-Ascii $key $payload[$key] }
}

$json = $payload | ConvertTo-Json -Compress -Depth 4
# Validación: debe volver a parsear.
$null = $json | ConvertFrom-Json

[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
Write-Host "QR generado: $([IO.Path]::GetFullPath($OutputPath)) ($($json.Length) caracteres)"
Write-Host "Checksum de firma: $($payload["${prefix}DEVICE_ADMIN_SIGNATURE_CHECKSUM"])"
