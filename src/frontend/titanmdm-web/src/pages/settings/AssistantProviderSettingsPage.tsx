import {
  Activity,
  CheckCircle2,
  Clock3,
  Cpu,
  RefreshCw,
  ShieldCheck,
  TriangleAlert,
  XCircle,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useState,
} from 'react'

import {
  titanAssistantApi,
  type TitanProviderConfiguration,
} from '../../assistant/api/titanAssistantApi'
import {
  SettingsPageHeader,
  SettingsSection,
  SettingsStatus,
} from '../../components/settings'

import './AssistantProviderSettingsPage.css'

function formatCheckedAt(value?: string | null): string {
  if (!value) {
    return 'Sin verificar'
  }

  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? 'Sin verificar'
    : date.toLocaleString()
}

export function AssistantProviderSettingsPage() {
  const [configuration, setConfiguration] =
    useState<TitanProviderConfiguration | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true)
    setError(null)

    try {
      const result =
        await titanAssistantApi.getProviderConfiguration(signal)
      setConfiguration(result)
    } catch (requestError) {
      if (signal?.aborted) {
        return
      }

      console.error(
        'No se pudo verificar la configuración IA.',
        requestError,
      )
      setError(
        'No fue posible verificar los proveedores. Revisa el acceso y la disponibilidad de la API.',
      )
    } finally {
      if (!signal?.aborted) {
        setLoading(false)
      }
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [load])

  return (
    <main className="titan-page ai-provider-settings">
      <SettingsPageHeader
        eyebrow="AI-2 · Administración"
        title="Proveedores de inteligencia artificial"
        description="Verifica la configuración efectiva y la conectividad de OpenRouter y Ollama sin exponer credenciales al navegador."
        icon={<Cpu size={17} />}
        status={
          <button
            type="button"
            className="ai-provider-settings__refresh"
            disabled={loading}
            onClick={() => void load()}
          >
            <RefreshCw
              size={16}
              className={loading ? 'ai-provider-settings__spin' : undefined}
            />
            {loading ? 'Verificando' : 'Verificar conexión'}
          </button>
        }
      />

      {error && (
        <SettingsStatus
          tone="error"
          icon={<TriangleAlert size={19} />}
          title="Verificación no disponible"
        >
          <span>{error}</span>
        </SettingsStatus>
      )}

      <SettingsStatus
        tone="neutral"
        icon={<ShieldCheck size={19} />}
        title="Credenciales protegidas"
      >
        <span>
          Las claves se configuran únicamente en el servidor mediante
          User Secrets, variables de entorno o el almacén seguro del
          despliegue. Esta pantalla nunca recibe ni modifica secretos.
        </span>
      </SettingsStatus>

      <SettingsSection
        title="Estado de proveedores"
        description="La disponibilidad procede de una comprobación real y los errores se muestran sanitizados."
      >
        <div className="ai-provider-settings__providers">
          {(configuration?.providers ?? []).map(provider => (
            <article
              key={provider.name}
              className={`ai-provider-card ai-provider-card--${
                provider.available
                  ? 'available'
                  : provider.enabled
                    ? 'unavailable'
                    : 'disabled'
              }`}
            >
              <header>
                <span className="ai-provider-card__icon">
                  {provider.available
                    ? <CheckCircle2 size={20} />
                    : <XCircle size={20} />}
                </span>
                <div>
                  <h2>{provider.name}</h2>
                  <p>
                    {provider.available
                      ? 'Disponible'
                      : provider.enabled
                        ? 'No disponible'
                        : 'Deshabilitado'}
                  </p>
                </div>
              </header>

              <dl>
                <div>
                  <dt>Modelo efectivo</dt>
                  <dd>{provider.model || 'No configurado'}</dd>
                </div>
                <div>
                  <dt>Última verificación</dt>
                  <dd>{formatCheckedAt(provider.checkedAtUtc)}</dd>
                </div>
                <div>
                  <dt>Latencia del probe</dt>
                  <dd>
                    {provider.latencyMs == null
                      ? 'No disponible'
                      : `${provider.latencyMs} ms`}
                  </dd>
                </div>
              </dl>

              {provider.detail && (
                <p className="ai-provider-card__detail">
                  {provider.detail}
                </p>
              )}
            </article>
          ))}

          {!loading && configuration?.providers.length === 0 && (
            <div className="ai-provider-settings__empty">
              No hay proveedores registrados en la API.
            </div>
          )}
        </div>
      </SettingsSection>

      {configuration && (
        <SettingsSection
          title="Política efectiva del asistente"
          description="Valores no secretos cargados por la API al iniciar el servicio."
        >
          <div className="ai-provider-settings__policy">
            <div>
              <Activity size={18} />
              <span>Proveedor preferido</span>
              <strong>{configuration.preferredProvider}</strong>
            </div>
            <div>
              <Clock3 size={18} />
              <span>Timeout por turno</span>
              <strong>{configuration.requestTimeoutSeconds} s</strong>
            </div>
            <div>
              <Cpu size={18} />
              <span>Límite de tokens</span>
              <strong>{configuration.maxTokens}</strong>
            </div>
            <div>
              <ShieldCheck size={18} />
              <span>Cuota por usuario</span>
              <strong>
                {configuration.maxTurnsPerWindow} /{' '}
                {configuration.quotaWindowSeconds} s
              </strong>
            </div>
          </div>
        </SettingsSection>
      )}

      <SettingsStatus
        tone="warning"
        icon={<TriangleAlert size={19} />}
        title="Configuración de servidor"
      >
        <span>
          Para producción usa `AI__OpenRouter__ApiKey`,
          `AI__OpenRouter__AssistantModel`, `Assistant__Provider`,
          `Assistant__Ollama__BaseUrl` y `Assistant__Ollama__Model`.
          Reinicia la API después de modificar estas variables.
        </span>
      </SettingsStatus>
    </main>
  )
}
