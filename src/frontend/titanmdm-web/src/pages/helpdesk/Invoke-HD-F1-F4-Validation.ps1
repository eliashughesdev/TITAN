param(
    [string]$Root = 'C:\TitanMDM',
    [string]$ApiBase = 'http://172.21.20.14:8020',
    [switch]$TestGraph,
    [switch]$SkipApiTests
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!(Test-Path (Join-Path $Root 'TitanMDM.slnx'))) { throw "Directorio de TitanMDM no válido: $Root" }
Push-Location $Root
try {
    $branch = (git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'feat/remote-support-rs-h') { throw "Rama incorrecta: $branch" }
    Write-Host '[1/5] Build .NET' -ForegroundColor Cyan
    dotnet build (Join-Path $Root 'TitanMDM.slnx') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'BUILD .NET FALLIDO' }
    Write-Host '[2/5] Tests de Helpdesk' -ForegroundColor Cyan
    dotnet test (Join-Path $Root 'tests\TitanMDM.UnitTests\TitanMDM.UnitTests.csproj') -c Release --filter 'FullyQualifiedName~Helpdesk'
    if ($LASTEXITCODE -ne 0) { throw 'TESTS HELPDESK FALLIDOS' }
    Write-Host '[3/5] Frontend build + tests' -ForegroundColor Cyan
    Push-Location (Join-Path $Root 'src\frontend\titanmdm-web')
    try {
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'BUILD FRONTEND FALLIDO' }
        npm test -- --passWithNoTests
        if ($LASTEXITCODE -ne 0) { throw 'TESTS FRONTEND FALLIDOS' }
    } finally { Pop-Location }
    Write-Host '[4/5] git diff --check / git status' -ForegroundColor Cyan
    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'GIT DIFF FALLIDO' }
    git status --short
    git diff --stat
    if (!$SkipApiTests) {
        Write-Host '[5/5] Diagnóstico API (JWT no se imprime ni almacena)' -ForegroundColor Cyan
        $secure = Read-Host 'Pega el JWT completo de tu sesión, solo en tu PowerShell' -AsSecureString
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try { $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
        if (($token -split '\.').Count -ne 3) { throw 'JWT incompleto. Debe tener tres segmentos separados por puntos.' }
        $headers = @{ Authorization = "Bearer $token" }
        $rootApi = $ApiBase.TrimEnd('/')
        foreach ($path in @('/api/helpdesk/follow-up','/api/helpdesk/routing/health','/api/helpdesk/mail/activity/summary')) {
            try {
                $response = Invoke-RestMethod -Method Get -Uri "$rootApi$path" -Headers $headers -TimeoutSec 25
                Write-Host "PASS $path" -ForegroundColor Green
                if ($path.EndsWith('follow-up')) {
                    Write-Host "SLA: $($response.summary | ConvertTo-Json -Compress)"
                }
            } catch {
                Write-Warning "NO VALIDADO $path : $($_.Exception.Message)"
            }
        }
        if ($TestGraph) {
            Write-Host 'Prueba Graph seleccionada explícitamente.' -ForegroundColor Yellow
            try {
                $graph = Invoke-RestMethod -Method Post -Uri "$rootApi/api/helpdesk/mail/test" -Headers $headers -ContentType 'application/json' -Body '{}' -TimeoutSec 45
                Write-Host ('GRAPH: ' + ($graph | ConvertTo-Json -Depth 6 -Compress))
            } catch { Write-Warning "GRAPH NO VALIDADO: $($_.Exception.Message)" }
        }
        $token = $null
        $headers.Clear()
    }
    Write-Host 'BUILD Y DIFF TERMINADOS. Validar manualmente creación, asignación, correo de prueba y SLA.' -ForegroundColor Green
} finally { Pop-Location }
