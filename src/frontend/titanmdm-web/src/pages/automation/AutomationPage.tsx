import {
  Activity,
  CheckCircle2,
  Clock3,
  Edit3,
  History,
  MapPin,
  Pause,
  Play,
  RefreshCw,
  Save,
  Smartphone,
  Sparkles,
  Trash2,
  Workflow,
  XCircle,
  Zap,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

import {
  automationApi,
  type AutomationActionType,
  type AutomationExecution,
  type AutomationRule,
  type AutomationTriggerType,
} from '../../api/automationApi'

import {
  devicesApi,
} from '../../api/devicesApi'

import type {
  DeviceListItem,
} from '../../types/device'

import './AutomationPage.css'

interface TriggerOption {
  value: AutomationTriggerType
  label: string
  description: string
}

interface ActionOption {
  value: AutomationActionType
  label: string
  description: string
}

const triggerOptions: TriggerOption[] = [
  {
    value: 'Manual',
    label: 'Ejecución manual',
    description:
      'Ejecutada directamente por un administrador.',
  },
  {
    value: 'DeviceEnrolled',
    label: 'Dispositivo inscrito',
    description:
      'Cuando un nuevo dispositivo entra en TitanMDM.',
  },
  {
    value: 'DeviceOnline',
    label: 'Dispositivo en línea',
    description:
      'Cuando un dispositivo vuelve a comunicarse.',
  },
  {
    value: 'DeviceOffline',
    label: 'Dispositivo fuera de línea',
    description:
      'Cuando se detecta pérdida de comunicación.',
  },
  {
    value: 'DeviceNonCompliant',
    label: 'No conforme',
    description:
      'Cuando un dispositivo incumple una política.',
  },
  {
    value: 'SecurityRisk',
    label: 'Riesgo de seguridad',
    description:
      'Cuando TitanMDM detecta una condición de riesgo.',
  },
  {
    value: 'GeofenceEnter',
    label: 'Entrada a geocerca',
    description:
      'Cuando un dispositivo entra en una zona.',
  },
  {
    value: 'GeofenceExit',
    label: 'Salida de geocerca',
    description:
      'Cuando un dispositivo abandona una zona.',
  },
  {
    value: 'LowBattery',
    label: 'Batería baja',
    description:
      'Cuando el nivel de batería cumple la condición.',
  },
  {
    value: 'LostModeActivated',
    label: 'Lost Mode activado',
    description:
      'Cuando un dispositivo entra en modo perdido.',
  },
  {
    value: 'Schedule',
    label: 'Programación',
    description:
      'Automatización preparada para ejecución programada.',
  },
]

const actionOptions: ActionOption[] = [
  {
    value: 'RequestLocation',
    label: 'Solicitar ubicación',
    description:
      'Genera LOCATION_REQUEST para el dispositivo.',
  },
  {
    value: 'SecurityScan',
    label: 'Analizar seguridad',
    description:
      'Ejecuta SECURITY_STATUS.',
  },
  {
    value: 'ComplianceScan',
    label: 'Evaluar cumplimiento',
    description:
      'Ejecuta COMPLIANCE_CHECK.',
  },
  {
    value: 'AppInventory',
    label: 'Inventario de aplicaciones',
    description:
      'Solicita APP_INVENTORY.',
  },
  {
    value: 'EnableLostMode',
    label: 'Activar Lost Mode',
    description:
      'Activa el flujo de recuperación del dispositivo.',
  },
  {
    value: 'DisableLostMode',
    label: 'Desactivar Lost Mode',
    description:
      'Finaliza el flujo de Lost Mode.',
  },
  {
    value: 'SendCommand',
    label: 'Comando personalizado',
    description:
      'Envía un comando mediante Command Center.',
  },
  {
    value: 'Notification',
    label: 'Notificación',
    description:
      'Genera una acción de notificación.',
  },
  {
  value:
    'WindowsInventory',

  label:
    'Inventario Windows',

  description:
    'Actualiza el inventario completo del endpoint Windows.',
},

{
  value:
    'WindowsUpdateScan',

  label:
    'Buscar Windows Update',

  description:
    'Solicita una nueva búsqueda de actualizaciones.',
},

{
  value:
    'WindowsUpdateInstall',

  label:
    'Instalar Windows Update',

  description:
    'Instala las actualizaciones disponibles del endpoint.',
},

{
  value:
    'LockDevice',

  label:
    'Bloquear dispositivo',

  description:
    'Bloquea la sesión interactiva del endpoint Windows.',
},

{
  value:
    'RestartDevice',

  label:
    'Reiniciar dispositivo',

  description:
    'Solicita el reinicio remoto del endpoint.',
},
]

export function AutomationPage() {
  const [rules, setRules] =
    useState<AutomationRule[]>([])

  const [executions, setExecutions] =
    useState<AutomationExecution[]>([])

  const [devices, setDevices] =
    useState<DeviceListItem[]>([])

  const [name, setName] =
    useState('')

  const [description, setDescription] =
    useState('')

  const [triggerType, setTriggerType] =
    useState<AutomationTriggerType>(
      'DeviceNonCompliant',
    )

  const [actionType, setActionType] =
    useState<AutomationActionType>(
      'ComplianceScan',
    )

  const [conditionField, setConditionField] =
    useState('')

  const [conditionValue, setConditionValue] =
    useState('')

  const [commandType, setCommandType] =
    useState('PING')

  const [lostMessage, setLostMessage] =
    useState(
      'Este dispositivo está administrado por TitanMDM.',
    )

  const [lostPhone, setLostPhone] =
    useState('')

  const [notificationMessage,
    setNotificationMessage] =
    useState('')

  const [selectedDeviceId,
    setSelectedDeviceId] =
    useState('')

  const [editingRuleId,
    setEditingRuleId] =
    useState<string | null>(null)

  const [activeView, setActiveView] =
    useState<'rules' | 'history'>(
      'rules',
    )

  const [loading, setLoading] =
    useState(true)

  const [working, setWorking] =
    useState(false)

  const [message, setMessage] =
    useState<string | null>(null)

  const [error, setError] =
    useState<string | null>(null)

  const loadData =
    useCallback(async () => {
      try {
        setLoading(true)
        setError(null)

        const [
          ruleData,
          executionData,
          deviceData,
        ] = await Promise.all([
          automationApi.getRules(),

          automationApi.getExecutions(
            100,
          ),

          devicesApi.getDevices({
            page: 1,
            pageSize: 100,
          }),
        ])

        setRules(ruleData)
        setExecutions(executionData)
        setDevices(deviceData.items)
      } catch (loadError) {
        console.error(
          'Automation load error:',
          loadError,
        )

        setError(
          'No fue posible cargar Automation Engine.',
        )
      } finally {
        setLoading(false)
      }
    }, [])

  useEffect(() => {
    void loadData()
  }, [loadData])

  useEffect(() => {
    document.title =
      'Automation | TitanMDM'
  }, [])

  const enabledRules =
    useMemo(
      () =>
        rules.filter(
          (rule) =>
            rule.isEnabled,
        ).length,
      [rules],
    )

  const successfulExecutions =
    useMemo(
      () =>
        executions.filter(
          (execution) =>
            execution.status ===
            'Success',
        ).length,
      [executions],
    )

  const failedExecutions =
    useMemo(
      () =>
        executions.filter(
          (execution) =>
            execution.status ===
            'Failed',
        ).length,
      [executions],
    )

  const totalExecutionCount =
    useMemo(
      () =>
        rules.reduce(
          (total, rule) =>
            total +
            rule.executionCount,
          0,
        ),
      [rules],
    )

  function clearEditor() {
    setName('')
    setDescription('')

    setTriggerType(
      'DeviceNonCompliant',
    )

    setActionType(
      'ComplianceScan',
    )

    setConditionField('')
    setConditionValue('')
    setCommandType('PING')

    setLostMessage(
      'Este dispositivo está administrado por TitanMDM.',
    )

    setLostPhone('')
    setNotificationMessage('')
    setEditingRuleId(null)
  }

  function buildConditionsJson():
    string {
    if (
      !conditionField.trim() ||
      !conditionValue.trim()
    ) {
      return '{}'
    }

    return JSON.stringify({
      [conditionField.trim()]:
        conditionValue.trim(),
    })
  }

  function buildActionPayload():
    string {
    switch (actionType) {
      case 'SendCommand':
        return JSON.stringify({
          commandType,
          payloadJson: '{}',
        })

      case 'EnableLostMode':
        return JSON.stringify({
          message:
            lostMessage.trim() ||
            'Dispositivo administrado por TitanMDM.',

          phoneNumber:
            lostPhone.trim() ||
            null,
        })

      case 'Notification':
        return JSON.stringify({
          message:
            notificationMessage.trim() ||
            'Evento detectado por TitanMDM.',
        })

        case 'WindowsUpdateInstall':
          return JSON.stringify({
            payloadJson:
              JSON.stringify({
                kbArticleIds:
                  [],

                acceptEula:
                  true,

                downloadOnly:
                  false,
              }),
          })

      default:
        return '{}'
    }
  }

  async function saveRule() {
    if (!name.trim()) {
      setError(
        'La automatización necesita un nombre.',
      )

      return
    }

    try {
      setWorking(true)
      setError(null)
      setMessage(null)

      const baseRequest = {
        name: name.trim(),

        description:
          description.trim() ||
          null,

        triggerType,

        conditionsJson:
          buildConditionsJson(),

        actionType,

        actionPayloadJson:
          buildActionPayload(),
      }

      if (editingRuleId) {
        const current =
          rules.find(
            (rule) =>
              rule.id ===
              editingRuleId,
          )

        await automationApi.update(
          editingRuleId,
          {
            ...baseRequest,

            isEnabled:
              current?.isEnabled ??
              true,
          },
        )

        setMessage(
          'Automatización actualizada correctamente.',
        )
      } else {
        await automationApi.create(
          baseRequest,
        )

        setMessage(
          'Automatización creada correctamente.',
        )
      }

      clearEditor()

      await loadData()
    } catch (saveError) {
      console.error(
        'Automation save error:',
        saveError,
      )

      setError(
        'No fue posible guardar la automatización.',
      )
    } finally {
      setWorking(false)
    }
  }

  function editRule(
    rule: AutomationRule,
  ) {
    setEditingRuleId(
      rule.id,
    )

    setName(rule.name)

    setDescription(
      rule.description ?? '',
    )

    setTriggerType(
      rule.triggerType,
    )

    setActionType(
      rule.actionType,
    )

    try {
      const conditions =
        JSON.parse(
          rule.conditionsJson ||
            '{}',
        ) as Record<
          string,
          unknown
        >

      const first =
        Object.entries(
          conditions,
        )[0]

      setConditionField(
        first?.[0] ?? '',
      )

      setConditionValue(
        first?.[1] !== undefined
          ? String(first[1])
          : '',
      )
    } catch {
      setConditionField('')
      setConditionValue('')
    }

    try {
      const payload =
        JSON.parse(
          rule.actionPayloadJson ||
            '{}',
        ) as {
          commandType?: string
          message?: string
          phoneNumber?: string
        }

      if (payload.commandType) {
        setCommandType(
          payload.commandType,
        )
      }

      if (
        rule.actionType ===
        'EnableLostMode'
      ) {
        setLostMessage(
          payload.message ?? '',
        )

        setLostPhone(
          payload.phoneNumber ?? '',
        )
      }

      if (
        rule.actionType ===
        'Notification'
      ) {
        setNotificationMessage(
          payload.message ?? '',
        )
      }
    } catch {
      // Payload inválido:
      // mantenemos valores seguros.
    }

    window.scrollTo({
      top: 0,
      behavior: 'smooth',
    })
  }

  async function toggleRule(
    rule: AutomationRule,
  ) {
    try {
      setWorking(true)
      setError(null)

      await automationApi.setEnabled(
        rule.id,
        !rule.isEnabled,
      )

      setMessage(
        rule.isEnabled
          ? 'Automatización pausada.'
          : 'Automatización activada.',
      )

      await loadData()
    } catch {
      setError(
        'No fue posible cambiar el estado de la automatización.',
      )
    } finally {
      setWorking(false)
    }
  }

  async function executeRule(
    rule: AutomationRule,
  ) {
    const needsDevice =
      rule.actionType !==
      'Notification'

    if (
      needsDevice &&
      !selectedDeviceId
    ) {
      setError(
        'Selecciona un dispositivo para ejecutar la automatización.',
      )

      return
    }

    try {
      setWorking(true)
      setError(null)
      setMessage(null)

      await automationApi.execute(
        rule.id,
        {
          deviceId:
            selectedDeviceId ||
            null,

          triggerPayloadJson:
            '{}',
        },
      )

      setMessage(
        `Automatización "${rule.name}" ejecutada.`,
      )

      await loadData()
    } catch (executeError) {
      console.error(
        executeError,
      )

      setError(
        'No fue posible ejecutar la automatización.',
      )
    } finally {
      setWorking(false)
    }
  }

  async function deleteRule(
    rule: AutomationRule,
  ) {
    const confirmed =
      window.confirm(
        `¿Eliminar la automatización "${rule.name}"?`,
      )

    if (!confirmed) {
      return
    }

    try {
      setWorking(true)
      setError(null)

      await automationApi.delete(
        rule.id,
      )

      if (
        editingRuleId ===
        rule.id
      ) {
        clearEditor()
      }

      setMessage(
        'Automatización eliminada.',
      )

      await loadData()
    } catch {
      setError(
        'No fue posible eliminar la automatización.',
      )
    } finally {
      setWorking(false)
    }
  }

  return (
    <div className="automation-page">
      <header className="automation-header">
        <div>
          <span className="automation-eyebrow">
            TITANMDM AUTOMATION ENGINE
          </span>

          <h1>
            Automatización
          </h1>

          <p>
            Responde automáticamente
            a eventos de seguridad,
            cumplimiento, ubicación y
            administración de la flota.
          </p>
        </div>

        <button
          type="button"
          className="automation-secondary"
          disabled={loading}
          onClick={() =>
            void loadData()
          }
        >
          <RefreshCw size={16} />
          Actualizar
        </button>
      </header>

      {message && (
        <div className="automation-notice success">
          <CheckCircle2 size={17} />
          {message}
        </div>
      )}

      {error && (
        <div className="automation-notice error">
          <XCircle size={17} />
          {error}
        </div>
      )}

      <section className="automation-stats">
        <Stat
          icon={<Workflow size={20} />}
          label="Reglas"
          value={rules.length}
        />

        <Stat
          icon={<Zap size={20} />}
          label="Activas"
          value={enabledRules}
        />

        <Stat
          icon={<Activity size={20} />}
          label="Ejecuciones"
          value={totalExecutionCount}
        />

        <Stat
          icon={
            <CheckCircle2 size={20} />
          }
          label="Correctas"
          value={successfulExecutions}
        />

        <Stat
          icon={<XCircle size={20} />}
          label="Fallidas"
          value={failedExecutions}
        />
      </section>

      <section className="automation-builder">
        <div className="automation-builder-title">
          <div>
            <Sparkles size={20} />

            <div>
              <strong>
                {editingRuleId
                  ? 'Editar automatización'
                  : 'Nueva automatización'}
              </strong>

              <span>
                Constructor visual de reglas
              </span>
            </div>
          </div>

          {editingRuleId && (
            <button
              type="button"
              className="automation-secondary"
              onClick={clearEditor}
            >
              Cancelar edición
            </button>
          )}
        </div>

        <div className="automation-basic-fields">
          <label>
            Nombre
            <input
              value={name}
              onChange={(event) =>
                setName(
                  event.target.value,
                )
              }
              placeholder="Compliance Android crítico"
            />
          </label>

          <label>
            Descripción
            <input
              value={description}
              onChange={(event) =>
                setDescription(
                  event.target.value,
                )
              }
              placeholder="Respuesta automática de TitanMDM"
            />
          </label>
        </div>

        <div className="automation-flow">
          <FlowCard
            number="01"
            title="CUANDO"
            description="Evento que inicia la regla."
          >
            <select
              value={triggerType}
              onChange={(event) =>
                setTriggerType(
                  event.target.value as
                    AutomationTriggerType,
                )
              }
            >
              {triggerOptions.map(
                (option) => (
                  <option
                    key={option.value}
                    value={option.value}
                  >
                    {option.label}
                  </option>
                ),
              )}
            </select>

            <small>
              {
                triggerOptions.find(
                  (item) =>
                    item.value ===
                    triggerType,
                )?.description
              }
            </small>
          </FlowCard>

          <div className="automation-flow-arrow">
            →
          </div>

          <FlowCard
            number="02"
            title="SI"
            description="Condición opcional."
          >
            <input
              value={conditionField}
              onChange={(event) =>
                setConditionField(
                  event.target.value,
                )
              }
              placeholder="Ej: platform"
            />

            <input
              value={conditionValue}
              onChange={(event) =>
                setConditionValue(
                  event.target.value,
                )
              }
              placeholder="Ej: Android"
            />

            <small>
              Vacío = cualquier
              dispositivo/evento.
            </small>
          </FlowCard>

          <div className="automation-flow-arrow">
            →
          </div>

          <FlowCard
            number="03"
            title="ENTONCES"
            description="Acción ejecutada."
          >
            <select
              value={actionType}
              onChange={(event) =>
                setActionType(
                  event.target.value as
                    AutomationActionType,
                )
              }
            >
              {actionOptions.map(
                (option) => (
                  <option
                    key={option.value}
                    value={option.value}
                  >
                    {option.label}
                  </option>
                ),
              )}
            </select>

            <small>
              {
                actionOptions.find(
                  (item) =>
                    item.value ===
                    actionType,
                )?.description
              }
            </small>
          </FlowCard>
        </div>

        {actionType ===
          'SendCommand' && (
          <div className="automation-action-config">
            <label>
              Comando
              <select
                value={commandType}
                onChange={(event) =>
                  setCommandType(
                    event.target.value,
                  )
                }
              >
                <option value="PING">
                  PING
                </option>

                <option value="DEVICE_INFO">
                  DEVICE_INFO
                </option>

                <option value="APP_INVENTORY">
                  APP_INVENTORY
                </option>

                <option value="SECURITY_STATUS">
                  SECURITY_STATUS
                </option>

                <option value="COMPLIANCE_CHECK">
                  COMPLIANCE_CHECK
                </option>

                <option value="LOCATION_REQUEST">
                  LOCATION_REQUEST
                </option>
              </select>
            </label>
          </div>
        )}

        {actionType ===
          'EnableLostMode' && (
          <div className="automation-action-config two-columns">
            <label>
              Mensaje de recuperación
              <input
                value={lostMessage}
                onChange={(event) =>
                  setLostMessage(
                    event.target.value,
                  )
                }
              />
            </label>

            <label>
              Teléfono
              <input
                value={lostPhone}
                onChange={(event) =>
                  setLostPhone(
                    event.target.value,
                  )
                }
                placeholder="+1..."
              />
            </label>
          </div>
        )}

        {actionType ===
          'Notification' && (
          <div className="automation-action-config">
            <label>
              Mensaje
              <input
                value={
                  notificationMessage
                }
                onChange={(event) =>
                  setNotificationMessage(
                    event.target.value,
                  )
                }
                placeholder="TitanMDM detectó un evento..."
              />
            </label>
          </div>
        )}

        <div className="automation-builder-actions">
          <button
            type="button"
            className="automation-primary"
            disabled={working}
            onClick={() =>
              void saveRule()
            }
          >
            <Save size={16} />

            {editingRuleId
              ? 'Guardar cambios'
              : 'Crear automatización'}
          </button>
        </div>
      </section>

      <div className="automation-tabs">
        <button
          type="button"
          className={
            activeView === 'rules'
              ? 'active'
              : ''
          }
          onClick={() =>
            setActiveView('rules')
          }
        >
          <Workflow size={16} />
          Reglas
          <span>{rules.length}</span>
        </button>

        <button
          type="button"
          className={
            activeView === 'history'
              ? 'active'
              : ''
          }
          onClick={() =>
            setActiveView('history')
          }
        >
          <History size={16} />
          Historial
          <span>
            {executions.length}
          </span>
        </button>
      </div>

      {activeView === 'rules' && (
        <>
          <section className="automation-manual">
            <Smartphone size={18} />

            <div>
              <strong>
                Dispositivo para
                ejecución manual
              </strong>

              <span>
                Utilizado al pulsar
                Ejecutar.
              </span>
            </div>

            <select
              value={selectedDeviceId}
              onChange={(event) =>
                setSelectedDeviceId(
                  event.target.value,
                )
              }
            >
              <option value="">
                Seleccionar dispositivo
              </option>

              {devices.map(
                (device) => (
                  <option
                    key={device.id}
                    value={device.id}
                  >
                    {device.deviceName}
                    {' · '}
                    {device.platform}
                  </option>
                ),
              )}
            </select>
          </section>

          <section className="automation-rule-grid">
            {rules.length === 0 ? (
              <div className="automation-empty">
                <Workflow size={32} />

                <strong>
                  No existen automatizaciones
                </strong>

                <span>
                  Utiliza el constructor
                  superior para crear la
                  primera regla.
                </span>
              </div>
            ) : (
              rules.map(
                (rule) => (
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

                        <h3>
                          {rule.name}
                        </h3>

                        <p>
                          {rule.description ??
                            'Sin descripción'}
                        </p>
                      </div>

                      <Zap size={20} />
                    </header>

                    <div className="automation-rule-flow">
                      <div>
                        <span>CUANDO</span>
                        <strong>
                          {
                            triggerOptions.find(
                              (item) =>
                                item.value ===
                                rule.triggerType,
                            )?.label ??
                            rule.triggerType
                          }
                        </strong>
                      </div>

                      <div>
                        <span>ENTONCES</span>
                        <strong>
                          {
                            actionOptions.find(
                              (item) =>
                                item.value ===
                                rule.actionType,
                            )?.label ??
                            rule.actionType
                          }
                        </strong>
                      </div>
                    </div>

                    <div className="automation-rule-meta">
                      <span>
                        <Activity size={13} />
                        {
                          rule.executionCount
                        }{' '}
                        ejecuciones
                      </span>

                      <span>
                        <Clock3 size={13} />
                        {formatDate(
                          rule.lastExecutedAtUtc,
                        )}
                      </span>
                    </div>

                    <footer>
                      <button
                        type="button"
                        title="Editar"
                        onClick={() =>
                          editRule(rule)
                        }
                      >
                        <Edit3 size={15} />
                      </button>

                      <button
                        type="button"
                        title={
                          rule.isEnabled
                            ? 'Pausar'
                            : 'Activar'
                        }
                        disabled={working}
                        onClick={() =>
                          void toggleRule(
                            rule,
                          )
                        }
                      >
                        {rule.isEnabled ? (
                          <Pause
                            size={15}
                          />
                        ) : (
                          <Play
                            size={15}
                          />
                        )}
                      </button>

                      <button
                        type="button"
                        className="automation-run"
                        disabled={working}
                        onClick={() =>
                          void executeRule(
                            rule,
                          )
                        }
                      >
                        <Play size={15} />
                        Ejecutar
                      </button>

                      <button
                        type="button"
                        className="automation-delete"
                        disabled={working}
                        onClick={() =>
                          void deleteRule(
                            rule,
                          )
                        }
                      >
                        <Trash2 size={15} />
                      </button>
                    </footer>
                  </article>
                ),
              )
            )}
          </section>
        </>
      )}

      {activeView ===
        'history' && (
        <section className="automation-history">
          <header>
            <History size={18} />

            <div>
              <strong>
                Historial de ejecuciones
              </strong>

              <span>
                Últimas operaciones
                procesadas por el motor.
              </span>
            </div>
          </header>

          <div className="automation-table-wrapper">
            <table className="automation-table">
              <thead>
                <tr>
                  <th>
                    AUTOMATIZACIÓN
                  </th>
                  <th>
                    DISPOSITIVO
                  </th>
                  <th>
                    EVENTO
                  </th>
                  <th>
                    ESTADO
                  </th>
                  <th>
                    INICIO
                  </th>
                  <th>
                    RESULTADO
                  </th>
                </tr>
              </thead>

              <tbody>
                {executions.length ===
                0 ? (
                  <tr>
                    <td
                      colSpan={6}
                      className="automation-table-empty"
                    >
                      No existen ejecuciones.
                    </td>
                  </tr>
                ) : (
                  executions.map(
                    (execution) => (
                      <tr
                        key={
                          execution.id
                        }
                      >
                        <td>
                          <strong>
                            {
                              execution.automationName
                            }
                          </strong>
                        </td>

                        <td>
                          {execution.deviceName ??
                            'Global'}
                        </td>

                        <td>
                          {
                            execution.triggerType
                          }
                        </td>

                        <td>
                          <ExecutionStatus
                            status={
                              execution.status
                            }
                          />
                        </td>

                        <td>
                          {formatDate(
                            execution.startedAtUtc,
                          )}
                        </td>

                        <td>
                          <span className="automation-result">
                            {execution.errorMessage ??
                              execution.resultJson ??
                              '—'}
                          </span>
                        </td>
                      </tr>
                    ),
                  )
                )}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  )
}

function FlowCard({
  number,
  title,
  description,
  children,
}: {
  number: string
  title: string
  description: string
  children: ReactNode
}) {
  return (
    <article className="automation-flow-card">
      <span className="automation-step">
        {number}
      </span>

      <h3>{title}</h3>

      <p>{description}</p>

      {children}
    </article>
  )
}

function Stat({
  icon,
  label,
  value,
}: {
  icon: ReactNode
  label: string
  value: number
}) {
  return (
    <article className="automation-stat">
      <div>{icon}</div>

      <span>{label}</span>

      <strong>{value}</strong>
    </article>
  )
}

function ExecutionStatus({
  status,
}: {
  status: string
}) {
  const icon =
    status === 'Success' ? (
      <CheckCircle2 size={13} />
    ) : status === 'Failed' ? (
      <XCircle size={13} />
    ) : status === 'Running' ? (
      <RefreshCw size={13} />
    ) : (
      <MapPin size={13} />
    )

  return (
    <span
      className={
        `automation-execution-status ` +
        status.toLowerCase()
      }
    >
      {icon}
      {status}
    </span>
  )
}

function formatDate(
  value: string | null,
): string {
  if (!value) {
    return 'Nunca'
  }

  const date =
    new Date(value)

  if (
    Number.isNaN(
      date.getTime(),
    )
  ) {
    return 'N/D'
  }

  return date.toLocaleString()
}