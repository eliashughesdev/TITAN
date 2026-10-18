import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  ShieldAlert,
} from 'lucide-react'

import type {
  AndroidDeviceCapabilities,
  AndroidRemoteAction,
} from '../../../../types/androidEnterprise'

import {
  androidActionCatalog,
  capabilityStatusLabels,
  findCapability,
  isActionEnabled,
} from './androidRemoteConsole.utils'

import type {
  AndroidActionCategory,
  AndroidActionDefinition,
} from './androidRemoteConsole.types'

interface Props {
  capabilities: AndroidDeviceCapabilities | null
  canExecute: boolean
  busyAction: AndroidRemoteAction | null
  onExecute: (action: AndroidRemoteAction) => void
}

const categoryLabels: Record<AndroidActionCategory, string> = {
  security: 'Seguridad',
  device: 'Dispositivo',
  recovery: 'Recuperación',
  compliance: 'Cumplimiento',
  destructive: 'Acciones destructivas',
}

const categoryOrder: AndroidActionCategory[] = [
  'security',
  'device',
  'recovery',
  'compliance',
  'destructive',
]

function toneClass(def: AndroidActionDefinition): string {
  return `arc-action arc-action--${def.tone}`
}

export function AndroidCommandPanel({
  capabilities,
  canExecute,
  busyAction,
  onExecute,
}: Props) {
  return (
    <section className="arc-card" aria-label="Comandos administrativos">
      <header className="arc-card__head">
        <div>
          <h3>Comandos administrativos</h3>
          <p>
            Operaciones evaluadas contra el modo de administración, la
            versión Android y el fabricante.
          </p>
        </div>
      </header>

      {!canExecute && (
        <div className="arc-inline-note arc-inline-note--warning">
          <ShieldAlert size={16} />
          No tienes el permiso android.enterprise.actions para emitir
          operaciones.
        </div>
      )}

      <div className="arc-command-groups">
        {categoryOrder.map(category => {
          const definitions = androidActionCatalog.filter(
            item => item.category === category,
          )

          if (definitions.length === 0) return null

          return (
            <div className="arc-command-group" key={category}>
              <h4>
                {category === 'destructive' && (
                  <AlertTriangle size={14} />
                )}
                {categoryLabels[category]}
              </h4>

              <div className="arc-command-grid">
                {definitions.map(definition => {
                  const view = findCapability(
                    capabilities,
                    definition.action,
                  )
                  const enabled = isActionEnabled(
                    view,
                    canExecute,
                    busyAction !== null,
                  )
                  const Icon = definition.icon

                  return (
                    <button
                      type="button"
                      key={definition.action}
                      className={toneClass(definition)}
                      disabled={!enabled}
                      title={view.reason ?? definition.description}
                      onClick={() => onExecute(definition.action)}
                    >
                      <Icon size={17} />

                      <span className="arc-action__text">
                        <strong>
                          {busyAction === definition.action
                            ? 'Emitiendo...'
                            : definition.label}
                        </strong>
                        <small>{definition.description}</small>
                      </span>

                      <span
                        className={`arc-badge arc-badge--${view.status.toLowerCase()}`}
                      >
                        {capabilityStatusLabels[view.status]}
                      </span>
                    </button>
                  )
                })}
              </div>
            </div>
          )
        })}
      </div>

      <div className="arc-command-legend">
        <span className="arc-legend-item">
          <CheckCircle2 size={13} /> Compatible
        </span>
        <span className="arc-legend-item">
          <Clock3 size={13} /> Pendiente de validación
        </span>
        <span className="arc-legend-item">
          <AlertTriangle size={13} /> No compatible
        </span>
      </div>

      <p className="arc-advisory">
        Una solicitud aceptada por Google no confirma que el dispositivo
        ya ejecutó la operación. El resultado real se verifica con la
        próxima sincronización de estado.
      </p>
    </section>
  )
}
