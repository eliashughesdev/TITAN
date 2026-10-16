
# TitanMDM - F3-A Launcher Studio Pro
# No hace commit, push ni modifica main.
# Actualiza el modelo y diseñador existentes de forma controlada.

$ErrorActionPreference = "Stop"
$root = "C:\TitanMDM"
$frontend = Join-Path $root "src\frontend\titanmdm-web"
$launcherDir = Join-Path $frontend "src\pages\android-launcher"
$modelPath = Join-Path $launcherDir "launcherModel.ts"
$pagePath = Join-Path $launcherDir "TitanLauncherPage.tsx"
$stylePath = Join-Path $launcherDir "TitanLauncherStudioPro.css"

Set-Location $root

if ((git branch --show-current).Trim() -ne "feat/remote-support-rs-h") {
    throw "Rama incorrecta: no se modificara el proyecto."
}

foreach ($file in @($modelPath, $pagePath)) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "Archivo no encontrado: $file"
    }
}

$model = [IO.File]::ReadAllText($modelPath)
$page = [IO.File]::ReadAllText($pagePath)

if ($model.Contains("wallpaperUrl?: string")) {
    throw "F3-A ya aparece en launcherModel.ts. No repetir integracion."
}

function Replace-Exact {
    param(
        [string]$Source,
        [string]$Find,
        [string]$Replacement,
        [string]$Label
    )
    if (-not $Source.Contains($Find)) {
        throw "Punto de integracion no encontrado: $Label"
    }
    return $Source.Replace($Find, $Replacement)
}

# Normalizar para que las anclas no dependan de CRLF.
$model = $model.Replace("`r`n", "`n")
$page = $page.Replace("`r`n", "`n")

# 1. Extender el modelo.
$model = Replace-Exact $model `
    "  backgroundColor: string`n  packageName: string" `
    "  backgroundColor: string`n  wallpaperUrl?: string`n  logoUrl?: string`n  gridColumns?: number`n  packageName: string" `
    "LauncherDraft"

$model = Replace-Exact $model `
    "  backgroundColor: DEFAULT_BACKGROUND_COLOR,`n  packageName:" `
    "  backgroundColor: DEFAULT_BACKGROUND_COLOR,`n  wallpaperUrl: '',`n  logoUrl: '',`n  gridColumns: 4,`n  packageName:" `
    "createLauncherDraft"

$model = Replace-Exact $model `
    "        backgroundColor: draft.backgroundColor.trim().toUpperCase(),`n        requiredForSetup:" `
    "        backgroundColor: draft.backgroundColor.trim().toUpperCase(),`n        wallpaperUrl: (draft.wallpaperUrl || '').trim(),`n        logoUrl: (draft.logoUrl || '').trim(),`n        gridColumns: draft.gridColumns || 4,`n        requiredForSetup:" `
    "buildLauncherConfiguration"

$model = Replace-Exact $model `
    "      backgroundColor: normalizeBrandingColor(launcher.backgroundColor, defaults.backgroundColor),`n      packageName:" `
    "      backgroundColor: normalizeBrandingColor(launcher.backgroundColor, defaults.backgroundColor),`n      wallpaperUrl: asString(launcher.wallpaperUrl, ''),`n      logoUrl: asString(launcher.logoUrl, ''),`n      gridColumns: [3, 4, 5].includes(Number(launcher.gridColumns)) ? Number(launcher.gridColumns) : 4,`n      packageName:" `
    "parseLauncherConfiguration"

# 2. Crear componente completo de branding.
$componentPath = Join-Path $launcherDir "LauncherBrandingStudio.tsx"

$component = @'
import type { ChangeEvent } from 'react'
import type { LauncherDraft } from './launcherModel'
import './TitanLauncherStudioPro.css'

interface Props {
  draft: LauncherDraft
  disabled: boolean
  onChange: (patch: Partial<LauncherDraft>) => void
}

function validHttpsImageUrl(value: string): boolean {
  if (!value.trim()) return true
  try {
    const url = new URL(value)
    return url.protocol === 'https:' &&
      !url.username &&
      !url.password &&
      url.hostname.length > 0 &&
      url.toString().length <= 2048
  } catch {
    return false
  }
}

