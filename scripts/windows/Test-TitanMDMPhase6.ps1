param(
    [string]$RepositoryRoot = "C:\TitanMDM",

    [string]$ServerUrl = "http://localhost:8020",

    [switch]$SkipFrontend,

    [switch]$SkipPackage,

    [switch]$SkipEf
)

$ErrorActionPreference = "Stop"

$script:Passed = 0
$script:Failed = 0
$script:Warnings = 0

# ============================================================
# HELPERS
# ============================================================

function Write-Section {
    param(
        [string]$Title
    )

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}

function Add-Pass {
    param(
        [string]$Message
    )

    $script:Passed++

    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Add-Fail {
    param(
        [string]$Message
    )

    $script:Failed++

    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Add-Warning {
    param(
        [string]$Message
    )

    $script:Warnings++

    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Invoke-NativeCommand {
    param(
        [string]$Name,

        [scriptblock]$Command
    )

    try {
        & $Command

        if ($LASTEXITCODE -eq 0) {
            Add-Pass $Name
            return $true
        }

        Add-Fail "$Name returned exit code $LASTEXITCODE"
        return $false
    }
    catch {
        Add-Fail "$Name - $($_.Exception.Message)"
        return $false
    }
}

# ============================================================
# START
# ============================================================

Write-Section "TitanMDM Phase 6 Enterprise Validation"

if (-not (Test-Path $RepositoryRoot)) {
    Write-Host "[FATAL] Repository does not exist: $RepositoryRoot" -ForegroundColor Red
    exit 1
}

Set-Location $RepositoryRoot

Write-Host "Repository : $RepositoryRoot"
Write-Host "Server     : $ServerUrl"
Write-Host ""

# ============================================================
# 1. REQUIRED FILES
# ============================================================

Write-Section "1. Required Windows Agent files"

$RequiredFiles = @(

    "agents\windows\TitanMDM.WindowsAgent\TitanMDM.WindowsAgent.csproj",
    "agents\windows\TitanMDM.WindowsAgent\Program.cs",
    "agents\windows\TitanMDM.WindowsAgent\Worker.cs",

    "agents\windows\TitanMDM.WindowsAgent\Services\EnrollmentService.cs",
    "agents\windows\TitanMDM.WindowsAgent\Services\HeartbeatBackgroundService.cs",

    "agents\windows\TitanMDM.WindowsAgent\Services\AgentLifecycleCoordinator.cs",
    "agents\windows\TitanMDM.WindowsAgent\Services\AgentLifecycleState.cs",

    "agents\windows\TitanMDM.WindowsAgent\Services\AgentRetryPolicy.cs",

    "agents\windows\TitanMDM.WindowsAgent\Services\AgentDiagnosticsService.cs",
    "agents\windows\TitanMDM.WindowsAgent\Services\AgentDiagnosticsBackgroundService.cs",

    "agents\windows\TitanMDM.WindowsAgent\Services\AgentSelfHealingService.cs",
    "agents\windows\TitanMDM.WindowsAgent\Services\AgentSelfHealingBackgroundService.cs",

    "agents\windows\TitanMDM.WindowsAgent\Services\RemoteSupportBackgroundService.cs",
    "agents\windows\TitanMDM.WindowsAgent\Services\RemoteDesktopHostLauncher.cs",

    "agents\windows\TitanMDM.WindowsAgent\Execution\WindowsActionExecutor.cs",

    "agents\windows\TitanMDM.RemoteHost\TitanMDM.RemoteHost.csproj",

    "scripts\windows\Build-TitanMDMAgentPackage.ps1",
    "scripts\windows\Build-TitanMDMInstaller.ps1",
    "scripts\windows\Install-TitanMDMAgent.ps1",
    "scripts\windows\Install-TitanMDMAgent-GPO.ps1",
    "scripts\windows\Uninstall-TitanMDMAgent.ps1",
    "scripts\windows\Setup-TitanMDM.iss",
    "scripts\windows\install-gpo.bat"
)

foreach ($RelativePath in $RequiredFiles) {

    $AbsolutePath = Join-Path $RepositoryRoot $RelativePath

    if (Test-Path $AbsolutePath) {
        Add-Pass "Exists: $RelativePath"
    }
    else {
        Add-Fail "Missing: $RelativePath"
    }
}

# ============================================================
# 2. WINDOWS AGENT BUILD
# ============================================================

Write-Section "2. Windows Agent build"

$AgentProject = Join-Path `
    $RepositoryRoot `
    "agents\windows\TitanMDM.WindowsAgent\TitanMDM.WindowsAgent.csproj"

Invoke-NativeCommand `
    -Name "Windows Agent Debug build" `
    -Command {

        dotnet build `
            $AgentProject `
            -c Debug
    }

# ============================================================
# 3. GLOBAL BUILD
# ============================================================

Write-Section "3. Global solution build"

$Solution = Join-Path `
    $RepositoryRoot `
    "TitanMDM.slnx"

Invoke-NativeCommand `
    -Name "TitanMDM global build" `
    -Command {

        dotnet build `
            $Solution `
            -c Debug
    }

# ============================================================
# 4. TESTS
# ============================================================

Write-Section "4. Automated tests"

Invoke-NativeCommand `
    -Name "TitanMDM automated tests" `
    -Command {

        dotnet test `
            $Solution `
            -c Debug `
            --no-build
    }

# ============================================================
# 5. ENTITY FRAMEWORK
# ============================================================

Write-Section "5. Entity Framework model"

if ($SkipEf) {

    Add-Warning "EF validation skipped by parameter."
}
else {

    $InfrastructureProject = Join-Path `
        $RepositoryRoot `
        "src\backend\TitanMDM.Infrastructure\TitanMDM.Infrastructure.csproj"

    $ApiProject = Join-Path `
        $RepositoryRoot `
        "src\backend\TitanMDM.Api\TitanMDM.Api.csproj"

    try {

        dotnet ef migrations has-pending-model-changes `
            --project $InfrastructureProject `
            --startup-project $ApiProject

        if ($LASTEXITCODE -eq 0) {

            Add-Pass "EF model matches migrations."
        }
        else {

            Add-Fail "EF model has pending changes or design-time configuration failed."
        }
    }
    catch {

        Add-Fail "EF validation failed: $($_.Exception.Message)"
    }
}

# ============================================================
# 6. FRONTEND
# ============================================================

Write-Section "6. Frontend production build"

if ($SkipFrontend) {

    Add-Warning "Frontend build skipped by parameter."
}
else {

    $FrontendDirectory = Join-Path `
        $RepositoryRoot `
        "src\frontend\titanmdm-web"

    if (-not (Test-Path $FrontendDirectory)) {

        Add-Fail "Frontend directory does not exist."
    }
    else {

        Push-Location $FrontendDirectory

        try {

            npm run build

            if ($LASTEXITCODE -eq 0) {

                Add-Pass "Frontend production build."
            }
            else {

                Add-Fail "Frontend production build returned exit code $LASTEXITCODE."
            }
        }
        catch {

            Add-Fail "Frontend production build failed: $($_.Exception.Message)"
        }
        finally {

            Pop-Location
        }
    }
}

# ============================================================
# 7. WINDOWS DISTRIBUTION PACKAGE
# ============================================================

Write-Section "7. Windows Agent distribution package"

if ($SkipPackage) {

    Add-Warning "Package generation skipped by parameter."
}
else {

    $PackageBuilder = Join-Path `
        $RepositoryRoot `
        "scripts\windows\Build-TitanMDMAgentPackage.ps1"

    if (-not (Test-Path $PackageBuilder)) {

        Add-Fail "Package builder does not exist."
    }
    else {

        try {

            & $PackageBuilder

            if ($LASTEXITCODE -ne 0) {

                throw "Package builder returned exit code $LASTEXITCODE."
            }

            $PackagePath = Join-Path `
                $RepositoryRoot `
                "artifacts\windows-agent\TitanMDM-WindowsAgent-x64.zip"

            if (-not (Test-Path $PackagePath)) {

                throw "TitanMDM-WindowsAgent-x64.zip was not generated."
            }

            $PackageFile = Get-Item $PackagePath

            if ($PackageFile.Length -le 0) {

                throw "Generated package is empty."
            }

            $Hash = Get-FileHash `
                $PackagePath `
                -Algorithm SHA256

            Add-Pass "Windows Agent package generated."

            Write-Host ""
            Write-Host "Package : $($PackageFile.FullName)"
            Write-Host "Bytes   : $($PackageFile.Length)"
            Write-Host "SHA256  : $($Hash.Hash)"
        }
        catch {

            Add-Fail "Package generation failed: $($_.Exception.Message)"
        }
    }
}

# ============================================================
# 8. API HEALTH
# ============================================================

Write-Section "8. TitanMDM API health"

$NormalizedServerUrl = $ServerUrl.TrimEnd("/")

try {

    $Health = Invoke-RestMethod `
        -Uri "$NormalizedServerUrl/api/health/live" `
        -Method Get `
        -TimeoutSec 10

    if ($Health.status -eq "Alive") {

        Add-Pass "API health/live reports Alive."
    }
    else {

        Add-Warning "API responded with status: $($Health.status)"
    }
}
catch {

    Add-Warning "API health check unavailable: $($_.Exception.Message)"
}

# ============================================================
# 9. LOCAL WINDOWS SERVICE
# ============================================================

Write-Section "9. Local Windows Agent runtime"

$AgentService = Get-Service `
    -Name "TitanMDMWindowsAgent" `
    -ErrorAction SilentlyContinue

if ($null -eq $AgentService) {

    Add-Warning "TitanMDMWindowsAgent service is not installed on this computer."
}
elseif ($AgentService.Status -eq "Running") {

    Add-Pass "TitanMDMWindowsAgent service is Running."
}
else {

    Add-Warning "TitanMDMWindowsAgent service state is $($AgentService.Status)."
}

# ============================================================
# 10. PROGRAMDATA STATE
# ============================================================

Write-Section "10. Agent persistent state"

$TitanProgramData = Join-Path `
    $env:ProgramData `
    "TitanMDM"

$SettingsPath = Join-Path `
    $TitanProgramData `
    "agentsettings.json"

$IdentityPath = Join-Path `
    $TitanProgramData `
    "device.json"

if (Test-Path $SettingsPath) {

    Add-Pass "agentsettings.json exists."

    try {

        $Settings = Get-Content `
            $SettingsPath `
            -Raw |
            ConvertFrom-Json

        if (
            [string]::IsNullOrWhiteSpace(
                [string]$Settings.enrollmentToken
            )
        ) {

            Add-Pass "No residual enrollment token stored."
        }
        elseif (Test-Path $IdentityPath) {

            Add-Warning "device.json exists but enrollmentToken is still present."
        }
        else {

            Add-Warning "Enrollment token exists because device enrollment is not complete."
        }

        if (
            -not [string]::IsNullOrWhiteSpace(
                [string]$Settings.serverUrl
            )
        ) {

            Write-Host "Configured server: $($Settings.serverUrl)"
        }
    }
    catch {

        Add-Fail "agentsettings.json is invalid JSON."
    }
}
else {

    Add-Warning "agentsettings.json does not exist."
}

if (Test-Path $IdentityPath) {

    Add-Pass "device.json exists."

    try {

        $Identity = Get-Content `
            $IdentityPath `
            -Raw |
            ConvertFrom-Json

        if (
            -not [string]::IsNullOrWhiteSpace(
                [string]$Identity.deviceId
            )
        ) {

            Write-Host "DeviceId: $($Identity.deviceId)"
        }
    }
    catch {

        Add-Warning "device.json exists but cannot be displayed as plain JSON."
        Write-Host "This may be expected when identity data is protected."
    }
}
else {

    Add-Warning "device.json does not exist."
}

# ============================================================
# 11. AGENT DIAGNOSTICS
# ============================================================

Write-Section "11. Agent diagnostics"

$DiagnosticCandidates = @(

    (Join-Path $TitanProgramData "diagnostics\agent-health.json"),

    (Join-Path $TitanProgramData "agent-health.json")
)

$DiagnosticPath = $null

foreach ($Candidate in $DiagnosticCandidates) {

    if (Test-Path $Candidate) {

        $DiagnosticPath = $Candidate
        break
    }
}

if ($null -eq $DiagnosticPath) {

    Add-Warning "Agent diagnostic file was not found yet."
}
else {

    Add-Pass "Agent diagnostic file exists."

    try {

        $Diagnostics = Get-Content `
            $DiagnosticPath `
            -Raw |
            ConvertFrom-Json

        Write-Host ""
        Write-Host "Diagnostic path : $DiagnosticPath"

        if ($null -ne $Diagnostics.machineName) {
            Write-Host "Machine         : $($Diagnostics.machineName)"
        }

        if ($null -ne $Diagnostics.deviceId) {
            Write-Host "DeviceId        : $($Diagnostics.deviceId)"
        }

        if ($null -ne $Diagnostics.serverReachable) {
            Write-Host "Server reachable: $($Diagnostics.serverReachable)"
        }

        if ($null -ne $Diagnostics.identityReadable) {
            Write-Host "Identity        : $($Diagnostics.identityReadable)"
        }

        if ($null -ne $Diagnostics.remoteHostExists) {
            Write-Host "RemoteHost      : $($Diagnostics.remoteHostExists)"
        }
    }
    catch {

        Add-Warning "Diagnostic file exists but could not be parsed."
    }
}

# ============================================================
# 12. REMOTE HOST
# ============================================================

Write-Section "12. RemoteHost"

$RemoteHostCandidates = @(

    (Join-Path `
        $env:ProgramFiles `
        "TitanMDM\RemoteHost\TitanMDM.RemoteHost.exe"),

    (Join-Path `
        $RepositoryRoot `
        "artifacts\windows-agent\package\RemoteHost\TitanMDM.RemoteHost.exe")
)

$RemoteHostFound = $false

foreach ($RemoteHostPath in $RemoteHostCandidates) {

    if (Test-Path $RemoteHostPath) {

        $RemoteHostFound = $true

        Add-Pass "RemoteHost binary exists: $RemoteHostPath"

        break
    }
}

if (-not $RemoteHostFound) {

    Add-Warning "RemoteHost binary was not found."
}

Write-Host ""
Write-Host "RemoteHost execution is NOT required for Phase 6 implementation closure." -ForegroundColor Yellow
Write-Host "Corporate Defender/ASR currently blocks the unsigned binary." -ForegroundColor Yellow
Write-Host "Execution validation moves to code-signing and security testing." -ForegroundColor Yellow

# ============================================================
# 13. GPO DEPLOYMENT ASSETS
# ============================================================

Write-Section "13. GPO deployment assets"

$GpoFiles = @(

    "scripts\windows\Install-TitanMDMAgent-GPO.ps1",
    "scripts\windows\install-gpo.bat"
)

foreach ($RelativePath in $GpoFiles) {

    $AbsolutePath = Join-Path `
        $RepositoryRoot `
        $RelativePath

    if (Test-Path $AbsolutePath) {

        Add-Pass "GPO asset exists: $RelativePath"
    }
    else {

        Add-Fail "Missing GPO asset: $RelativePath"
    }
}

Write-Host ""
Write-Host "Production OU deployment remains a formal testing task." -ForegroundColor Yellow

# ============================================================
# FINAL RESULT
# ============================================================

Write-Section "FINAL RESULT"

Write-Host ""
Write-Host "PASS     : $script:Passed" -ForegroundColor Green
Write-Host "WARNINGS : $script:Warnings" -ForegroundColor Yellow
Write-Host "FAIL     : $script:Failed" -ForegroundColor Red
Write-Host ""

if ($script:Failed -gt 0) {

    Write-Host "PHASE 6: NOT READY FOR IMPLEMENTATION CLOSURE" -ForegroundColor Red

    exit 1
}

Write-Host "PHASE 6: IMPLEMENTATION READY FOR TESTING" -ForegroundColor Green

exit 0