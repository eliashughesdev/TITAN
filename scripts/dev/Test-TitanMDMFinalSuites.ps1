[CmdletBinding()]
param(
    [switch]$SkipIntegration,
    [switch]$SkipFrontend
)

$ErrorActionPreference = 'Stop'
$root = 'C:\TitanMDM'
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Repository directory not found: $root"
}
Set-Location -LiteralPath $root

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logDir = Join-Path $root "artifacts\qa\AUT1-$stamp"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$results = New-Object System.Collections.Generic.List[object]

function Invoke-Stage {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Directory,
        [Parameter(Mandatory=$true)][string]$Executable,
        [Parameter(Mandatory=$true)][string[]]$Arguments
    )

    $log = Join-Path $logDir ($Name + '.log')
    $stdOut = Join-Path $logDir ($Name + '.stdout.tmp')
    $stdErr = Join-Path $logDir ($Name + '.stderr.tmp')
    $start = Get-Date
    $code = -1
    Write-Host ('=== ' + $Name + ' ===') -ForegroundColor Cyan
    Push-Location -LiteralPath $Directory
    try {
        # Do not pipe native stderr into PowerShell error stream (PS 5.1).
        # Execute via a native child process and collect both streams in files.
        $command = if ($Executable -eq 'npm.cmd') { 'npm.cmd' } else { $Executable }
        if ($command -eq 'npm.cmd') {
            $commandLine = 'npm.cmd ' + ($Arguments -join ' ')
            $process = Start-Process -FilePath $env:ComSpec `
                -ArgumentList @('/d', '/s', '/c', ('"' + $commandLine + '"')) `
                -WorkingDirectory $Directory -Wait -PassThru -NoNewWindow `
                -RedirectStandardOutput $stdOut -RedirectStandardError $stdErr
        }
        else {
            $process = Start-Process -FilePath $command `
                -ArgumentList $Arguments -WorkingDirectory $Directory `
                -Wait -PassThru -NoNewWindow `
                -RedirectStandardOutput $stdOut -RedirectStandardError $stdErr
        }
        $code = [int]$process.ExitCode
        $outText = if (Test-Path $stdOut) { Get-Content -LiteralPath $stdOut -Raw } else { '' }
        $errText = if (Test-Path $stdErr) { Get-Content -LiteralPath $stdErr -Raw } else { '' }
        @('=== STDOUT ===', $outText, '=== STDERR ===', $errText) |
            Out-File -LiteralPath $log -Encoding UTF8
        if ($code -ne 0) {
            Get-Content -LiteralPath $log -Tail 50 | Out-Host
        }
    }
    catch {
        $code = -1
        $errorText = ($_ | Out-String)
        $errorText | Add-Content -LiteralPath $log -Encoding UTF8
        Write-Host $errorText -ForegroundColor Red
    }
    finally {
        Pop-Location
        Remove-Item -LiteralPath $stdOut, $stdErr -ErrorAction SilentlyContinue
    }
    $state = if ($code -eq 0) { 'PASS' } else { 'FAIL' }
    $elapsed = [math]::Round(((Get-Date) - $start).TotalSeconds, 1)
    $results.Add([pscustomobject]@{
        Stage = $Name
        Status = $state
        ExitCode = $code
        Seconds = $elapsed
        LogPath = $log
    })
    Write-Host ($Name + ': ' + $state) -ForegroundColor $(if ($code -eq 0) { 'Green' } else { 'Red' })
}

$solution = Join-Path $root 'TitanMDM.slnx'
if (-not (Test-Path -LiteralPath $solution)) {
    throw 'TitanMDM.slnx does not exist in repository root.'
}
Write-Host ('Branch: ' + (& git branch --show-current))

Invoke-Stage -Name '01-Restore' -Directory $root -Executable 'dotnet' -Arguments @('restore', 'TitanMDM.slnx')
Invoke-Stage -Name '02-Build' -Directory $root -Executable 'dotnet' -Arguments @('build', 'TitanMDM.slnx', '-c', 'Release', '--no-restore')
Invoke-Stage -Name '03-Unit' -Directory $root -Executable 'dotnet' -Arguments @('test', 'tests/TitanMDM.UnitTests/TitanMDM.UnitTests.csproj', '-c', 'Release', '--no-restore', '--logger', 'console;verbosity=normal')

if (-not $SkipIntegration) {
    Invoke-Stage -Name '04-Integration' -Directory $root -Executable 'dotnet' -Arguments @('test', 'tests/TitanMDM.IntegrationTests/TitanMDM.IntegrationTests.csproj', '-c', 'Release', '--no-restore', '--logger', 'console;verbosity=normal')
}

if (-not $SkipFrontend) {
    $frontend = Join-Path $root 'src\frontend\titanmdm-web'
    if (-not (Test-Path -LiteralPath $frontend)) {
        throw ('Frontend directory missing: ' + $frontend)
    }
    Invoke-Stage -Name '05-Frontend-Build' -Directory $frontend -Executable 'npm.cmd' -Arguments @('run', 'build')
    Invoke-Stage -Name '06-Frontend-Tests' -Directory $frontend -Executable 'npm.cmd' -Arguments @('run', 'test', '--', '--run')
}

$csv = Join-Path $logDir 'AUT1-QA-RESULTS.csv'
$results | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
Write-Host '=== FINAL RESULTS ===' -ForegroundColor Cyan
$results | Format-Table -AutoSize
Write-Host ('Logs: ' + $logDir)

$failures = @($results | Where-Object { $_.Status -ne 'PASS' })
if ($failures.Count -gt 0) {
    Write-Host ('Failed stages: ' + $failures.Count) -ForegroundColor Red
    exit 1
}
Write-Host 'All executed stages passed. Inspect logs for skipped tests.' -ForegroundColor Green
exit 0
