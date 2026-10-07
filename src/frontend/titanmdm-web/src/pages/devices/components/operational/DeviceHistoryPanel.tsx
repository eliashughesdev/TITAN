import {
  ChevronRight,
  Clock3,
  RefreshCw,
  Search,
  X,
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
} from '../../../../api/deviceCommandsApi'

import {
  formatDate,
  parseJson,
} from './deviceOperational.utils'

interface Props {
  deviceId: string
  refreshKey: number
}

function duration(
  command:
    DeviceCommand,
): string {
  const start =
    command.startedAtUtc
    ??
    command.sentAtUtc
    ??
    command.createdAtUtc

  const end =
    command.completedAtUtc
    ??
    command.updatedAtUtc

  const milliseconds =
    new Date(end).getTime()
    -
    new Date(start).getTime()

  if (
    !Number.isFinite(
      milliseconds,
    )
    ||
    milliseconds < 0
  ) {
    return 'N/D'
  }

  if (milliseconds < 1000) {
    return `${milliseconds} ms`
  }

  return `${
    (
      milliseconds
      /
      1000
    ).toFixed(1)
  } s`
}

export function DeviceHistoryPanel({
  deviceId,
  refreshKey,
}: Props) {
  const [
    commands,
    setCommands,
  ] =
    useState<
      DeviceCommand[]
    >([])

  const [
    loading,
    setLoading,
  ] =
    useState(false)

  const [
    search,
    setSearch,
  ] =
    useState('')

  const [
    status,
    setStatus,
  ] =
    useState('')

  const [
    selected,
    setSelected,
  ] =
    useState<
      DeviceCommand | null
    >(null)

  const load =
    useCallback(
      async () => {
        try {
          setLoading(true)

          const response =
            await deviceCommandsApi
              .getForDevice(
                deviceId,
              )

          setCommands(
            response.items,
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
      refreshKey,
    ],
  )

  const filtered =
    useMemo(
      () => {
        const term =
          search
            .trim()
            .toLowerCase()

        return commands.filter(
          command => {
            const matchesSearch =
              !term
              ||
              command
                .commandType
                .toLowerCase()
                .includes(term)
              ||
              command.id
                .toLowerCase()
                .includes(term)

            const matchesStatus =
              !status
              ||
              command.status
              ===
              status

            return (
              matchesSearch
              &&
              matchesStatus
            )
          },
        )
      },
      [
        commands,
        search,
        status,
      ],
    )

  return (
    <article className="op-panel">
      <header>
        <Clock3
          size={18}
        />

        <div>
          <strong>
            Historial operacional
          </strong>

          <span>
            Últimos comandos del dispositivo
          </span>
        </div>

        <button
          type="button"
          disabled={loading}
          onClick={() =>
            void load()
          }
        >
          <RefreshCw
            size={14}
          />

          Actualizar
        </button>
      </header>

      <div className="op-history-filters">
        <div className="op-search">
          <Search
            size={15}
          />

          <input
            value={search}
            placeholder="Buscar comando..."
            onChange={
              event =>
                setSearch(
                  event
                    .target
                    .value,
                )
            }
          />
        </div>

        <select
          value={status}
          onChange={
            event =>
              setStatus(
                event.target.value,
              )
          }
        >
          <option value="">
            Todos los estados
          </option>

          <option value="Pending">
            Pending
          </option>

          <option value="Queued">
            Queued
          </option>

          <option value="Executing">
            Executing
          </option>

          <option value="Success">
            Success
          </option>

          <option value="Failed">
            Failed
          </option>

          <option value="Timeout">
            Timeout
          </option>

          <option value="Cancelled">
            Cancelled
          </option>
        </select>
      </div>

      <div className="op-table-wrap">
        <table className="op-table">
          <thead>
            <tr>
              <th>
                Comando
              </th>

              <th>
                Estado
              </th>

              <th>
                Fecha
              </th>

              <th>
                Duración
              </th>

              <th>
                Intentos
              </th>

              <th />
            </tr>
          </thead>

          <tbody>
            {filtered.map(
              command => (
                <tr
                  key={
                    command.id
                  }
                >
                  <td>
                    <strong>
                      {
                        command
                          .commandType
                      }
                    </strong>
                  </td>

                  <td>
                    <span
                      className={
                        `op-command-status ${command.status.toLowerCase()}`
                      }
                    >
                      {command.status}
                    </span>
                  </td>

                  <td>
                    {formatDate(
                      command
                        .createdAtUtc,
                    )}
                  </td>

                  <td>
                    {duration(
                      command,
                    )}
                  </td>

                  <td>
                    {
                      command
                        .deliveryAttempts
                    }
                  </td>

                  <td>
                    <button
                      type="button"
                      className="op-row-action"
                      onClick={() =>
                        setSelected(
                          command,
                        )
                      }
                    >
                      Detalle

                      <ChevronRight
                        size={14}
                      />
                    </button>
                  </td>
                </tr>
              ),
            )}

            {!loading &&
            filtered.length ===
              0 && (
              <tr>
                <td
                  colSpan={6}
                  className="op-table-empty"
                >
                  No hay comandos para mostrar.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {selected && (
        <div className="op-modal-backdrop">
          <div className="op-modal op-modal-large">
            <header>
              <div>
                <span>
                  COMMAND DETAIL
                </span>

                <h3>
                  {
                    selected
                      .commandType
                  }
                </h3>
              </div>

              <button
                type="button"
                onClick={() =>
                  setSelected(null)
                }
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div className="op-modal-grid">
              <div>
                <span>
                  Estado
                </span>

                <strong>
                  {selected.status}
                </strong>
              </div>

              <div>
                <span>
                  Duración
                </span>

                <strong>
                  {duration(
                    selected,
                  )}
                </strong>
              </div>

              <div>
                <span>
                  Creado
                </span>

                <strong>
                  {formatDate(
                    selected
                      .createdAtUtc,
                  )}
                </strong>
              </div>

              <div>
                <span>
                  Completado
                </span>

                <strong>
                  {formatDate(
                    selected
                      .completedAtUtc,
                  )}
                </strong>
              </div>

              {selected
                .errorMessage && (
                <div className="wide op-error-box">
                  <span>
                    Error
                  </span>

                  <strong>
                    {
                      selected
                        .errorMessage
                    }
                  </strong>
                </div>
              )}
            </div>

            <details className="op-technical-details">
              <summary>
                Ver payload enviado
              </summary>

              <pre>
                {JSON.stringify(
                  parseJson(
                    selected
                      .payloadJson,
                  ),
                  null,
                  2,
                )}
              </pre>
            </details>

            <details className="op-technical-details">
              <summary>
                Ver resultado técnico
              </summary>

              <pre>
                {JSON.stringify(
                  parseJson(
                    selected
                      .resultJson,
                  ),
                  null,
                  2,
                )}
              </pre>
            </details>
          </div>
        </div>
      )}
    </article>
  )
}