export function LauncherBrandingStudio({
  draft,
  disabled,
  onChange,
}: Props) {
  const wallpaper = draft.wallpaperUrl || ''
  const logo = draft.logoUrl || ''
  const wallpaperValid = validHttpsImageUrl(wallpaper)
  const logoValid = validHttpsImageUrl(logo)

  function changeUrl(
    field: 'wallpaperUrl' | 'logoUrl',
    event: ChangeEvent<HTMLInputElement>,
  ) {
    onChange({ [field]: event.target.value })
  }

  return (
    <section className="tlsp">
      <div className="tlsp-header">
        <div>
          <span>TITAN LAUNCHER STUDIO PRO</span>
          <h3>Identidad visual corporativa</h3>
          <p>
            Personaliza la experiencia del dispositivo
            manteniendo la configuración centralizada.
          </p>
        </div>
        <span className="tlsp-tag">Perfil versionado</span>
      </div>

      <div className="tlsp-fields">
        <label>
          <span>Imagen de fondo (URL HTTPS)</span>
          <input
            disabled={disabled}
            value={wallpaper}
            onChange={event => changeUrl('wallpaperUrl', event)}
            placeholder="https://servidor.empresa.com/launcher/fondo.webp"
            aria-invalid={!wallpaperValid}
          />
        </label>

        <label>
          <span>Logotipo corporativo (URL HTTPS)</span>
          <input
            disabled={disabled}
            value={logo}
            onChange={event => changeUrl('logoUrl', event)}
            placeholder="https://servidor.empresa.com/launcher/logo.png"
            aria-invalid={!logoValid}
          />
        </label>

        <label>
          <span>Columnas de aplicaciones</span>
          <select
            disabled={disabled}
            value={draft.gridColumns || 4}
            onChange={event => onChange({
              gridColumns: Number(event.target.value),
            })}
          >
            <option value={3}>3 columnas</option>
            <option value={4}>4 columnas</option>
            <option value={5}>5 columnas</option>
          </select>
        </label>
      </div>

      {(!wallpaperValid || !logoValid) && (
        <p className="tlsp-warning" role="alert">
          Usa enlaces HTTPS válidos, sin credenciales
          incorporadas ni direcciones locales.
        </p>
      )}

      <p className="tlsp-note">
        Las imágenes se referencian mediante URL, no se
        guardan como archivos dentro del JSON de la política.
        El teléfono debe poder acceder a la URL publicada.
      </p>

      <div
        className="tlsp-preview"
        style={{
          backgroundColor: draft.backgroundColor,
          backgroundImage: wallpaperValid && wallpaper
            ? `linear-gradient(#06121a66,#06121a99),url("${wallpaper}")`
            : undefined,
        }}
      >
        <div className="tlsp-preview-top">
          {logoValid && logo ? (
            <img
              src={logo}
              alt="Logotipo corporativo"
              referrerPolicy="no-referrer"
            />
          ) : (
            <span className="tlsp-monogram">
              {(draft.companyName || 'T').charAt(0).toUpperCase()}
            </span>
          )}
          <div>
            <strong>{draft.companyName}</strong>
            <small>{draft.welcomeMessage}</small>
          </div>
        </div>

        <div
          className="tlsp-app-grid"
          style={{
            gridTemplateColumns:
              `repeat(${draft.gridColumns || 4}, minmax(0, 1fr))`,
          }}
        >
          {draft.applications
            .filter(app => app.visible)
            .map(app => (
              <div key={app.packageName} className="tlsp-app">
                <span>
                  {(app.label || app.packageName)
                    .charAt(0).toUpperCase()}
                </span>
                <small>{app.label || app.packageName}</small>
              </div>
            ))}
        </div>

        <footer>Administrado por TitanMDM</footer>
      </div>
    </section>
  )
}
'@

# 3. Montar el editor en la pantalla de diseñador existente.
$page = Replace-Exact $page `
    "import './TitanLauncherPage.css'" `
    "import { LauncherBrandingStudio } from './LauncherBrandingStudio'`nimport './TitanLauncherPage.css'" `
    "import LauncherBrandingStudio"

$anchor = '          <label><span>Paquete del agente</span>'

$insert = @'
          <div className="launcher-wide">
            <LauncherBrandingStudio
              draft={draft}
              disabled={!canManage}
              onChange={onDraft}
            />
          </div>
'@

$page = Replace-Exact $page `
    $anchor `
    ($insert + "`n" + $anchor) `
    "Diseñador Launcher"

# 4. Crear CSS encapsulado.
$css = @'
.tlsp {
  --tlsp-green: #159b63;
  width: 100%;
  display: grid;
  gap: 18px;
  padding: 20px;
  border: 1px solid #d5e9dd;
  border-radius: 17px;
  background: linear-gradient(120deg, #f8fcf9, #eef8f2);
}

.tlsp * {
  box-sizing: border-box;
}

.tlsp-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: start;
}

.tlsp-header > div > span {
  color: #168552;
  letter-spacing: .1em;
  font-weight: 850;
  font-size: 10px;
}

.tlsp-header h3 {
  font-size: 18px;
  font-weight: 800;
  margin: 7px 0 5px;
}

.tlsp-header p,
.tlsp-note {
  font-size: 11px;
  line-height: 1.6;
  color: #607585;
  margin: 0;
}

