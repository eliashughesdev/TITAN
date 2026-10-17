import {
  FlaskConical,
  Pause,
  Play,
  Plus,
  RefreshCw,
  SlidersHorizontal,
  Workflow,
  XCircle,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useState,
} from 'react'

import {
  automationApi,
  type AutomationActionDescriptor,
  type AutomationEventDescriptor,
  type EnterpriseCreateRuleRequest,
  type EnterpriseRule,
} from '../../api/automationApi'

import type {
  DeviceListItem,
} from '../../types/device'

import {
  AutomationRuleBuilder,
} from './AutomationRuleBuilder'

import {
  AutomationSimulatorPanel,
} from './AutomationSimulatorPanel'

import {
  describeAutomationError,
  formatDate,
} from './automationUi'

interface AutomationEnterprisePanelProps {
  devices: DeviceListItem[]
}

export function AutomationEnterprisePanel({
  devices,
}: AutomationEnterprisePanelProps) {
  const [rules, setRules] =
    useState<EnterpriseRule[]>([])

  const [events, setEvents] =
    useState<AutomationEventDescriptor[]>([])

  const [actions, setActions] =
    useState<AutomationActionDescriptor[]>([])

  const [loading, setLoading] =
    useState(true)

  const [saving, setSaving] =
    useState(false)

  const [editorOpen, setEditorOpen] =
    useState(false)

  const [editing, setEditing] =
    useState<EnterpriseRule | null>(null)

  const [simulating, setSimulating] =
    useState<EnterpriseRule | null>(null)

  const [message, setMessage] =
    useState<string | null>(null)

  const [error, setError] =
    useState<string | null>(null)

  const fetchData = useCallback(
    () =>
      Promise.all([
        automationApi.getEnterpriseRules(),
        automationApi.getEvents(),
        automationApi.getActions(),
      ]),
    [],
  )

  const applyData = useCallback(
    ([ruleData, eventData, actionData]: [
      EnterpriseRule[],
      AutomationEventDescriptor[],
      AutomationActionDescriptor[],
    ]) => {
      setRules(ruleData)
      setEvents(eventData)
      setActions(actionData)
      setError(null)
    },
    [],
  )

  const applyError = useCallback(
    (loadError: unknown) => {
      console.error(
        'Automation enterprise error:',
        loadError,
      )

      setError(
        describeAutomationError(
          loadError,
          'No fue posible cargar las reglas empresariales.',
        ),
      )
    },
    [],
  )

  useEffect(() => {
    let active = true

    fetchData()
      .then((data) => {
        if (active) {
          applyData(data)
        }
      })
      .catch((loadError) => {
        if (active) {
          applyError(loadError)
        }
      })
      .finally(() => {
        if (active) {
          setLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [fetchData, applyData, applyError])

  const refresh = useCallback(
    () =>
      fetchData()
        .then((data) => {
          applyData(data)
        })
        .catch((loadError) => {
          applyError(loadError)
        })
        .finally(() => {
          setLoading(false)
        }),
    [fetchData, applyData, applyError],
  )

  function openCreate() {
    setEditing(null)
    setEditorOpen(true)
    setMessage(null)
    setError(null)
  }

  function openEdit(rule: EnterpriseRule) {
    setEditing(rule)
    setEditorOpen(true)
    setMessage(null)
    setError(null)
  }

  async function submitRule(
    request: EnterpriseCreateRuleRequest,
  ) {
    try {
      setSaving(true)
      setError(null)
      setMessage(null)

      if (editing) {
        await automationApi.updateEnterpriseRule(
          editing.id,
          request,
        )

        setMessage(
          'Regla empresarial actualizada.',
        )
      } else {
        await automationApi.createEnterpriseRule(
          request,
        )

        setMessage(
          'Regla empresarial creada.',
        )
      }

      setEditorOpen(false)
      setEditing(null)

      await refresh()
    } catch (saveError) {
      console.error(
        'Automation enterprise save error:',
        saveError,
      )

      setError(
        describeAutomationError(
          saveError,
          'No fue posible guardar la regla. Revisa la definición.',
        ),
      )
    } finally {
      setSaving(false)
    }
  }

  async function toggleRule(rule: EnterpriseRule) {
    try {
      setSaving(true)
      setError(null)

      await automationApi.updateEnterpriseRule(
        rule.id,
        {
          name: rule.name,
          description: rule.description,
          isEnabled: !rule.isEnabled,
          definition: rule.definition,
        },
      )

      setMessage(
        rule.isEnabled
          ? 'Regla pausada.'
          : 'Regla activada.',
      )

      await refresh()
    } catch (toggleError) {
      console.error(
        'Automation enterprise toggle error:',
        toggleError,
      )

      setError(
        describeAutomationError(
          toggleError,
          'No fue posible cambiar el estado.',
        ),
      )
    } finally {
      setSaving(false)
    }
  }

  if (editorOpen) {
    return (
      <AutomationRuleBuilder
        events={events}
        actions={actions}
        initial={editing}
        saving={saving}
        onCancel={() => {
          setEditorOpen(false)
          setEditing(null)
        }}
        onSubmit={(request) =>
          void submitRule(request)
        }
      />
    )
  }

  return (
    <section className="automation-enterprise">
      <header className="automation-panel-header">
        <div>
          <SlidersHorizontal size={18} />
          <div>
            <strong>
              Reglas empresariales
            </strong>
            <span>
              Motor avanzado: definiciones
              versionadas, políticas y simulación.
            </span>
          </div>
        </div>

        <div className="automation-panel-actions">
          <button
            type="button"
            className="automation-primary"
            onClick={openCreate}
          >
            <Plus size={15} />
            Nueva regla
          </button>

          <button
            type="button"
            className="automation-secondary"
            disabled={loading}
            onClick={() => {
              setLoading(true)
              void refresh()
            }}
          >
            <RefreshCw size={15} />
            Actualizar
          </button>
        </div>
      </header>

      {message && (
        <div className="automation-notice success">
          {message}
        </div>
      )}

      {error && (
        <div className="automation-notice error">
          <XCircle size={16} />
          {error}
        </div>
      )}

      {simulating && (
        <AutomationSimulatorPanel
          rule={simulating}
          devices={devices}
          onClose={() => setSimulating(null)}
        />
      )}

      {loading && rules.length === 0 ? (
        <div className="automation-panel-loading">
          Cargando reglas empresariales…
        </div>
      ) : rules.length === 0 ? (
        <div className="automation-empty">
          <Workflow size={30} />
          <strong>
            No existen reglas empresariales
          </strong>
          <span>
            Crea la primera regla con el motor
            avanzado (AUT-1).
          </span>
        </div>
      ) : (
        <div className="automation-enterprise-grid">
          {rules.map((rule) => (
            <article
              key={rule.id}
              className={
                rule.isEnabled
                  ? 'automation-rule'
                  : 'automation-rule disabled'
              }
            >
              <header>
                <div>
                  <span
                    className={
                      rule.isEnabled
                        ? 'automation-state active'
                        : 'automation-state paused'
                    }
                  >
                    {rule.isEnabled
                      ? 'ACTIVA'
                      : 'PAUSADA'}
                  </span>

                  <h3>{rule.name}</h3>

                  <p>
                    {rule.description ??
                      'Sin descripción'}
                  </p>
                </div>

                <Workflow size={20} />
              </header>

              <div className="automation-rule-flow">
                <div>
                  <span>MÓDULO</span>
                  <strong>
                    {rule.definition.module}
                  </strong>
                </div>

                <div>
                  <span>EVENTO</span>
                  <strong>
                    {rule.definition.trigger}
                  </strong>
                </div>

                <div>
                  <span>PRIORIDAD</span>
                  <strong>{rule.priority}</strong>
                </div>

                <div>
                  <span>ACCIONES</span>
                  <strong>
                    {rule.definition.actions.length}
                  </strong>
                </div>
              </div>

              <div className="automation-rule-meta">
                <span>
                  {rule.executionCount} ejecuciones
                </span>

                <span>
                  Fallos consecutivos:{' '}
                  {rule.consecutiveFailures}
                </span>

                <span>
                  {formatDate(
                    rule.lastExecutedAtUtc,
                  )}
                </span>
              </div>

              <footer>
                <button
                  type="button"
                  title="Editar"
                  onClick={() => openEdit(rule)}
                >
                  <SlidersHorizontal size={15} />
                </button>

                <button
                  type="button"
                  title={
                    rule.isEnabled
                      ? 'Pausar'
                      : 'Activar'
                  }
                  disabled={saving}
                  onClick={() => void toggleRule(rule)}
                >
                  {rule.isEnabled ? (
                    <Pause size={15} />
                  ) : (
                    <Play size={15} />
                  )}
                </button>

                <button
                  type="button"
                  className="automation-run"
                  onClick={() => {
                    setSimulating(rule)
                    setMessage(null)
                  }}
                >
                  <FlaskConical size={15} />
                  Simular
                </button>
              </footer>
            </article>
          ))}
        </div>
      )}
    </section>
  )
}
