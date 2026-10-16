
import { useCallback, useEffect, useRef, useState } from 'react'
import { CheckCircle2, RefreshCw, ShieldCheck } from 'lucide-react'
import apiClient from '../../../api/apiClient'
import './ManagedGooglePlayPanel.css'

interface WebTokenResponse {
  token: string
  url: string
}

interface SelectedApp {
  packageName: string
  productId: string
}

interface GoogleFrame {
  register: (
    name: string,
    callback: (value: unknown) => void,
    filter: unknown,
  ) => void
  close?: () => void
}

interface GoogleIframes {
  CROSS_ORIGIN_IFRAMES_FILTER: unknown
  getContext: () => {
    openChild: (options: {
      url: string
      where: HTMLElement
      attributes: Record<string, string>
    }) => GoogleFrame
  }
}

interface GoogleApi {
  load: (
    name: string,
    callback: () => void,
  ) => void
  iframes: GoogleIframes
}

declare global {
  interface Window {
    gapi?: GoogleApi
  }
}

const scriptUrl = 'https://apis.google.com/js/api.js'

function loadGoogleApi(): Promise<GoogleApi> {
  return new Promise((resolve, reject) => {
    const ready = () => {
      if (window.gapi) resolve(window.gapi)
      else reject(new Error('Google API no disponible.'))
    }

    if (window.gapi) {
      ready()
      return
    }

    let script = document.querySelector<HTMLScriptElement>(
      `script[src="${scriptUrl}"]`,
    )

    if (!script) {
      script = document.createElement('script')
      script.src = scriptUrl
      script.async = true
      document.head.appendChild(script)
    }

    script.addEventListener('load', ready, { once: true })
    script.addEventListener(
      'error',
      () => reject(new Error('No se pudo cargar Google API.')),
      { once: true },
    )
  })
}

function extractSelectedApp(value: unknown): SelectedApp | null {
  if (typeof value !== 'object' || value === null) return null

  const data = value as Record<string, unknown>

  if (data.action !== 'selected') return null
  if (typeof data.packageName !== 'string') return null

  const packageName = data.packageName.trim()

  if (!/^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$/.test(packageName)) {
    return null
  }

  return {
    packageName,
    productId:
      typeof data.productId === 'string'
        ? data.productId
        : `app:${packageName}`,
  }
}

export function ManagedGooglePlayPanel({
  onSelect,
}: {
  onSelect?: (app: SelectedApp) => void
} = {}) {
  const containerRef = useRef<HTMLDivElement>(null)
  const frameRef = useRef<GoogleFrame | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<SelectedApp[]>([])

  const openCatalog = useCallback(async () => {
    if (!window.isSecureContext || location.protocol !== 'https:') {
      setError(
        'Managed Google Play requiere abrir TitanMDM mediante HTTPS.',
      )
      return
    }

    setLoading(true)
    setError(null)

    try {
      const response = await apiClient.post<WebTokenResponse>(
        '/android-enterprise/managed-play/web-token',
      )

      const iframeUrl = new URL(response.data.url)

      if (
        iframeUrl.origin !== 'https://play.google.com' ||
        iframeUrl.pathname !== '/work/embedded/search' ||
        iframeUrl.searchParams.get('mode') !== 'SELECT'
      ) {
        throw new Error('Google Play devolvió una URL inesperada.')
      }

      const gapi = await loadGoogleApi()
      const target = containerRef.current

      if (!target) return

      await new Promise<void>((resolve, reject) => {
        gapi.load('gapi.iframes', () => {
          try {
            if (!window.gapi?.iframes) {
              throw new Error('No se cargó el módulo de iframes.')
            }

            frameRef.current?.close?.()
            target.replaceChildren()

            const frame = gapi.iframes.getContext().openChild({
              url: iframeUrl.toString(),
              where: target,
              attributes: {
                style: 'width:100%;height:760px;border:0',
                scrolling: 'yes',
              },
            })

            frame.register(
              'onproductselect',
              (event: unknown) => {
                const app = extractSelectedApp(event)

                if (!app) return
                onSelect?.(app)

                setSelected(current =>
                  current.some(item => item.packageName === app.packageName)
                    ? current
                    : [...current, app],
                )
              },
              gapi.iframes.CROSS_ORIGIN_IFRAMES_FILTER,
            )

            frameRef.current = frame
            resolve()
          } catch (exception) {
            reject(exception)
          }
        })
      })
    } catch (exception) {
      setError(
        exception instanceof Error
          ? exception.message
          : 'No fue posible iniciar Managed Google Play.',
      )
    } finally {
      setLoading(false)
    }
  }, [onSelect])

  useEffect(() => {
    return () => {
      frameRef.current?.close?.()
      frameRef.current = null
    }
  }, [])

  const copyPackages = async () => {
    await navigator.clipboard.writeText(
      selected.map(app => app.packageName).join('\n'),
    )
  }

  return (
    <main className="managed-play">
      <header className="managed-play-header">
        <div>
          <span className="managed-play-eyebrow">
            ANDROID ENTERPRISE
          </span>
          <h1>Catálogo Managed Google Play</h1>
          <p>
            Selecciona aplicaciones empresariales para incorporarlas
            posteriormente a las políticas Android.
          </p>
        </div>

        <button
          type="button"
          disabled={loading}
          onClick={() => void openCatalog()}
        >
          <RefreshCw size={16} />
          {loading ? 'Conectando...' : 'Abrir catálogo'}
        </button>
      </header>

      {error && (
        <div className="managed-play-error" role="alert">
          {error}
        </div>
      )}

      <div className="managed-play-layout">
        <section className="managed-play-card">
          <div className="managed-play-section-title">
            <ShieldCheck size={18} />
            <strong>Google Play empresarial</strong>
          </div>

          <div ref={containerRef} className="managed-play-frame">
            {!loading && (
              <div className="managed-play-placeholder">
                Abre el catálogo para seleccionar aplicaciones
                públicas, privadas o aplicaciones web.
              </div>
            )}
          </div>
        </section>

        <aside className="managed-play-card">
          <div className="managed-play-section-title">
            <CheckCircle2 size={18} />
            <strong>Aplicaciones seleccionadas</strong>
          </div>

          <p>
            {selected.length} aplicaciones seleccionadas en esta sesión.
          </p>

          {selected.map(app => (
            <div className="managed-play-app" key={app.packageName}>
              <strong>{app.packageName}</strong>
              <button
                type="button"
                onClick={() =>
                  setSelected(current =>
                    current.filter(
                      item => item.packageName !== app.packageName,
                    ),
                  )
                }
              >
                Quitar
              </button>
            </div>
          ))}

          <button
            type="button"
            disabled={selected.length === 0}
            onClick={() => void copyPackages()}
          >
            Copiar paquetes seleccionados
          </button>

          <small>
            Seleccionar una aplicación no la instala ni publica una política.
            Este bloque únicamente captura los paquetes; el guardado
            persistente y su aplicación a dispositivos forman parte
            del siguiente cierre de integración.
          </small>
        </aside>
      </div>
    </main>
  )
}
