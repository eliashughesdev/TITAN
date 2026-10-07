[CmdletBinding()]
param(
    [int]$PythonPort = 8127,
    [int]$ApiPort = 8020,
    [switch]$EnableEntra
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..')
)

$pythonDir = Join-Path $repoRoot 'integrations\ponches\backend'
$pythonExe = Join-Path $pythonDir '.venv\Scripts\python.exe'
$apiProject = Join-Path $repoRoot 'src\backend\TitanMDM.Api\TitanMDM.Api.csproj'
$logDir = Join-Path $repoRoot '.titan-local'

$pythonProcess = $null
$previousLocation = Get-Location
$environmentBackup = @{}

function Test-LocalPort([int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient

    try {
        $task = $client.ConnectAsync('127.0.0.1', $Port)

        if (-not $task.Wait(600)) {
            return $false
        }

        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

try {
    Set-Location $repoRoot

    if (-not (Test-Path $pythonExe)) {
        throw 'Falta el entorno Python. Crea .venv con Python 3.12 e instala requirements.txt.'
    }

    if (
        $PythonPort -lt 1024 -or
        $PythonPort -gt 65535 -or
        $ApiPort -lt 1024 -or
        $ApiPort -gt 65535 -or
        $PythonPort -eq $ApiPort
    ) {
        throw 'Los puertos deben ser diferentes y estar entre 1024 y 65535.'
    }

    if (
        (Test-LocalPort $PythonPort) -or
        (Test-LocalPort $ApiPort)
    ) {
        throw 'Python o TitanMDM ya están ejecutándose. Detén sus terminales con Ctrl+C antes de usar este iniciador.'
    }

    Get-Command dotnet -ErrorAction Stop | Out-Null

    dotnet build $apiProject --nologo

    if ($LASTEXITCODE -ne 0) {
        throw 'El backend no compiló. Corrige el error mostrado antes de iniciar servicios.'
    }

    # Compatible con Windows PowerShell 5.1.
    $keyBytes = New-Object byte[] 32

    $generator =
        [System.Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $generator.GetBytes($keyBytes)
    }
    finally {
        $generator.Dispose()
    }

    $integrationKey = [Convert]::ToBase64String($keyBytes)

    $variables = @{
        'ASPNETCORE_ENVIRONMENT' = 'Development'
        'DOTNET_ENVIRONMENT' = 'Development'

        'EntraLogin__Enabled' = $(
            if ($EnableEntra) {
                'true'
            }
            else {
                'false'
            }
        )

        'Ponches__BaseUrl' = "http://127.0.0.1:$PythonPort"
        'Ponches__IntegrationKey' = $integrationKey
        'Ponches__TimeoutSeconds' = '180'
        'TITAN_PONCHES_INTEGRATION_KEY' = $integrationKey
    }

    foreach ($name in $variables.Keys) {
        $environmentBackup[$name] =
            [Environment]::GetEnvironmentVariable(
                $name,
                'Process'
            )

        [Environment]::SetEnvironmentVariable(
            $name,
            $variables[$name],
            'Process'
        )
    }

    New-Item `
        -ItemType Directory `
        -Path $logDir `
        -Force | Out-Null

    $outLog = Join-Path $logDir 'ponches.stdout.log'
    $errLog = Join-Path $logDir 'ponches.stderr.log'

    $pythonProcess = Start-Process `
        -FilePath $pythonExe `
        -ArgumentList @(
            '-m',
            'uvicorn',
            'app.main:app',
            '--host',
            '127.0.0.1',
            '--port',
            "$PythonPort"
        ) `
        -WorkingDirectory $pythonDir `
        -RedirectStandardOutput $outLog `
        -RedirectStandardError $errLog `
        -PassThru

    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    $health = $null

    while ([DateTime]::UtcNow -lt $deadline) {
        $pythonProcess.Refresh()

        if ($pythonProcess.HasExited) {
            throw "Python se cerró. Revisa $errLog"
        }

        try {
            $health = Invoke-RestMethod `
                -Uri "http://127.0.0.1:$PythonPort/api/internal/titan/health" `
                -Headers @{
                    'X-Titan-Integration-Key' = $integrationKey
                } `
                -TimeoutSec 10

            break
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if ($null -eq $health) {
        throw "Python no quedó listo en 60 segundos. Revisa $errLog"
    }

    if ($health.database -ne 'online') {
        throw 'Python inició, pero BioTimeDB está offline. Revisa la conexión de integrations\ponches\backend\.env.'
    }

    Write-Host `
        'Python, clave interna y BioTimeDB: verificados.' `
        -ForegroundColor Green

    Write-Host `
        "TitanMDM API: http://localhost:$ApiPort" `
        -ForegroundColor Cyan

    Write-Host 'Abre el frontend habitual. Mantén esta terminal abierta.'
    Write-Host 'Ctrl+C detiene esta sesión y el Python que inició este script.'

    dotnet run `
        --project $apiProject `
        --no-build `
        --no-launch-profile `
        --urls "http://0.0.0.0:$ApiPort"

    if ($LASTEXITCODE -ne 0) {
        throw 'TitanMDM se cerró con un error. Revisa la excepción anterior.'
    }
}
finally {
    if ($null -ne $pythonProcess) {
        $pythonProcess.Refresh()

        if (-not $pythonProcess.HasExited) {
            Stop-Process `
                -Id $pythonProcess.Id `
                -ErrorAction SilentlyContinue
        }
    }

    foreach ($name in $environmentBackup.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $environmentBackup[$name],
            'Process'
        )
    }

    Set-Location $previousLocation
}