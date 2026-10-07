import {
  Activity,
  CheckCircle2,
  Layers3,
  RefreshCw,
  ShieldCheck,
  UsersRound,
  Wifi,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  deviceCommandsApi,
  type DeviceCommand,
} from '../../../api/deviceCommandsApi'

import {
  devicesApi,
} from '../../../api/devicesApi'

import type {
  DeviceOperationalSnapshot,
} from '../../../types/device'

import {
  DeviceActionsPanel,
} from './operational/DeviceActionsPanel'

import {
  DeviceHistoryPanel,
} from './operational/DeviceHistoryPanel'

import {
  DeviceInventoryView,
} from './operational/DeviceInventoryView'

import {
  DeviceSoftwarePanel,
} from './operational/DeviceSoftwarePanel'

import {
  formatDate,
} from './operational/deviceOperational.utils'

import './DeviceOperationalPanel.css'

interface Props {
  deviceId: string
}

const INVENTORY_COMMANDS = [
  'DEVICE_INVENTORY',
  'APP_INVENTORY',
  'NETWORK_INFO',
  'SECURITY_STATUS',
  'COMPLIANCE_CHECK',
  'WINDOWS_UPDATE_STATUS',
] as const

function isTerminal(
  status: string,
): boolean {
  return [
    'Success',
    'Failed',
    'Timeout',
    'Cancelled',
  ].includes(status)
}

async function waitForCommand(
  command: DeviceCommand,
): Promise<DeviceCommand> {
  let current =
    command

  for (
    let attempt = 0;
    attempt < 30;
    attempt++
  ) {
    if (
      isTerminal(
        current.status,
      )
    ) {
      return current
    }

    await new Promise<void>(
      resolve => {
        window.setTimeout(
          resolve,
          2000,
        )
      },
    )

    current =
      await deviceCommandsApi
        .getById(
          current.id,
        )
  }

  return current
}

function calculateHealth(
  snapshot:
    DeviceOperationalSnapshot,
): {
  score: number

  checks: Array<{
    label: string
    ok: boolean
    value: string
  }>
} {
  let score = 0

  const checks:
    Array<{
      label: string
      ok: boolean
      value: string
    }> =
      []

  const managed =
    snapshot.device.isManaged

  score +=
    managed
      ? 25
      : 0

  checks.push({
    label:
      'Administración',

    ok:
      managed,

    value:
      managed
        ? 'Administrado'
        : 'No administrado',
  })

  const compliance =
    snapshot.device
      .complianceStatus

  const compliant =
    compliance ===
    'Compliant'

  score +=
    compliant
      ? 25
      : compliance ===
          'Unknown'
        ? 10
        : 0

  checks.push({
    label:
      'Cumplimiento',

    ok:
      compliant,

    value:
      compliance,
  })

  const agent =
    Boolean(
      snapshot.device
        .agentVersion,
    )
    ||
    Boolean(
      snapshot.security
        ?.agentInstalled,
    )

  score +=
    agent
      ? 20
      : 0

  checks.push({
    label:
      'Agente',

    ok:
      agent,

    value:
      snapshot.device
        .agentVersion
      ??
      snapshot.security
        ?.agentVersionName
      ??
      'No detectado',
  })

  const lastSeen =
    snapshot.device
      .lastSeenAtUtc
      ? new Date(
          snapshot.device
            .lastSeenAtUtc,
        )
      : null

  const recent =
    lastSeen !== null
    &&
    Date.now()
    -
    lastSeen.getTime()
    <
    24
    *
    60
    *
    60
    *
    1000

  score +=
    recent
      ? 15
      : 0

  checks.push({
    label:
      'Telemetría',

    ok:
      recent,

    value:
      formatDate(
        snapshot.device
          .lastSeenAtUtc,
      ),
  })

  const securityScore =
    snapshot.security
      ?.complianceScore

  if (
    securityScore !==
    undefined
  ) {
    const normalized =
      Math.max(
        0,
        Math.min(
          100,
          securityScore,
        ),
      )

    score +=
      Math.round(
        normalized
        *
        0.15,
      )
  } else {
    score += 7
  }

  checks.push({
    label:
      'Seguridad',

    ok:
      securityScore ===
        undefined
        ||
        securityScore >=
          70,

    value:
      securityScore !==
        undefined
        ? `${securityScore}/100`
        : 'Sin evaluación',
  })

  const online =
    snapshot.device.status ===
    'Online'

  checks.push({
    label:
      'Conectividad',

    ok:
      online,

    value:
      snapshot.device.status,
  })

  return {
    score:
      Math.max(
        0,
        Math.min(
          100,
          score,
        ),
      ),

    checks,
  }
}

