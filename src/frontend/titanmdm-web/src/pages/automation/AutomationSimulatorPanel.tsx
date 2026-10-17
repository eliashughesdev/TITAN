import {
  FlaskConical,
  RefreshCw,
  X,
} from 'lucide-react'

import {
  useState,
} from 'react'

import {
  automationApi,
  type AutomationSimulationResult,
  type EnterpriseRule,
} from '../../api/automationApi'

import type {
  DeviceListItem,
} from '../../types/device'

import {
  describeAutomationError,
} from './automationUi'

interface AutomationSimulatorPanelProps {
  rule: EnterpriseRule
  devices: DeviceListItem[]
  onClose: () => void
}

export function AutomationSimulatorPanel({
  rule,
  devices,
  onClose,
}: AutomationSimulatorPanelProps) {
  const [deviceId, setDeviceId] =
    useState('')

  const [payloadJson, setPayloadJson] =
    useState('{}')

  const [result, setResult] =
    useState<AutomationSimulationResult | null>(
      null,
    )

  const [running, setRunning] =
    useState(false)

  const [error, setError] =
    useState<string | null>(null)

  async function run() {
    try {
      setRunning(true)
      setError(null)

      const simulation =
        await automationApi.simulate(rule.id, {
          deviceId: deviceId || null,
          triggerPayloadJson:
            payloadJson.trim() || '{}',
        })

      setResult(simulation)
    } catch (simulationError) {
      console.error(
        'Automation simulation error:',
        simulationError,
      )

      setError(
        describeAutomationError(
          simulationError,
          'No fue posible simular la regla.',
        ),
      )
    } finally {
      setRunning(false)
    }
  }

  return (
    <section className="automation-simulator">
      <header className="automation-panel-header">
        <div>
          <FlaskConical size={17} />
          <div>
            <strong>
              Simulador · {rule.name}
            </strong>
            <span>
              Evalúa condiciones y acciones sin
              ejecutar cambios reales.
            </span>
          </div>
        </div>

        <button
          type="button"
          className="automation-secondary"
          onClick={onClose}
        >
          <X size={15} />
          Cerrar
        </button>
      </header>

      {error && (
        <div className="automation-notice error">
          {error}
        </div>
      )}

      <div className="automation-simulator-form">
        <label>
          Dispositivo (opcional)
          <select
            value={deviceId}
            onChange={(event) =>
              setDeviceId(event.target.value)
            }
          >
            <option value="">
              Sin dispositivo
            </option>
            {devices.map((device) => (
              <option
                key={device.id}
                value={device.id}
              >
                {device.deviceName} ·{' '}
                {device.platform}
              </option>
            ))}
          </select>
        </label>

        <label>
          Payload del evento (JSON)
          <textarea
            value={payloadJson}
            onChange={(event) =>
              setPayloadJson(event.target.value)
            }
            rows={3}
          />
        </label>

        <button
          type="button"
          className="automation-primary"
          disabled={running}
          onClick={() => void run()}
        >
          <RefreshCw size={15} />
          Simular
        </button>
      </div>

      {result && (
        <div className="automation-simulator-result">
          <div className="automation-simulator-badges">
            <span
              className={
                result.matched
                  ? 'automation-state active'
                  : 'automation-state paused'
              }
            >
              {result.matched
                ? 'COINCIDE'
                : 'NO COINCIDE'}
            </span>

            <span className="automation-step">
              {result.plannedActions.length}{' '}
              acciones
            </span>
          </div>

          {result.warnings.length > 0 && (
            <ul className="automation-warning-list">
              {result.warnings.map(
                (warning, index) => (
                  <li key={index}>{warning}</li>
                ),
              )}
            </ul>
          )}

          <h4>Condiciones</h4>

          {result.conditionTraces.length === 0 ? (
            <div className="automation-inline-empty">
              Sin condiciones evaluadas.
            </div>
          ) : (
            <div className="automation-table-wrapper">
              <table className="automation-table">
                <thead>
                  <tr>
                    <th>CAMPO</th>
                    <th>OPERADOR</th>
                    <th>ESPERADO</th>
                    <th>REAL</th>
                    <th>RESULTADO</th>
                  </tr>
                </thead>
                <tbody>
                  {result.conditionTraces.map(
                    (trace, index) => (
                      <tr key={index}>
                        <td>{trace.field}</td>
                        <td>{trace.operator}</td>
                        <td>{trace.expected ?? '—'}</td>
                        <td>{trace.actual ?? '—'}</td>
                        <td>
                          {trace.passed
                            ? 'OK'
                            : 'FALSO'}
                        </td>
                      </tr>
                    ),
                  )}
                </tbody>
              </table>
            </div>
          )}

          <h4>Acciones planificadas</h4>

          {result.plannedActions.length === 0 ? (
            <div className="automation-inline-empty">
              Sin acciones planificadas.
            </div>
          ) : (
            <ul className="automation-planned-list">
              {result.plannedActions.map(
                (action) => (
                  <li key={`${action.code}-${action.order}`}>
                    <span
                      className={
                        action.valid
                          ? 'automation-state active'
                          : 'automation-state paused'
                      }
                    >
                      {action.order + 1}
                    </span>
                    <strong>{action.code}</strong>
                    {action.message && (
                      <small>{action.message}</small>
                    )}
                  </li>
                ),
              )}
            </ul>
          )}
        </div>
      )}
    </section>
  )
}
