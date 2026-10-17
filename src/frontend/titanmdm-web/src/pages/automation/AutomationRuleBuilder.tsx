import {
  ArrowDown,
  ArrowUp,
  Plus,
  Save,
  Trash2,
  X,
} from 'lucide-react'

import {
  useState,
} from 'react'

import type {
  AutomationActionDescriptor,
  AutomationActionDto,
  AutomationConditionDto,
  AutomationConditionGroupDto,
  AutomationEventDescriptor,
  AutomationPoliciesDto,
  AutomationRuleDefinition,
  AutomationScheduleDefinitionDto,
  EnterpriseCreateRuleRequest,
  EnterpriseRule,
} from '../../api/automationApi'

const moduleOptions = [
  'general',
  'helpdesk',
  'windows',
  'android',
  'ponches',
  'inventory',
  'compliance',
  'notifications',
]

const operatorOptions = [
  'Eq',
  'NotEq',
  'GreaterThan',
  'GreaterThanOrEqual',
  'LessThan',
  'LessThanOrEqual',
  'Contains',
  'NotContains',
  'StartsWith',
  'EndsWith',
  'In',
  'NotIn',
  'Exists',
  'NotExists',
  'IsEmpty',
  'IsNotEmpty',
]

const errorPolicyOptions = [
  'StopRule',
  'ContinueActions',
  'DisableRuleAfterFailures',
]

const concurrencyOptions = [
  'Parallel',
  'SkipIfRunning',
  'Queue',
]

const recurrenceOptions = [
  'Once',
  'Hourly',
  'Daily',
  'Weekly',
  'Monthly',
  'Interval',
]

const weekDays = [
  'Domingo',
  'Lunes',
  'Martes',
  'Miércoles',
  'Jueves',
  'Viernes',
  'Sábado',
]

const defaultPolicies: AutomationPoliciesDto = {
  errorPolicy: 'StopRule',
  disableAfterFailures: 5,
  maxRetries: 2,
  retryBackoffSeconds: 30,
  timeoutSeconds: 60,
  concurrencyPolicy: 'SkipIfRunning',
  minSecondsBetweenExecutions: 0,
}

const defaultSchedule: AutomationScheduleDefinitionDto =
  {
    recurrence: 'Daily',
    intervalMinutes: 60,
    timeOfDay: '08:00',
    dayOfWeek: 1,
    dayOfMonth: 1,
    timeZoneId: 'UTC',
    startAtUtc: null,
    catchUp: false,
  }

interface AutomationRuleBuilderProps {
  events: AutomationEventDescriptor[]
  actions: AutomationActionDescriptor[]
  initial?: EnterpriseRule | null
  saving: boolean
  onCancel: () => void
  onSubmit: (
    request: EnterpriseCreateRuleRequest,
  ) => void
}