export function DeviceOperationalPanel({
  deviceId,
}: Props) {
  const [
    snapshot,
    setSnapshot,
  ] =
    useState<
      DeviceOperationalSnapshot | null
    >(null)

  const [
    loading,
    setLoading,
  ] =
    useState(true)

  const [
    working,
    setWorking,
  ] =
    useState<
      string | null
    >(null)

  const [
    message,
    setMessage,
  ] =
    useState<
      string | null
    >(null)

  const [
    error,
    setError,
  ] =
    useState<
      string | null
    >(null)

  const [
    historyRefreshKey,
    setHistoryRefreshKey,
  ] =
    useState(0)

  const load =
    useCallback(
      async (): Promise<void> => {
        try {
          setLoading(true)

          const data =
            await devicesApi
              .getOperationalSnapshot(
                deviceId,
              )

          setSnapshot(data)
          setError(null)
        } catch {
          setError(
            'No fue posible cargar la información operacional.',
          )
        } finally {
          setLoading(false)
        }
      },
      [
        deviceId,
      ],
    )

  useEffect(
    () => {
      void load()
    },
    [
      load,
    ],
  )

  const health =
    useMemo(
      () =>
        snapshot
          ? calculateHealth(
              snapshot,
            )
          : null,
      [
        snapshot,
      ],
    )

  async function execute(
    commandType: string,
    payloadJson = '{}',
  ): Promise<void> {
    if (working) {
      return
    }

    try {
      setWorking(
        commandType,
      )

      setError(null)
      setMessage(null)

      const command =
        await deviceCommandsApi
          .create({
            deviceId,
            commandType,
            payloadJson,
            expirationMinutes:
              30,
          })

      const result =
        await waitForCommand(
          command,
        )

      if (
        result.status ===
        'Success'
      ) {
        setMessage(
          `${commandType} completado correctamente.`,
        )
      } else {
        setError(
          result.errorMessage
          ??
          `${commandType} terminó con estado ${result.status}.`,
        )
      }

      await load()

      setHistoryRefreshKey(
        current =>
          current + 1,
      )
    } catch {
      setError(
        `No fue posible ejecutar ${commandType}.`,
      )
    } finally {
      setWorking(null)
    }
  }

  async function fullRefresh():
    Promise<void> {
    if (
      working
      ||
      !snapshot
    ) {
      return
    }

    if (
      snapshot.device.platform !==
      'Windows'
    ) {
      await execute(
        'DEVICE_INFO',
      )

      return
    }

    try {
      setWorking(
        'FULL_REFRESH',
      )

      setError(null)

      setMessage(
        'Solicitando inventario completo...',
      )

      const commands:
        DeviceCommand[] =
          await Promise.all(
            INVENTORY_COMMANDS.map(
              commandType =>
                deviceCommandsApi
                  .create({
                    deviceId,
                    commandType,
                    payloadJson:
                      '{}',
                    expirationMinutes:
                      30,
                  }),
            ),
          )

      const results:
        DeviceCommand[] =
          await Promise.all(
            commands.map(
              (
                command:
                  DeviceCommand,
              ) =>
                waitForCommand(
                  command,
                ),
            ),
          )

      const failures =
        results.filter(
          (
            result:
              DeviceCommand,
          ) =>
            result.status !==
            'Success',
        )

      await load()

      setHistoryRefreshKey(
        current =>
          current + 1,
      )

      if (
        failures.length >
        0
      ) {
        setError(
          `${failures.length} operación(es) del inventario no finalizaron correctamente.`,
        )
      } else {
        setMessage(
          'Inventario actualizado correctamente.',
        )
      }
    } catch {
      setError(
        'No fue posible completar la actualización integral.',
      )
    } finally {
      setWorking(null)
    }
  }

  if (
    loading
    &&
    !snapshot
  ) {
    return (
      <section className="device-op-loading">
        <RefreshCw
          size={20}
          className="device-detail-spin"
        />

        Cargando información operacional...
      </section>
    )
  }

  if (!snapshot) {
    return (
      <section className="device-op-error">
        {error
        ??
        'Información operacional no disponible.'}
      </section>
    )
  }

  const windows =
    snapshot.device.platform ===
    'Windows'

  return (
    <section className="device-op">
      <div className="device-op-toolbar">
        <div>
          <span>
            DEVICE OPERATIONS
          </span>

          <strong>
            Inventario y administración
          </strong>

          <small>
            Último inventario:{' '}
            {formatDate(
              snapshot
                .lastInventoryAtUtc,
            )}
          </small>
        </div>

        <button
          type="button"
          className="device-op-primary"
          disabled={
            Boolean(working)
          }
          onClick={() =>
            void fullRefresh()
          }
        >
          <RefreshCw
            size={15}
            className={
              working ===
              'FULL_REFRESH'
                ? 'device-detail-spin'
                : ''
            }
          />

          {working ===
          'FULL_REFRESH'
            ? 'Actualizando...'
            : 'Actualizar inventario'}
        </button>
      </div>

      {message && (
        <div className="device-op-notice success">
          <CheckCircle2
            size={16}
          />

          {message}
        </div>
      )}

      {error && (
        <div className="device-op-notice error">
          {error}
        </div>
      )}

      <div className="device-op-health-layout">
        <article className="device-op-health">
          <div className="device-op-health-score">
            <strong>
              {health?.score
              ??
              0}
            </strong>

            <span>
              /100
            </span>
          </div>

          <div>
            <span>
              DEVICE HEALTH
            </span>

            <h3>
              Salud del endpoint
            </h3>

            <p>
              Administración, cumplimiento,
              agente, telemetría y seguridad.
            </p>
          </div>
        </article>

        <div className="device-op-health-checks">
          {health?.checks.map(
            check => (
              <div
                key={check.label}
                className={
                  check.ok
                    ? 'good'
                    : 'warning'
                }
              >
                <span>
                  {check.label}
                </span>

                <strong>
                  {check.value}
                </strong>
              </div>
            ),
          )}
        </div>
      </div>

      <div className="device-op-summary">
        <article>
          <Activity
            size={18}
          />

          <span>
            Estado
          </span>

          <strong>
            {snapshot.device.status}
          </strong>
        </article>

        <article>
          <ShieldCheck
            size={18}
          />

          <span>
            Cumplimiento
          </span>

          <strong>
            {
              snapshot.device
                .complianceStatus
            }
          </strong>
        </article>

        <article>
          <UsersRound
            size={18}
          />

          <span>
            Grupos
          </span>

          <strong>
            {snapshot.groups.length}
          </strong>
        </article>

        <article>
          <Wifi
            size={18}
          />

          <span>
            IP
          </span>

          <strong>
            {snapshot.device
              .ipAddress
            ??
            'N/D'}
          </strong>
        </article>
      </div>

      <article className="op-panel">
        <header>
          <Layers3
            size={18}
          />

          <div>
            <strong>
              Grupos
            </strong>

            <span>
              Membresía del endpoint
            </span>
          </div>
        </header>

        <div className="device-op-groups">
          {snapshot.groups.length ===
          0 ? (
            <span>
              No pertenece a ningún grupo.
            </span>
          ) : (
            snapshot.groups.map(
              group => (
                <div
                  key={group.id}
                >
                  <strong>
                    {group.name}
                  </strong>

                  <span>
                    {group.isDynamic
                      ? 'Dinámico'
                      : 'Estático'}

                    {' · '}

                    {group.source}
                  </span>
                </div>
              ),
            )
          )}
        </div>
      </article>

      {windows && (
        <>
          <DeviceInventoryView
            inventory={
              snapshot
                .latestResults[
                  'DEVICE_INVENTORY'
                ]
            }
            network={
              snapshot
                .latestResults[
                  'NETWORK_INFO'
                ]
            }
          />

          <DeviceSoftwarePanel
            snapshot={
              snapshot
                .latestResults[
                  'APP_INVENTORY'
                ]
            }
            busy={
              Boolean(working)
            }
            onRefresh={() =>
              execute(
                'APP_INVENTORY',
              )
            }
            onExecute={
              execute
            }
          />

          <DeviceActionsPanel
            busy={
              Boolean(working)
            }
            onFullRefresh={
              fullRefresh
            }
            onExecute={
              execute
            }
          />

          <DeviceHistoryPanel
            deviceId={
              deviceId
            }
            refreshKey={
              historyRefreshKey
            }
          />
        </>
      )}
    </section>
  )
}