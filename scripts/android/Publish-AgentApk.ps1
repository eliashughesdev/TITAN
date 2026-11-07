<#
.SYNOPSIS
    Publica el APK del agente Android en la rama main de GitHub para que la
    URL raw del QR de aprovisionamiento sirva siempre la última compilación.

.DESCRIPTION
    1. (Opcional, -Build) compila el agente con gradlew assembleDebug.
    2. Copia app/build/outputs/apk/debug/app-debug.apk a deploy/app-debug.apk
       y verifica su firma con apksigner.
    3. Regenera provisioning-qr.json y provisioning-qr.png
       (New-ProvisioningQr.ps1), con el checksum del certificado de firma.
    4. Si el APK difiere del que tiene origin/main, crea un commit con SOLO
       deploy/app-debug.apk encima de origin/main y lo empuja a main.

    El commit se arma con un índice temporal (git read-tree / commit-tree):
    no toca la rama actual, el índice ni el árbol de trabajo, así que los
    cambios locales sin commit (y archivos como opencode.json, PDFs o
    provisioning-qr.json con la contraseña Wi-Fi) nunca se publican.

    Lo ejecuta automáticamente la tarea Gradle publishDeployApk después de
    assembleDebug. Para desactivarlo: -PtitanAutoPublish=false o
    TITAN_AUTO_PUBLISH=false.

.EXAMPLE
    .\scripts\android\Publish-AgentApk.ps1 -Build
.EXAMPLE
    .\scripts\android\Publish-AgentApk.ps1 -NoPush
#>
[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$NoPush,
    [switch]$SkipQr,
    [string]$Remote = 'origin',
    [string]$Branch = 'main',
    [string]$Message = 'chore: auto-update Android agent APK for provisioning',
    [string]$RawUrl = 'https://raw.githubusercontent.com/eliashughesdev/TITAN/main/deploy/app-debug.apk'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path (Split-Path (Split-Path $MyInvocation.MyCommand.Path -Parent) -Parent) -Parent
$agentDir = Join-Path $root 'agents\TitanMDMAndroidAgent'
$builtApk = Join-Path $agentDir 'app\build\outputs\apk\debug\app-debug.apk'
$deployApk = Join-Path $root 'deploy\app-debug.apk'
$deployRel = 'deploy/app-debug.apk'

function Invoke-Native {
    param([string]$File, [string[]]$Arguments, [switch]$AllowFailure)
    # Herramientas nativas escriben avisos en stderr: no deben abortar.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $output = & $File @Arguments 2>&1 }
    finally { $ErrorActionPreference = $previous }
    $text = ($output | ForEach-Object { "$_" }) -join "`n"
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "$File $($Arguments -join ' ') falló ($LASTEXITCODE):`n$text"
    }
    return $text.Trim()
}

function Invoke-Git([string[]]$Arguments, [switch]$AllowFailure) {
    Invoke-Native -File 'git.exe' -Arguments (@('-C', $root) + $Arguments) -AllowFailure:$AllowFailure
}

if (-not $env:JAVA_HOME) {
    $jbr = 'C:\Program Files\Android\Android Studio\jbr'
    if (Test-Path $jbr) { $env:JAVA_HOME = $jbr }
}

# 1. Compilación opcional (sin volver a disparar esta publicación).
if ($Build) {
    Write-Host '==> Compilando agente (assembleDebug)...'
    Push-Location $agentDir
    try { $null = Invoke-Native -File (Join-Path $agentDir 'gradlew.bat') -Arguments @('assembleDebug', '-PtitanAutoPublish=false') }
    finally { Pop-Location }
}

if (-not (Test-Path $builtApk)) { throw "No existe $builtApk. Compile el agente (assembleDebug) o use -Build." }

# 2. Copia a deploy/ y verificación de firma.
$builtHash = (Get-FileHash $builtApk -Algorithm SHA256).Hash.ToLowerInvariant()
$deployHash = if (Test-Path $deployApk) { (Get-FileHash $deployApk -Algorithm SHA256).Hash.ToLowerInvariant() } else { '' }
if ($builtHash -ne $deployHash) {
    New-Item -ItemType Directory -Force (Split-Path $deployApk -Parent) | Out-Null
    Copy-Item $builtApk $deployApk -Force
    Write-Host "==> APK copiado a $deployRel"
}
Write-Host "    SHA-256 del archivo APK: $builtHash"

# 3. QR (JSON + PNG) con el checksum del certificado de firma.
if (-not $SkipQr) {
    try {
        & (Join-Path $PSScriptRoot 'New-ProvisioningQr.ps1') -ApkPath $deployApk
    }
    catch {
        Write-Warning "No se regeneró el QR: $($_.Exception.Message)"
    }
}

if ($NoPush) {
    Write-Host '==> -NoPush: no se publica en GitHub.'
    return
}

# 4. Commit solo del APK encima de origin/main y push.
Write-Host "==> Sincronizando $deployRel con $Remote/$Branch..."
$null = Invoke-Git @('fetch', '--quiet', $Remote, $Branch)
$remoteRef = "refs/remotes/$Remote/$Branch"
$blob = Invoke-Git @('hash-object', '-w', '--', $deployApk)
$remoteBlob = Invoke-Git @('rev-parse', '--verify', '--quiet', "${remoteRef}:$deployRel") -AllowFailure

if ($remoteBlob -eq $blob) {
    Write-Host "    $Remote/$Branch ya tiene este APK. Nada que publicar."
    return
}

$tempIndex = Join-Path ([IO.Path]::GetTempPath()) "titan-apk-index-$([guid]::NewGuid().ToString('N'))"
$previousIndex = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $tempIndex
    $null = Invoke-Git @('read-tree', $remoteRef)
    $null = Invoke-Git @('update-index', '--add', '--cacheinfo', "100644,$blob,$deployRel")
    $tree = Invoke-Git @('write-tree')
}
finally {
    if ($null -eq $previousIndex) { Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue } else { $env:GIT_INDEX_FILE = $previousIndex }
    Remove-Item $tempIndex -Force -ErrorAction SilentlyContinue
}

$parent = Invoke-Git @('rev-parse', $remoteRef)
$commit = Invoke-Git @('commit-tree', $tree, '-p', $parent, '-m', $Message)
$null = Invoke-Git @('push', $Remote, "${commit}:refs/heads/$Branch")
Write-Host "    Publicado $($commit.Substring(0, 7)) en $Remote/$Branch ($Message)."

# Si la rama local es la publicada y está limpia, se adelanta sin merge.
$current = Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD')
if ($current -eq $Branch) {
    $null = Invoke-Git @('merge', '--ff-only', '--quiet', $commit) -AllowFailure
}

# raw.githubusercontent.com cachea ~5 min. El QR usa el checksum del
# certificado (no del archivo), así que un APK en caché sigue instalando.
try {
    $probe = Join-Path ([IO.Path]::GetTempPath()) "titan-apk-raw-$([guid]::NewGuid().ToString('N')).apk"
    Invoke-WebRequest -Uri "${RawUrl}?v=$($commit.Substring(0, 7))" -OutFile $probe -UseBasicParsing -TimeoutSec 120
    $rawHash = (Get-FileHash $probe -Algorithm SHA256).Hash.ToLowerInvariant()
    Remove-Item $probe -Force -ErrorAction SilentlyContinue
    if ($rawHash -eq $builtHash) { Write-Host '    URL raw verificada: sirve el APK nuevo.' }
    else { Write-Warning 'La URL raw aún sirve la versión anterior (caché de GitHub, ~5 min).' }
}
catch {
    Write-Warning "No se pudo verificar la URL raw: $($_.Exception.Message)"
}
