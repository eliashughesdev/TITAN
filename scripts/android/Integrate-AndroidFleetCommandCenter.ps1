
$ErrorActionPreference = "Stop"

$root = "C:\TitanMDM"
$frontend = Join-Path $root "src\frontend\titanmdm-web"
$devicesPage = Join-Path $frontend "src\pages\devices\DevicesPage.tsx"
$fleetPage = Join-Path $frontend "src\pages\devices\components\AndroidFleetCommandCenter.tsx"
$fleetCss = Join-Path $frontend "src\pages\devices\components\AndroidFleetCommandCenter.css"

Set-Location $root

if ((git branch --show-current).Trim() -ne "feat/remote-support-rs-h") {
    throw "Rama incorrecta. No se aplicaran cambios."
}

foreach ($path in @($devicesPage, $fleetPage, $fleetCss)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Falta un archivo requerido: $path"
    }
}

$content = Get-Content -LiteralPath $devicesPage -Raw -Encoding UTF8

$import = @'
import { AndroidFleetCommandCenter } from './components/AndroidFleetCommandCenter'

'@

$targetImport = "import './DevicesPage.css'"

if (-not $content.Contains("import { AndroidFleetCommandCenter }")) {
    if (-not $content.Contains($targetImport)) {
        throw "No se encontro el punto de integracion de imports."
    }

    $content = $content.Replace(
        $targetImport,
        $import + $targetImport
    )
}

$targetRender = '      <section className="devices-panel">'

$integration = @'
      {isAndroidWorkspace && (
        <AndroidFleetCommandCenter />
      )}

'@

if (-not $content.Contains('<AndroidFleetCommandCenter />')) {
    if (-not $content.Contains($targetRender)) {
        throw "La estructura de DevicesPage cambio. No se modifico."
    }

    $content = $content.Replace(
        $targetRender,
        $integration + $targetRender
    )
}

$backup = "$devicesPage.pre-fleet.bak"
Copy-Item -LiteralPath $devicesPage -Destination $backup -Force

try {
    Set-Content -LiteralPath $devicesPage -Value $content -Encoding utf8 -NoNewline

    # Corregir la ruta real de Titan Launcher
    $fleetContent = Get-Content -LiteralPath $fleetPage -Raw -Encoding UTF8

    $fleetContent = $fleetContent.Replace(
        'to="/android-launcher?workspace=android"',
        'to="/android/titan-launcher?workspace=android"'
    )

    Set-Content -LiteralPath $fleetPage -Value $fleetContent -Encoding utf8 -NoNewline

    Set-Location $frontend

    Write-Host "=== COMPILACION FRONTEND ===" -ForegroundColor Cyan
    npm run build

    if ($LASTEXITCODE -ne 0) {
        throw "Falló npm run build"
    }

    Write-Host "=== PRUEBAS FRONTEND ===" -ForegroundColor Cyan
    npm run test -- --run

    if ($LASTEXITCODE -ne 0) {
        throw "Fallaron las pruebas frontend"
    }

    Write-Host "CENTRO DE CONTROL INTEGRADO" -ForegroundColor Green
}
catch {
    Copy-Item -LiteralPath $backup -Destination $devicesPage -Force
    Write-Host "DevicesPage.tsx restaurado por error." -ForegroundColor Yellow
    throw
}
finally {
    Set-Location $root
}

git diff --check
git status --short
