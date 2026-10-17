import { useEffect, useState, type ReactNode } from 'react'
import {
  Boxes,
  Image as ImageIcon,
  LayoutGrid,
  Palette,
  Save,
  Settings2,
  ShieldCheck,
  CloudUpload,
  Rocket,
} from 'lucide-react'
import apiClient from '../../api/apiClient'
import { LauncherBrandingStudio } from './LauncherBrandingStudio'
import { ManagedGooglePlayPanel } from '../apps/components/ManagedGooglePlayPanel'
import {
  hasReadableContrast,
  normalizeBrandingColor,
  type LauncherApplication,
  type LauncherDraft,
  type LauncherInstallType,
} from './launcherModel'
import { launcherWorkflow, workflowLabel } from './studioWorkflow'
import './TitanLauncherStudioV3.css'

export type StudioTool = 'identity' | 'appearance' | 'media' | 'apps' | 'layout' | 'config' | 'publish'

const tools: Array<{ id: StudioTool; label: string; icon: typeof Palette }> = [
  { id: 'identity', label: 'Identidad', icon: ShieldCheck },
  { id: 'appearance', label: 'Apariencia', icon: Palette },
  { id: 'media', label: 'Fondo y logotipo', icon: ImageIcon },
  { id: 'apps', label: 'Aplicaciones', icon: Boxes },
  { id: 'layout', label: 'Distribucion', icon: LayoutGrid },
  { id: 'config', label: 'Configuracion', icon: Settings2 },
  { id: 'publish', label: 'Publicacion', icon: Rocket },
]

interface Props {
  name: string
  description: string
  draft: LauncherDraft
  dirty: boolean
  canManage: boolean
  working: boolean
  saved: boolean
  active: boolean
  published: boolean
  assigned: boolean
  applied: boolean
  version: string
  tool: StudioTool
  onTool: (tool: StudioTool) => void
  onName: (value: string) => void
  onDescription: (value: string) => void
  onDraft: (patch: Partial<LauncherDraft>) => void
  onApplications: (applications: LauncherApplication[]) => void
  onSave: () => Promise<void>
  onPublish: () => Promise<void>
  onDeployments: () => void
  appsPanel?: ReactNode
}