export function AutomationRuleBuilder({
  events,
  actions,
  initial,
  saving,
  onCancel,
  onSubmit,
}: AutomationRuleBuilderProps) {
  const [name, setName] = useState(
    initial?.name ?? '',
  )

  const [description, setDescription] =
    useState(initial?.description ?? '')

  const [isEnabled, setIsEnabled] =
    useState(initial?.isEnabled ?? true)

  const [module, setModule] = useState(
    initial?.definition.module ?? 'general',
  )

  const [priority, setPriority] = useState(
    initial?.definition.priority ?? 100,
  )

  const [trigger, setTrigger] = useState(
    initial?.definition.trigger ?? 'Manual',
  )

  const [groupOperator, setGroupOperator] =
    useState<'All' | 'Any'>(
      initial?.definition.conditions.operator ===
        'Any'
        ? 'Any'
        : 'All',
    )

  const [conditions, setConditions] = useState<
    AutomationConditionDto[]
  >(
    initial?.definition.conditions.conditions ??
      [],
  )

  const preservedGroups =
    initial?.definition.conditions.groups ?? []

  const [ruleActions, setRuleActions] =
    useState<AutomationActionDto[]>(
      initial?.definition.actions ?? [],
    )

  const [policies, setPolicies] =
    useState<AutomationPoliciesDto>(
      initial?.definition.policies ??
        defaultPolicies,
    )

  const [scheduleEnabled, setScheduleEnabled] =
    useState(
      initial?.definition.schedule !== null &&
        initial?.definition.schedule !==
          undefined,
    )

  const [schedule, setSchedule] =
    useState<AutomationScheduleDefinitionDto>(
      initial?.definition.schedule ??
        defaultSchedule,
    )

  const [scopeSiteIds, setScopeSiteIds] =
    useState(
      (initial?.definition.scopeSiteIds ?? []).join(
        ', ',
      ),
    )

  const [validationError, setValidationError] =
    useState<string | null>(null)

  const enabledActions = actions.filter(
    (action) => action.enabled,
  )

  function findActionDescriptor(
    code: string,
  ): AutomationActionDescriptor | undefined {
    return actions.find(
      (action) => action.code === code,
    )
  }

  function addCondition() {
    setConditions((current) => [
      ...current,
      {
        field: '',
        operator: 'Eq',
        value: '',
        values: null,
      },
    ])
  }

  function updateCondition(
    index: number,
    patch: Partial<AutomationConditionDto>,
  ) {
    setConditions((current) =>
      current.map((condition, position) =>
        position === index
          ? { ...condition, ...patch }
          : condition,
      ),
    )
  }

  function removeCondition(index: number) {
    setConditions((current) =>
      current.filter(
        (_, position) => position !== index,
      ),
    )
  }

  function addAction(code: string) {
    const descriptor = findActionDescriptor(code)

    const parameters: Record<
      string,
      string | null
    > = {}

    descriptor?.parameters.forEach((parameter) => {
      parameters[parameter.name] =
        parameter.defaultValue ?? ''
    })

    setRuleActions((current) => [
      ...current,
      {
        code,
        order: current.length,
        parameters,
        continueOnError: false,
      },
    ])
  }

  function updateAction(
    index: number,
    patch: Partial<AutomationActionDto>,
  ) {
    setRuleActions((current) =>
      current.map((action, position) =>
        position === index
          ? { ...action, ...patch }
          : action,
      ),
    )
  }

  function updateActionParameter(
    index: number,
    parameterName: string,
    value: string,
  ) {
    setRuleActions((current) =>
      current.map((action, position) =>
        position === index
          ? {
              ...action,
              parameters: {
                ...action.parameters,
                [parameterName]: value,
              },
            }
          : action,
      ),
    )
  }

  function changeActionCode(
    index: number,
    code: string,
  ) {
    const descriptor = findActionDescriptor(code)

    const parameters: Record<
      string,
      string | null
    > = {}

    descriptor?.parameters.forEach((parameter) => {
      parameters[parameter.name] =
        parameter.defaultValue ?? ''
    })

    updateAction(index, {
      code,
      parameters,
    })
  }

  function removeAction(index: number) {
    setRuleActions((current) =>
      current
        .filter((_, position) => position !== index)
        .map((action, position) => ({
          ...action,
          order: position,
        })),
    )
  }

  function moveAction(
    index: number,
    direction: -1 | 1,
  ) {
    setRuleActions((current) => {
      const target = index + direction

      if (
        target < 0 ||
        target >= current.length
      ) {
        return current
      }

      const next = [...current]

      const [item] = next.splice(index, 1)
      next.splice(target, 0, item)

      return next.map((action, position) => ({
        ...action,
        order: position,
      }))
    })
  }

  function submit() {
    setValidationError(null)

    if (!name.trim()) {
      setValidationError(
        'La automatización necesita un nombre.',
      )
      return
    }

    if (ruleActions.length === 0) {
      setValidationError(
        'Agrega al menos una acción.',
      )
      return
    }

    if (trigger === 'Schedule' && !scheduleEnabled) {
      setValidationError(
        'El disparador Programación requiere una programación.',
      )
      return
    }

    const cleanConditions = conditions
      .filter(
        (condition) =>
          condition.field.trim().length > 0,
      )
      .map((condition) => ({
        field: condition.field.trim(),
        operator: condition.operator,
        value:
          condition.operator === 'In' ||
          condition.operator === 'NotIn'
            ? null
            : (condition.value ?? ''),
        values:
          condition.operator === 'In' ||
          condition.operator === 'NotIn'
            ? (condition.value ?? '')
                .split(',')
                .map((item) => item.trim())
                .filter((item) => item.length > 0)
            : null,
      }))

    const rootGroup: AutomationConditionGroupDto = {
      operator: groupOperator,
      conditions: cleanConditions,
      groups: preservedGroups,
    }

    const definition: AutomationRuleDefinition = {
      version: 1,
      module,
      trigger,
      priority,
      conditions: rootGroup,
      actions: ruleActions.map((action, index) => ({
        code: action.code,
        order: index,
        parameters: action.parameters,
        continueOnError: action.continueOnError,
      })),
      policies,
      schedule: scheduleEnabled ? schedule : null,
      scopeSiteIds: scopeSiteIds
        .split(',')
        .map((item) => item.trim())
        .filter(
          (item) =>
            /^[0-9a-fA-F-]{36}$/.test(item),
        ),
    }

    onSubmit({
      name: name.trim(),
      description:
        description.trim().length > 0
          ? description.trim()
          : null,
      isEnabled,
      definition,
    })
  }

  const scheduleIsInterval =
    schedule.recurrence === 'Interval'

  const scheduleNeedsTime =
    schedule.recurrence === 'Daily' ||
    schedule.recurrence === 'Weekly' ||
    schedule.recurrence === 'Monthly'

  return (
    <section className="automation-builder automation-enterprise-builder">
      <div className="automation-builder-title">
        <div>
          <Save size={19} />
          <div>
            <strong>
              {initial
                ? 'Editar regla empresarial'
                : 'Nueva regla empresarial'}
            </strong>
            <span>
              Definición versionada (AND/OR +
              acciones ordenadas + políticas)
            </span>
          </div>
        </div>

        <button
          type="button"
          className="automation-secondary"
          onClick={onCancel}
        >
          <X size={15} />
          Cancelar
        </button>
      </div>

      {validationError && (
        <div className="automation-notice error">
          {validationError}
        </div>
      )}

      <div className="automation-basic-fields">
        <label>
          Nombre
          <input
            value={name}
            onChange={(event) =>
              setName(event.target.value)
            }
            placeholder="Respuesta a incumplimiento crítico"
          />
        </label>

        <label>
          Descripción
          <input
            value={description}
            onChange={(event) =>
              setDescription(event.target.value)
            }
            placeholder="Qué resuelve esta regla"
          />
        </label>

        <label>
          Módulo
          <select
            value={module}
            onChange={(event) =>
              setModule(event.target.value)
            }
          >
            {moduleOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </label>

        <label>
          Prioridad (0-1000)
          <input
            type="number"
            min={0}
            max={1000}
            value={priority}
            onChange={(event) =>
              setPriority(
                Number(event.target.value) || 0,
              )
            }
          />
        </label>

        <label>
          Estado
          <select
            value={isEnabled ? 'on' : 'off'}
            onChange={(event) =>
              setIsEnabled(
                event.target.value === 'on',
              )
            }
          >
            <option value="on">Activa</option>
            <option value="off">Pausada</option>
          </select>
        </label>

        <label>
          Sitios (IDs separados por coma)
          <input
            value={scopeSiteIds}
            onChange={(event) =>
              setScopeSiteIds(event.target.value)
            }
            placeholder="Vacío = toda la organización"
          />
        </label>
      </div>

      <div className="automation-enterprise-section">
        <h3>CUANDO (evento disparador)</h3>

        <select
          value={trigger}
          onChange={(event) =>
            setTrigger(event.target.value)
          }
        >
          {events.map((event) => (
            <option
              key={event.code}
              value={event.code}
              disabled={!event.connected}
            >
              {event.label} ({event.code})
              {event.connected ? '' : ' — no conectado'}
            </option>
          ))}
        </select>

        <small>
          {
            events.find(
              (event) => event.code === trigger,
            )?.description
          }
        </small>
      </div>

      <div className="automation-enterprise-section">
        <div className="automation-section-head">
          <h3>SI (condiciones)</h3>

          <div className="automation-section-actions">
            <select
              value={groupOperator}
              onChange={(event) =>
                setGroupOperator(
                  event.target.value as
                    | 'All'
                    | 'Any',
                )
              }
            >
              <option value="All">
                Todas (AND)
              </option>
              <option value="Any">
                Al menos una (OR)
              </option>
            </select>

            <button
              type="button"
              className="automation-secondary"
              onClick={addCondition}
            >
              <Plus size={14} />
              Condición
            </button>
          </div>
        </div>

        {conditions.length === 0 ? (
          <div className="automation-inline-empty">
            Sin condiciones: la regla se ejecuta
            para cualquier dispositivo del evento.
          </div>
        ) : (
          <div className="automation-condition-list">
            {conditions.map((condition, index) => (
              <div
                key={index}
                className="automation-condition-row"
              >
                <input
                  value={condition.field}
                  onChange={(event) =>
                    updateCondition(index, {
                      field: event.target.value,
                    })
                  }
                  placeholder="campo (ej: platform)"
                />

                <select
                  value={condition.operator}
                  onChange={(event) =>
                    updateCondition(index, {
                      operator: event.target.value,
                    })
                  }
                >
                  {operatorOptions.map((option) => (
                    <option
                      key={option}
                      value={option}
                    >
                      {option}
                    </option>
                  ))}
                </select>

                <input
                  value={condition.value ?? ''}
                  onChange={(event) =>
                    updateCondition(index, {
                      value: event.target.value,
                    })
                  }
                  placeholder={
                    condition.operator === 'In' ||
                    condition.operator ===
                      'NotIn'
                      ? 'valor1, valor2'
                      : 'valor'
                  }
                  disabled={
                    condition.operator === 'Exists' ||
                    condition.operator ===
                      'NotExists' ||
                    condition.operator ===
                      'IsEmpty' ||
                    condition.operator ===
                      'IsNotEmpty'
                  }
                />

                <button
                  type="button"
                  className="automation-delete"
                  onClick={() =>
                    removeCondition(index)
                  }
                >
                  <Trash2 size={14} />
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="automation-enterprise-section">
        <div className="automation-section-head">
          <h3>ENTONCES (acciones)</h3>

          <select
            value=""
            onChange={(event) => {
              if (event.target.value) {
                addAction(event.target.value)
              }
            }}
          >
            <option value="">Agregar acción…</option>
            {enabledActions.map((action) => (
              <option
                key={action.code}
                value={action.code}
              >
                {action.label} ({action.code})
              </option>
            ))}
          </select>
        </div>

        {ruleActions.length === 0 ? (
          <div className="automation-inline-empty">
            Agrega al menos una acción.
          </div>
        ) : (
          <div className="automation-action-list">
            {ruleActions.map((action, index) => {
              const descriptor =
                findActionDescriptor(action.code)

              return (
                <div
                  key={`${action.code}-${index}`}
                  className="automation-action-row"
                >
                  <div className="automation-action-head">
                    <span className="automation-step">
                      {String(index + 1).padStart(
                        2,
                        '0',
                      )}
                    </span>

                    <select
                      value={action.code}
                      onChange={(event) =>
                        changeActionCode(
                          index,
                          event.target.value,
                        )
                      }
                    >
                      {enabledActions.map((option) => (
                        <option
                          key={option.code}
                          value={option.code}
                        >
                          {option.label}
                        </option>
                      ))}
                    </select>

                    {descriptor?.impact && (
                      <span
                        className={
                          'automation-impact ' +
                          descriptor.impact
                        }
                      >
                        {descriptor.impact}
                      </span>
                    )}

                    <label className="automation-inline-check">
                      <input
                        type="checkbox"
                        checked={action.continueOnError}
                        onChange={(event) =>
                          updateAction(index, {
                            continueOnError:
                              event.target.checked,
                          })
                        }
                      />
                      Continuar si falla
                    </label>

                    <button
                      type="button"
                      onClick={() =>
                        moveAction(index, -1)
                      }
                      disabled={index === 0}
                    >
                      <ArrowUp size={14} />
                    </button>

                    <button
                      type="button"
                      onClick={() =>
                        moveAction(index, 1)
                      }
                      disabled={
                        index ===
                        ruleActions.length - 1
                      }
                    >
                      <ArrowDown size={14} />
                    </button>

                    <button
                      type="button"
                      className="automation-delete"
                      onClick={() =>
                        removeAction(index)
                      }
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>

                  {descriptor &&
                    descriptor.parameters.length >
                      0 && (
                      <div className="automation-action-params">
                        {descriptor.parameters.map(
                          (parameter) => (
                            <label
                              key={parameter.name}
                            >
                              {parameter.label}
                              {parameter.required
                                ? ' *'
                                : ''}
                              <input
                                value={
                                  action.parameters[
                                    parameter.name
                                  ] ?? ''
                                }
                                onChange={(event) =>
                                  updateActionParameter(
                                    index,
                                    parameter.name,
                                    event.target
                                      .value,
                                  )
                                }
                              />
                            </label>
                          ),
                        )}
                      </div>
                    )}
                </div>
              )
            })}
          </div>
        )}
      </div>

      <div className="automation-enterprise-section">
        <h3>POLÍTICAS</h3>

        <div className="automation-policy-grid">
          <label>
            Política de error
            <select
              value={policies.errorPolicy}
              onChange={(event) =>
                setPolicies((current) => ({
                  ...current,
                  errorPolicy: event.target.value,
                }))
              }
            >
              {errorPolicyOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>

          <label>
            Concurrencia
            <select
              value={policies.concurrencyPolicy}
              onChange={(event) =>
                setPolicies((current) => ({
                  ...current,
                  concurrencyPolicy:
                    event.target.value,
                }))
              }
            >
              {concurrencyOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>

          <label>
            Deshabilitar tras fallos
            <input
              type="number"
              min={1}
              max={50}
              value={policies.disableAfterFailures}
              onChange={(event) =>
                setPolicies((current) => ({
                  ...current,
                  disableAfterFailures:
                    Number(event.target.value) || 1,
                }))
              }
            />
          </label>

          <label>
            Reintentos (0-5)
            <input
              type="number"
              min={0}
              max={5}
              value={policies.maxRetries}
              onChange={(event) =>
                setPolicies((current) => ({
                  ...current,
                  maxRetries:
                    Number(event.target.value) || 0,
                }))
              }
            />
          </label>

          <label>
            Backoff (segundos)
            <input
              type="number"
              min={0}
              max={3600}
              value={
                policies.retryBackoffSeconds
              }
              onChange={(event) =>
                setPolicies((current) => ({
                  ...current,
                  retryBackoffSeconds:
                    Number(event.target.value) || 0,
                }))
              }
            />
          </label>

          <label>
            Timeout (1-600 s)
            <input
              type="number"
              min={1}
              max={600}
              value={policies.timeoutSeconds}
              onChange={(event) =>
                setPolicies((current) => ({
                  ...current,
                  timeoutSeconds:
                    Number(event.target.value) || 1,
                }))
              }
            />
          </label>

          <label>
            Min. entre ejecuciones (s)
            <input
              type="number"
              min={0}
              value={
                policies.minSecondsBetweenExecutions
              }
              onChange={(event) =>
                setPolicies((current) => ({
                  ...current,
                  minSecondsBetweenExecutions:
                    Number(event.target.value) || 0,
                }))
              }
            />
          </label>
        </div>
      </div>

      <div className="automation-enterprise-section">
        <div className="automation-section-head">
          <h3>PROGRAMACIÓN</h3>

          <label className="automation-inline-check">
            <input
              type="checkbox"
              checked={scheduleEnabled}
              onChange={(event) =>
                setScheduleEnabled(
                  event.target.checked,
                )
              }
            />
            Habilitar programación
          </label>
        </div>

        {scheduleEnabled && (
          <div className="automation-policy-grid">
            <label>
              Recurrencia
              <select
                value={schedule.recurrence}
                onChange={(event) =>
                  setSchedule((current) => ({
                    ...current,
                    recurrence: event.target.value,
                  }))
                }
              >
                {recurrenceOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </label>

            {scheduleIsInterval && (
              <label>
                Intervalo (minutos, ≥5)
                <input
                  type="number"
                  min={5}
                  value={
                    schedule.intervalMinutes ?? 5
                  }
                  onChange={(event) =>
                    setSchedule((current) => ({
                      ...current,
                      intervalMinutes:
                        Number(
                          event.target.value,
                        ) || 5,
                    }))
                  }
                />
              </label>
            )}

            {scheduleNeedsTime && (
              <label>
                Hora (HH:mm)
                <input
                  value={schedule.timeOfDay ?? '08:00'}
                  onChange={(event) =>
                    setSchedule((current) => ({
                      ...current,
                      timeOfDay: event.target.value,
                    }))
                  }
                  placeholder="08:00"
                />
              </label>
            )}

            {schedule.recurrence === 'Weekly' && (
              <label>
                Día de la semana
                <select
                  value={schedule.dayOfWeek ?? 1}
                  onChange={(event) =>
                    setSchedule((current) => ({
                      ...current,
                      dayOfWeek: Number(
                        event.target.value,
                      ),
                    }))
                  }
                >
                  {weekDays.map((day, index) => (
                    <option key={day} value={index}>
                      {day}
                    </option>
                  ))}
                </select>
              </label>
            )}

            {schedule.recurrence === 'Monthly' && (
              <label>
                Día del mes (1-28)
                <input
                  type="number"
                  min={1}
                  max={28}
                  value={schedule.dayOfMonth ?? 1}
                  onChange={(event) =>
                    setSchedule((current) => ({
                      ...current,
                      dayOfMonth:
                        Number(event.target.value) ||
                        1,
                    }))
                  }
                />
              </label>
            )}

            <label>
              Zona horaria
              <input
                value={schedule.timeZoneId}
                onChange={(event) =>
                  setSchedule((current) => ({
                    ...current,
                    timeZoneId: event.target.value,
                  }))
                }
                placeholder="UTC"
              />
            </label>

            <label className="automation-inline-check">
              <input
                type="checkbox"
                checked={schedule.catchUp}
                onChange={(event) =>
                  setSchedule((current) => ({
                    ...current,
                    catchUp: event.target.checked,
                  }))
                }
              />
              Recuperar ejecuciones perdidas
            </label>
          </div>
        )}
      </div>

      <div className="automation-builder-actions">
        <button
          type="button"
          className="automation-primary"
          disabled={saving}
          onClick={submit}
        >
          <Save size={15} />
          {initial
            ? 'Guardar cambios'
            : 'Crear regla empresarial'}
        </button>
      </div>
    </section>
  )
}
