import {
  RefreshCw,
  ScrollText,
} from 'lucide-react'

import type {
  AndroidCommandEntry,
} from './androidRemoteConsole.types'

import {
  commandStateLabels,
  formatConsoleDate,
} from './androidRemoteConsole.utils'

interface Props {
  entries: AndroidCommandEntry[]
  loading: boolean
  onRefresh: () => void
}

function stateClass(state: AndroidCommandEntry['state']): string {
  return `arc-state arc-state--${state}`
}

export function AndroidOperationsHistory({
  entries,
  loading,
  onRefresh,
}: Props) {
  return (
    <section className="arc-card" aria-label="Historial de operaciones">
      <header className="arc-card__head">
        <div>
          <h3>Historial de operaciones</h3>
          <p>Auditoría por operador, dispositivo y acción.</p>
        </div>
        <button
          type="button"
          className="arc-ghost-button"
          onClick={onRefresh}
          disabled={loading}
        >
          <RefreshCw size={14} />
          Actualizar
        </button>
      </header>

      {entries.length === 0 ? (
        <div className="arc-empty">
          <ScrollText size={26} />
          Sin operaciones registradas para este dispositivo.
        </div>
      ) : (
        <div className="arc-history-scroll">
          <table className="arc-history">
            <thead>
              <tr>
                <th>Acción</th>
                <th>Estado</th>
                <th>Operador</th>
                <th>Fecha</th>
                <th>Correlación</th>
              </tr>
            </thead>
            <tbody>
              {entries.map(entry => (
                <tr key={entry.id}>
                  <td>
                    <strong>{entry.label}</strong>
                    {entry.detail && <small>{entry.detail}</small>}
                  </td>
                  <td>
                    <span className={stateClass(entry.state)}>
                      {commandStateLabels[entry.state]}
                    </span>
                  </td>
                  <td>{entry.actor ?? 'Sistema'}</td>
                  <td>{formatConsoleDate(entry.requestedAtUtc)}</td>
                  <td>
                    <small>
                      {entry.correlationId ?? 'No disponible'}
                    </small>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