export function TitanLauncherStudioV3(props: Props) {
  const [scale, setScale] = useState<'phone' | 'large'>('phone')
  const [wallpaperPreview, setWallpaperPreview] = useState<string>()
  const [logoPreview, setLogoPreview] = useState<string>()
  const secure = typeof window !== 'undefined' && window.isSecureContext && window.location.protocol === 'https:'
  const status = launcherWorkflow({
    dirty: props.dirty,
    saved: props.saved,
    active: props.active,
    published: props.published,
    assigned: props.assigned,
    applied: props.applied,
    failed: false,
  })
  const visible = props.draft.applications.filter((app) => app.visible)
  const textOk = hasReadableContrast(props.draft.textColor || '#F4F7F8', props.draft.backgroundColor)

  useEffect(() => {
    let cancelled = false
    void resolvePreview(props.draft.wallpaperUrl).then((url) => {
      if (!cancelled) setWallpaperPreview(url)
    })
    void resolvePreview(props.draft.logoUrl).then((url) => {
      if (!cancelled) setLogoPreview(url)
    })
    return () => {
      cancelled = true
    }
  }, [props.draft.wallpaperUrl, props.draft.logoUrl])

  return (
    <section className="lx3" aria-label="Titan Launcher Studio">
      <header className="lx3-top">
        <div className="lx3-identity">
          <strong>{props.name || 'Perfil sin nombre'}</strong>
          <span>Version {props.version}</span>
        </div>
        <span className={`lx3-chip ${status === 'dirty' ? 'warn' : status === 'applied' ? 'ok' : ''}`} data-workflow={status}>
          {workflowLabel[status]}
        </span>
        <div className="lx3-actions">
          <button type="button" className="lx3-btn primary" disabled={!props.canManage || props.working || !props.dirty} onClick={() => void props.onSave()}><Save size={15} /> Guardar</button>
          <button type="button" className="lx3-btn" disabled={!props.canManage || props.working || props.dirty || !props.active} onClick={() => void props.onPublish()}><CloudUpload size={15} /> Publicar</button>
          <button type="button" className="lx3-btn" onClick={() => setScale((value) => value === 'phone' ? 'large' : 'phone')}>Previsualizar</button>
          <button type="button" className="lx3-btn" onClick={props.onDeployments}><Rocket size={15} /> Despliegues</button>
        </div>
      </header>
      <div className="lx3-progress" aria-label="Estado del perfil">
        {(['dirty', 'saved', 'active', 'published', 'assigned', 'applying', 'applied'] as const).map((step) => (
          <span key={step} className={reached(status, step) ? 'on' : ''}>{workflowLabel[step]}</span>
        ))}
      </div>
      <div className="lx3-workspace">
        <nav className="lx3-side" aria-label="Herramientas del Studio">
          {tools.map((tool) => {
            const Icon = tool.icon
            return (
              <button key={tool.id} type="button" className={props.tool === tool.id ? 'active' : ''} aria-current={props.tool === tool.id ? 'true' : undefined} onClick={() => props.onTool(tool.id)}>
                <Icon size={16} /> {tool.label}
              </button>
            )
          })}
        </nav>
        <div className="lx3-stage">
          <div className={`lx3-device ${scale}`}>
            <div
              className="lx3-screen"
              style={{
                backgroundColor: props.draft.backgroundColor,
                color: props.draft.textColor || '#F4F7F8',
                backgroundImage: wallpaperPreview ? `linear-gradient(rgba(6,18,26,.45), rgba(6,18,26,.62)), url("${wallpaperPreview}")` : undefined,
                backgroundSize: props.draft.imageFit === 'contain' ? 'contain' : 'cover',
                backgroundPosition: 'center',
                backgroundRepeat: 'no-repeat',
              }}
            >
              <div className="lx3-brand">
                <span className="lx3-logo">
                  {logoPreview ? <img src={logoPreview} alt="" /> : (props.draft.companyName || 'T').slice(0, 1)}
                </span>
                <div>
                  <strong>{props.draft.companyName || 'Compania'}</strong>
                  <div className="lx3-note">{props.draft.welcomeMessage || 'Bienvenida'}</div>
                </div>
              </div>
              <div
                className="lx3-apps"
                style={{
                  gridTemplateColumns: `repeat(${props.draft.gridColumns || 4}, minmax(0, 1fr))`,
                  gap: props.draft.gridGap || 12,
                }}
              >
                {visible.length ? visible.map((app) => (
                  <div key={app.packageName || app.label} className="lx3-app">
                    <i style={{ width: Math.min(props.draft.iconSize || 56, 64), height: Math.min(props.draft.iconSize || 56, 64), background: props.draft.buttonColor }} aria-label="Marcador visual, no es el icono oficial">
                      {(app.label || app.packageName || '?').slice(0, 1).toUpperCase()}
                    </i>
                    <small>{app.label || app.packageName}</small>
                  </div>
                )) : <p className="lx3-note">Agrega aplicaciones visibles para completar la pantalla.</p>}
              </div>
              <small>{textOk ? 'Contraste de texto aceptable' : 'Contraste insuficiente'}</small>
            </div>
          </div>
        </div>
        <aside className="lx3-inspector" aria-label="Inspector">
          <h3>{tools.find((tool) => tool.id === props.tool)?.label}</h3>
          <Inspector {...props} secure={secure} />
        </aside>
      </div>
    </section>
  )
}