.tlsp-tag {
  white-space: nowrap;
  border: 1px solid #b7e8cc;
  background: #dff8e9;
  color: #107345;
  padding: 7px 10px;
  border-radius: 30px;
  font-size: 10px;
  font-weight: 800;
}

.tlsp-fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.tlsp-fields label {
  display: grid;
  gap: 7px;
  min-width: 0;
}

.tlsp-fields label > span {
  font-weight: 700;
  font-size: 11px;
  color: #294d3b;
}

.tlsp-fields input,
.tlsp-fields select {
  width: 100%;
  min-height: 41px;
  border: 1px solid #ccdfd4;
  border-radius: 9px;
  background: #fff;
  padding: 10px 12px;
  font: inherit;
  color: #203f30;
  font-size: 12px;
}

.tlsp-fields [aria-invalid="true"] {
  border-color: #e76d6d;
}

.tlsp-warning {
  color: #a43c3c;
  background: #fff1f1;
  padding: 11px;
  border-radius: 9px;
  font-size: 11px;
}

.tlsp-preview {
  min-height: 330px;
  padding: 24px;
  border-radius: 18px;
  color: white;
  display: flex;
  flex-direction: column;
  gap: 26px;
  background-size: cover;
  background-position: center;
  overflow: hidden;
}

.tlsp-preview-top {
  display: flex;
  align-items: center;
  gap: 13px;
}

.tlsp-preview-top img {
  width: 52px;
  height: 52px;
  object-fit: contain;
}

.tlsp-monogram {
  background: #159b63;
  width: 52px;
  height: 52px;
  border-radius: 13px;
  display: grid;
  place-items: center;
  font-size: 23px;
  font-weight: 900;
}

.tlsp-preview-top div {
  display: grid;
  gap: 5px;
}

.tlsp-preview-top strong {
  font-size: 19px;
}

.tlsp-preview-top small {
  opacity: .8;
}

.tlsp-app-grid {
  display: grid;
  gap: 14px;
}

.tlsp-app {
  text-align: center;
  min-width: 0;
}

.tlsp-app > span {
  display: grid;
  place-items: center;
  margin: auto auto 8px;
  width: 52px;
  height: 52px;
  border-radius: 14px;
  background: #159b63;
  font-weight: 900;
  font-size: 21px;
}

.tlsp-app small {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tlsp-preview footer {
  margin-top: auto;
  text-align: center;
  font-size: 10px;
  opacity: .7;
}

@media(max-width: 720px) {
  .tlsp-fields {
    grid-template-columns: 1fr;
  }
  .tlsp-header {
    flex-direction: column;
  }
}
'@

# 5. Respaldar los archivos antes de modificar.
$backupDir = Join-Path $root "artifacts\f3-launcher-backup"
New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

Copy-Item $modelPath (Join-Path $backupDir "launcherModel.ts.bak") -Force
Copy-Item $pagePath (Join-Path $backupDir "TitanLauncherPage.tsx.bak") -Force

try {
    [IO.File]::WriteAllText(
        $modelPath,
        $model,
        [Text.UTF8Encoding]::new($false)
    )

    [IO.File]::WriteAllText(
        $pagePath,
        $page,
        [Text.UTF8Encoding]::new($false)
    )

    [IO.File]::WriteAllText(
        $componentPath,
        $component,
        [Text.UTF8Encoding]::new($false)
    )

    [IO.File]::WriteAllText(
        $stylePath,
        $css,
        [Text.UTF8Encoding]::new($false)
    )

    Set-Location $frontend

    Write-Host "`n=== TYPESCRIPT ===" -ForegroundColor Cyan
    npx tsc --noEmit

    if ($LASTEXITCODE -ne 0) {
        throw "Validacion TypeScript fallida."
    }

    Write-Host "`n=== BUILD ===" -ForegroundColor Cyan
    npm run build

    if ($LASTEXITCODE -ne 0) {
        throw "Compilacion React fallida."
    }

    Write-Host "`n=== TESTS ===" -ForegroundColor Cyan
    npm run test -- --run

    if ($LASTEXITCODE -ne 0) {
        throw "Pruebas React fallidas."
    }

    Write-Host "`nF3-A: UI y modelo compilados." -ForegroundColor Green
}
catch {
    Copy-Item (Join-Path $backupDir "launcherModel.ts.bak") $modelPath -Force
    Copy-Item (Join-Path $backupDir "TitanLauncherPage.tsx.bak") $pagePath -Force

    Remove-Item $componentPath -Force -ErrorAction SilentlyContinue
    Remove-Item $stylePath -Force -ErrorAction SilentlyContinue

    Write-Host "Cambios F3-A revertidos." -ForegroundColor Yellow
    throw
}
finally {
    Set-Location $root
}

git diff --check
git status --short
