import { useEffect, useState, type DragEvent } from 'react'
import apiClient from '../../api/apiClient'
import {
  contrastRatio,
  hasReadableContrast,
  isAssetToken,
  isValidLauncherResourceUrl,
  type LauncherDraft,
} from './launcherModel'
import './TitanLauncherStudioPro.css'

interface Props {
  draft: LauncherDraft
  disabled: boolean
  onChange: (patch: Partial<LauncherDraft>) => void
}

interface StoredAsset {
  id: string
  kind: string
  format: string
  token: string
  createdAtUtc: string
}

const presets = {
  corporate: {
    themePreset: 'corporate' as const,
    primaryColor: '#16A36A',
    secondaryColor: '#0E7C54',
    backgroundColor: '#0B1720',
    textColor: '#F4F7F8',
    cardColor: '#142831',
    cardTextColor: '#F4F7F8',
    buttonColor: '#16A36A',
  },
  dark: {
    themePreset: 'dark' as const,
    primaryColor: '#7DCEA0',
    secondaryColor: '#1B4F3E',
    backgroundColor: '#071015',
    textColor: '#F7FBF8',
    cardColor: '#102028',
    cardTextColor: '#F7FBF8',
    buttonColor: '#7DCEA0',
  },
}

export function LauncherBrandingStudio({ draft, disabled, onChange }: Props) {
  const [previous, setPrevious] = useState<StoredAsset[]>([])
  const [preview, setPreview] = useState<{ wallpaper?: string; logo?: string }>({})
  const [uploadError, setUploadError] = useState<string | null>(null)
  const wallpaper = draft.wallpaperUrl || ''
  const logo = draft.logoUrl || ''
  const wallpaperValid = isValidLauncherResourceUrl(wallpaper)
  const logoValid = isValidLauncherResourceUrl(logo)
  const textContrast = contrastRatio(draft.textColor || '#F4F7F8', draft.backgroundColor)
  const cardContrast = contrastRatio(draft.cardTextColor || '#F4F7F8', draft.cardColor || '#142831')

  useEffect(() => {
    let active = true
    apiClient.get<StoredAsset[]>('/android-enterprise/launcher-assets')
      .then((response) => {
        if (active) setPrevious(response.data)
      })
      .catch(() => {
        if (active) setPrevious([])
      })
    return () => {
      active = false
    }
  }, [wallpaper, logo])

  async function upload(kind: 'wallpaper' | 'logo', file: File) {
    setUploadError(null)
    const localPreview = URL.createObjectURL(file)
    setPreview((current) => ({ ...current, [kind]: localPreview }))
    const body = new FormData()
    body.set('kind', kind)
    body.set('file', file)
    try {
      const response = await apiClient.post<{ token: string }>(
        '/android-enterprise/launcher-assets',
        body,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      )
      onChange(kind === 'wallpaper'
        ? { wallpaperUrl: response.data.token }
        : { logoUrl: response.data.token })
    } catch {
      setUploadError('No se pudo guardar la imagen. Usa PNG, JPEG o WebP dentro del límite.')
    }
  }

  function drop(kind: 'wallpaper' | 'logo', event: DragEvent<HTMLLabelElement>) {
    event.preventDefault()
    const file = event.dataTransfer.files[0]
    if (file && !disabled) void upload(kind, file)
  }

  const wallpaperPreview = preview.wallpaper || (wallpaperValid && wallpaper.startsWith('https://') ? wallpaper : undefined)
  const logoPreview = preview.logo || (logoValid && logo.startsWith('https://') ? logo : undefined)

  return (
    <section className="tlsp">
      <div className="tlsp-header">
        <div>
          <span>TITAN LAUNCHER STUDIO PRO</span>
          <h3>Identidad visual corporativa</h3>
          <p>Adjunta el fondo y el logotipo. La política guarda el identificador, no la imagen.</p>
        </div>
        <span className="tlsp-tag">Perfil versionado</span>
      </div>

      <div className="tlsp-fields">
        <label
          className="tlsp-drop"
          onDragOver={(event) => event.preventDefault()}
          onDrop={(event) => drop('wallpaper', event)}
        >
          <span>Adjuntar fondo</span>
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            disabled={disabled}
            onChange={(event) => {
              const file = event.target.files?.[0]
              if (file) void upload('wallpaper', file)
            }}
          />
        </label>
        <label
          className="tlsp-drop"
          onDragOver={(event) => event.preventDefault()}
          onDrop={(event) => drop('logo', event)}
        >
          <span>Adjuntar logotipo</span>
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            disabled={disabled}
            onChange={(event) => {
              const file = event.target.files?.[0]
              if (file) void upload('logo', file)
            }}
          />
        </label>
        <label>
          <span>Ajuste de imagen</span>
          <select
            disabled={disabled}
            value={draft.imageFit || 'cover'}
            onChange={(event) => onChange({
              imageFit: event.target.value as LauncherDraft['imageFit'],
              themePreset: 'custom',
            })}
          >
            <option value="cover">Cubrir</option>
            <option value="contain">Contener</option>
            <option value="center">Centrado</option>
          </select>
        </label>
        <label>
          <span>Columnas</span>
          <select
            disabled={disabled}
            value={draft.gridColumns || 4}
            onChange={(event) => onChange({ gridColumns: Number(event.target.value) })}
          >
            <option value={3}>3 columnas</option>
            <option value={4}>4 columnas</option>
            <option value={5}>5 columnas</option>
          </select>
        </label>
      </div>

      <div className="tlsp-actions">
        <button type="button" disabled={disabled || !wallpaper} onClick={() => onChange({ wallpaperUrl: '' })}>Quitar fondo</button>
        <button type="button" disabled={disabled || !logo} onClick={() => onChange({ logoUrl: '' })}>Quitar logotipo</button>
        <button type="button" disabled={disabled} onClick={() => onChange(presets.corporate)}>Preset corporativo</button>
        <button type="button" disabled={disabled} onClick={() => onChange(presets.dark)}>Modo oscuro</button>
      </div>

      {previous.length > 0 && (
        <div className="tlsp-previous">
          <span>Imagenes anteriores de esta organizacion</span>
          {previous.map((asset) => (
            <button
              key={asset.id}
              type="button"
              disabled={disabled}
              onClick={() => onChange(asset.kind === 'logo'
                ? { logoUrl: asset.token }
                : { wallpaperUrl: asset.token })}
            >
              {asset.kind} {asset.format}
            </button>
          ))}
        </div>
      )}

      {(!wallpaperValid || !logoValid || uploadError) && (
        <p className="tlsp-warning" role="alert">
          {uploadError || 'Usa un recurso corporativo o un HTTPS publico PNG, JPEG o WebP.'}
        </p>
      )}
      {(!hasReadableContrast(draft.textColor || '#F4F7F8', draft.backgroundColor) ||
        !hasReadableContrast(draft.cardTextColor || '#F4F7F8', draft.cardColor || '#142831')) && (
        <p className="tlsp-warning" role="status">
          Contraste insuficiente ({textContrast.toFixed(2)} / {cardContrast.toFixed(2)}). El minimo recomendado es 4.5:1.
          <button
            type="button"
            disabled={disabled}
            onClick={() => onChange({ textColor: '#F4F7F8', cardTextColor: '#F4F7F8', backgroundColor: '#0B1720', cardColor: '#142831' })}
          >
            Ajustar contraste
          </button>
        </p>
      )}

      <div className="tlsp-theme">
        <ColorField label="Primario" value={draft.primaryColor} disabled={disabled} onChange={(primaryColor) => onChange({ primaryColor, themePreset: 'custom' })} />
        <ColorField label="Secundario" value={draft.secondaryColor || '#0E7C54'} disabled={disabled} onChange={(secondaryColor) => onChange({ secondaryColor, themePreset: 'custom' })} />
        <ColorField label="Fondo" value={draft.backgroundColor} disabled={disabled} onChange={(backgroundColor) => onChange({ backgroundColor, themePreset: 'custom' })} />
        <ColorField label="Texto" value={draft.textColor || '#F4F7F8'} disabled={disabled} onChange={(textColor) => onChange({ textColor, themePreset: 'custom' })} />
        <ColorField label="Tarjeta" value={draft.cardColor || '#142831'} disabled={disabled} onChange={(cardColor) => onChange({ cardColor, themePreset: 'custom' })} />
        <ColorField label="Texto de tarjeta" value={draft.cardTextColor || '#F4F7F8'} disabled={disabled} onChange={(cardTextColor) => onChange({ cardTextColor, themePreset: 'custom' })} />
        <ColorField label="Boton" value={draft.buttonColor || '#16A36A'} disabled={disabled} onChange={(buttonColor) => onChange({ buttonColor, themePreset: 'custom' })} />
        <label>
          <span>Radio {draft.borderRadius ?? 18}</span>
          <input type="range" min={0} max={32} disabled={disabled} value={draft.borderRadius ?? 18} onChange={(event) => onChange({ borderRadius: Number(event.target.value), themePreset: 'custom' })} />
        </label>
        <label>
          <span>Transparencia {draft.cardOpacity ?? 100}</span>
          <input type="range" min={0} max={100} disabled={disabled} value={draft.cardOpacity ?? 100} onChange={(event) => onChange({ cardOpacity: Number(event.target.value), themePreset: 'custom' })} />
        </label>
        <label>
          <span>Sombra {draft.shadowIntensity ?? 24}</span>
          <input type="range" min={0} max={100} disabled={disabled} value={draft.shadowIntensity ?? 24} onChange={(event) => onChange({ shadowIntensity: Number(event.target.value), themePreset: 'custom' })} />
        </label>
        <label>
          <span>Icono {draft.iconSize ?? 56}</span>
          <input type="range" min={40} max={96} disabled={disabled} value={draft.iconSize ?? 56} onChange={(event) => onChange({ iconSize: Number(event.target.value), themePreset: 'custom' })} />
        </label>
        <label>
          <span>Espaciado {draft.gridGap ?? 12}</span>
          <input type="range" min={4} max={32} disabled={disabled} value={draft.gridGap ?? 12} onChange={(event) => onChange({ gridGap: Number(event.target.value), themePreset: 'custom' })} />
        </label>
      </div>

      <p className="tlsp-note">
        {isAssetToken(wallpaper) || isAssetToken(logo)
          ? 'Recurso corporativo privado. No se publica la ruta del servidor ni Base64.'
          : 'Un perfil anterior puede conservar una URL HTTPS. Sustituyela adjuntando un archivo.'}
      </p>

      <div
        className="tlsp-preview"
        style={{
          backgroundColor: draft.backgroundColor,
          color: draft.textColor,
          backgroundImage: wallpaperPreview
            ? `linear-gradient(#06121a66,#06121a99),url("${wallpaperPreview}")`
            : undefined,
          backgroundSize: draft.imageFit === 'contain' ? 'contain' : 'cover',
          backgroundPosition: 'center',
          borderRadius: draft.borderRadius ?? 18,
        }}
      >
        <div className="tlsp-preview-top">
          {logoPreview ? (
            <img src={logoPreview} alt="Logotipo corporativo" referrerPolicy="no-referrer" />
          ) : (
            <span className="tlsp-monogram">{(draft.companyName || 'T').charAt(0).toUpperCase()}</span>
          )}
          <div>
            <strong>{draft.companyName}</strong>
            <small>{draft.welcomeMessage}</small>
          </div>
        </div>
        <div
          className="tlsp-app-grid"
          style={{ gridTemplateColumns: `repeat(${draft.gridColumns || 4}, minmax(0, 1fr))`, gap: draft.gridGap ?? 12 }}
        >
          {draft.applications.filter((app) => app.visible).map((app) => (
            <div
              key={app.packageName}
              className="tlsp-app"
              style={{
                color: draft.cardTextColor,
                opacity: (draft.cardOpacity ?? 100) / 100,
              }}
            >
              <span style={{ width: draft.iconSize ?? 56, height: draft.iconSize ?? 56, background: draft.buttonColor }}>
                {(app.label || app.packageName).charAt(0).toUpperCase()}
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

function ColorField({
  label,
  value,
  disabled,
  onChange,
}: {
  label: string
  value: string
  disabled: boolean
  onChange: (value: string) => void
}) {
  return (
    <label>
      <span>{label}</span>
      <input type="color" disabled={disabled} value={value} onChange={(event) => onChange(event.target.value.toUpperCase())} />
    </label>
  )
}