function Inspector(props: Props & { secure: boolean }) {
  if (props.tool === 'identity') {
    return (
      <>
        <Field label="Nombre del perfil"><input value={props.name} disabled={!props.canManage} onChange={(event) => props.onName(event.target.value)} /></Field>
        <Field label="Compania"><input value={props.draft.companyName} disabled={!props.canManage} onChange={(event) => props.onDraft({ companyName: event.target.value })} /></Field>
        <Field label="Mensaje"><input value={props.draft.welcomeMessage} disabled={!props.canManage} onChange={(event) => props.onDraft({ welcomeMessage: event.target.value })} /></Field>
        <Field label="Descripcion"><textarea value={props.description} disabled={!props.canManage} onChange={(event) => props.onDescription(event.target.value)} /></Field>
      </>
    )
  }
  if (props.tool === 'appearance') {
    return (
      <>
        <div className="lx3-actions">
          <button type="button" className="lx3-btn" disabled={!props.canManage} onClick={() => props.onDraft({ themePreset: 'corporate', primaryColor: '#16A36A', backgroundColor: '#0B1720', textColor: '#F4F7F8' })}>Corporativo</button>
          <button type="button" className="lx3-btn" disabled={!props.canManage} onClick={() => props.onDraft({ themePreset: 'dark', primaryColor: '#7DCEA0', backgroundColor: '#071015', textColor: '#F7FBF8' })}>Oscuro</button>
        </div>
        <ColorField label="Primario" value={props.draft.primaryColor} disabled={!props.canManage} onChange={(primaryColor) => props.onDraft({ primaryColor, themePreset: 'custom' })} />
        <ColorField label="Fondo" value={props.draft.backgroundColor} disabled={!props.canManage} onChange={(backgroundColor) => props.onDraft({ backgroundColor, themePreset: 'custom' })} />
        <ColorField label="Texto" value={props.draft.textColor || '#F4F7F8'} disabled={!props.canManage} onChange={(textColor) => props.onDraft({ textColor, themePreset: 'custom' })} />
        <ColorField label="Tarjeta" value={props.draft.cardColor || '#142831'} disabled={!props.canManage} onChange={(cardColor) => props.onDraft({ cardColor, themePreset: 'custom' })} />
        <Field label={`Borde ${props.draft.borderRadius ?? 18}`}><input type="range" min={0} max={32} disabled={!props.canManage} value={props.draft.borderRadius ?? 18} onChange={(event) => props.onDraft({ borderRadius: Number(event.target.value) })} /></Field>
        <Field label={`Sombra ${props.draft.shadowIntensity ?? 24}`}><input type="range" min={0} max={100} disabled={!props.canManage} value={props.draft.shadowIntensity ?? 24} onChange={(event) => props.onDraft({ shadowIntensity: Number(event.target.value) })} /></Field>
        <p className="lx3-note">{hasReadableContrast(props.draft.textColor || '#F4F7F8', props.draft.backgroundColor) ? 'Contraste WCAG aceptable.' : 'El contraste esta por debajo de 4.5:1.'}</p>
      </>
    )
  }
  if (props.tool === 'media') {
    return (
      <>
        <LauncherBrandingStudio draft={props.draft} disabled={!props.canManage} onChange={props.onDraft} showPreview={false} />
        <Field label="Ajuste">
          <select disabled={!props.canManage} value={props.draft.imageFit || 'cover'} onChange={(event) => props.onDraft({ imageFit: event.target.value as LauncherDraft['imageFit'] })}>
            <option value="cover">Cubrir</option>
            <option value="contain">Contener</option>
            <option value="center">Centrar</option>
          </select>
        </Field>
        <p className="lx3-note">El recorte libre no se guarda: el contrato solo admite cubrir, contener o centrar.</p>
      </>
    )
  }
  if (props.tool === 'apps') {
    return props.appsPanel ?? <AppsInspector {...props} />
  }
  if (props.tool === 'layout') {
    return (
      <>
        <Field label="Columnas">
          <select disabled={!props.canManage} value={props.draft.gridColumns || 4} onChange={(event) => props.onDraft({ gridColumns: Number(event.target.value) })}>
            <option value={3}>3</option>
            <option value={4}>4</option>
            <option value={5}>5</option>
          </select>
        </Field>
        <Field label="Icono"><input type="range" min={40} max={96} disabled={!props.canManage} value={props.draft.iconSize || 56} onChange={(event) => props.onDraft({ iconSize: Number(event.target.value) })} /></Field>
        <Field label="Espaciado"><input type="range" min={4} max={32} disabled={!props.canManage} value={props.draft.gridGap || 12} onChange={(event) => props.onDraft({ gridGap: Number(event.target.value) })} /></Field>
        <p className="lx3-note">Las carpetas no forman parte del contrato actual.</p>
      </>
    )
  }
  if (props.tool === 'config') {
    return (
      <>
        <Field label="Paquete"><input disabled={!props.canManage} value={props.draft.packageName} onChange={(event) => props.onDraft({ packageName: event.target.value })} /></Field>
        <Field label="Actividad HOME"><input disabled={!props.canManage} value={props.draft.activityName} onChange={(event) => props.onDraft({ activityName: event.target.value })} /></Field>
        <label className="lx3-field"><input type="checkbox" disabled={!props.canManage} checked={props.draft.requiredForSetup} onChange={(event) => props.onDraft({ requiredForSetup: event.target.checked })} /> Requerida en el aprovisionamiento</label>
      </>
    )
  }
  return (
    <>
      <p className="lx3-note">Guardar activa el perfil. Publicar lo envia a Google. Ninguno de esos pasos confirma que el telefono lo aplico.</p>
      <button type="button" className="lx3-btn primary" disabled={!props.canManage || props.working || !props.dirty} onClick={() => void props.onSave()}>Guardar y activar</button>
      <button type="button" className="lx3-btn" disabled={!props.canManage || props.working || props.dirty || !props.active} onClick={() => void props.onPublish()}>Publicar</button>
      <button type="button" className="lx3-btn" onClick={props.onDeployments}>Asignar dispositivos</button>
    </>
  )
}

