import {
  Activity,
  Eye,
  MonitorSmartphone,
  ShieldAlert,
} from 'lucide-react'

import type {
  AndroidDeviceCapabilities,
} from '../../../../types/androidEnterprise'

import type {
  LucideIcon,
} from 'lucide-react'

interface Props {
  capabilities: AndroidDeviceCapabilities | null
}

interface SupportTier {
  id: string
  label: string
  description: string
  icon: LucideIcon
  supported: boolean
  note: string
}

export function AndroidRemoteSupportPanel({
  capabilities,
}: Props) {
  const diagnostics = capabilities?.remoteDiagnosticsSupported ?? true
  const vision = capabilities?.remoteVisionSupported ?? false
  const control = capabilities?.remoteControlSupported ?? false

  const tiers: SupportTier[] = [
    {
      id: 'diagnostic',
      label: 'A. Diagnóstico remoto',
      description:
        'Inventario, política aplicada, cumplimiento y auditoría ' +
        'sin acceso a la pantalla del usuario.',
      icon: Activity,
      supported: diagnostics,
      note: diagnostics
        ? 'Disponible mediante Android Management API.'
        : 'No disponible para este dispositivo.',
    },
    {
      id: 'vision',
      label: 'B. Visualización autorizada',
      description:
        'Captura de pantalla mediante MediaProjection con ' +
        'consentimiento explícito y visible del usuario.',
      icon: Eye,
      supported: vision,
      note: vision
        ? 'Disponible.'
        : 'Pendiente: requiere agente Android con MediaProjection ' +
          'y consentimiento del usuario. No se implementa captura oculta.',
    },
    {
      id: 'control',
      label: 'C. Control interactivo compatible',
      description:
        'Entrada remota limitada al consentimiento otorgado por ' +
        'el usuario en el dispositivo.',
      icon: MonitorSmartphone,
      supported: control,
      note: control
        ? 'Disponible.'
        : 'Pendiente: el control interactivo desatendido universal ' +
          'no está soportado y no se ofrece.',
    },
  ]

  return (
    <section className="arc-card" aria-label="Soporte remoto Android">
      <header className="arc-card__head">
        <div>
          <h3>Soporte remoto</h3>
          <p>
            Capacidades separadas por nivel de autorización. Sin captura
            secreta ni control oculto.
          </p>
        </div>
      </header>

      <div className="arc-support-tiers">
        {tiers.map(tier => {
          const Icon = tier.icon

          return (
            <article
              className={
                tier.supported
                  ? 'arc-support-tier arc-support-tier--on'
                  : 'arc-support-tier arc-support-tier--off'
              }
              key={tier.id}
            >
              <Icon size={18} />
              <div>
                <strong>{tier.label}</strong>
                <p>{tier.description}</p>
                <small>{tier.note}</small>
              </div>
            </article>
          )
        })}
      </div>

      {capabilities?.alternativeRouteNote && (
        <div className="arc-inline-note arc-inline-note--warning">
          <ShieldAlert size={16} />
          {capabilities.alternativeRouteNote}
        </div>
      )}

      {capabilities?.remoteSupportNote && (
        <p className="arc-advisory">{capabilities.remoteSupportNote}</p>
      )}
    </section>
  )
}