function AppsInspector({ draft, canManage, onApplications, secure }: Props & { secure: boolean }) {
  const [manualPackage, setManualPackage] = useState('')
  const [manualLabel, setManualLabel] = useState('')
  function add(packageName: string, label: string) {
    if (draft.applications.some((app) => app.packageName.toLowerCase() === packageName.toLowerCase())) return
    onApplications([...draft.applications, { packageName, label, installType: 'AVAILABLE', visible: true, availability: 'manual' }])
  }
  return (
    <>
      {!secure && (
        <div className="lx3-block" role="status">
          Managed Google Play exige HTTPS. Abre TitanMDM en un origen seguro para buscar en el catalogo oficial. Mientras tanto puedes agregar un package name validado.
        </div>
      )}
      {secure && <ManagedGooglePlayPanel onSelect={(app) => add(app.packageName, app.packageName)} />}
      <Field label="Package name"><input value={manualPackage} disabled={!canManage} onChange={(event) => setManualPackage(event.target.value)} placeholder="com.empresa.app" /></Field>
      <Field label="Nombre"><input value={manualLabel} disabled={!canManage} onChange={(event) => setManualLabel(event.target.value)} /></Field>
      <button type="button" className="lx3-btn" disabled={!canManage || !/^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$/.test(manualPackage.trim())} onClick={() => add(manualPackage.trim(), manualLabel.trim() || manualPackage.trim())}>Agregar</button>
      {draft.applications.map((app, index) => (
        <article key={`${app.packageName}-${index}`} className="lx3-field">
          <strong>{app.label || app.packageName || 'Sin nombre'}</strong>
          <span>{app.packageName || 'Package pendiente'} · {app.visible ? 'Seleccionada' : 'Oculta'} · no instalada</span>
          <select disabled={!canManage} value={app.visible ? app.installType : 'BLOCKED'} onChange={(event) => {
            const value = event.target.value
            const next = draft.applications.map((item, current) => current === index ? {
              ...item,
              visible: value !== 'BLOCKED',
              installType: value === 'BLOCKED' ? item.installType : value as LauncherInstallType,
            } : item)
            onApplications(next)
          }}>
            <option value="AVAILABLE">Disponible</option>
            <option value="FORCE_INSTALLED">Obligatoria</option>
            <option value="REQUIRED_FOR_SETUP">Requerida en setup</option>
            <option value="BLOCKED">Bloqueada</option>
          </select>
        </article>
      ))}
      <p className="lx3-note">Seleccionar no aprueba, no publica ni instala. El icono oficial no viene en el evento del iframe.</p>
    </>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="lx3-field"><span>{label}</span>{children}</label>
}

function ColorField({ label, value, disabled, onChange }: { label: string; value: string; disabled: boolean; onChange: (value: string) => void }) {
  const safe = normalizeBrandingColor(value, '#16A36A')
  return (
    <label className="lx3-field">
      <span>{label}</span>
      <input type="color" disabled={disabled} value={safe} onChange={(event) => onChange(event.target.value.toUpperCase())} />
    </label>
  )
}

async function resolvePreview(value?: string): Promise<string | undefined> {
  if (!value) return undefined
  if (value.startsWith('https://')) return value
  const match = /^titan-asset:([0-9a-fA-F-]{36})$/.exec(value.trim())
  if (!match) return undefined
  try {
    const response = await apiClient.get<Blob>(`/android-enterprise/launcher-assets/${match[1]}`, { responseType: 'blob' })
    return URL.createObjectURL(response.data)
  } catch {
    return undefined
  }
}

function reached(current: ReturnType<typeof launcherWorkflow>, step: ReturnType<typeof launcherWorkflow>): boolean {
  const order = ['draft', 'dirty', 'saved', 'active', 'published', 'assigned', 'applying', 'applied']
  if (current === 'error') return step === 'error'
  return order.indexOf(current) >= order.indexOf(step)
}
