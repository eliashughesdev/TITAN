import {
  useMemo,
  useState,
} from 'react'

import {
  CheckCircle2,
  Database,
  Download,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react'

import {
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'

import {
  authFetch,
} from '../lib/api'

import {
  Btn,
  PageHeader,
  Panel,
} from '../ui/kit'

type SyncRow = {
  id?: number

  fecha?: string

  evento?: string

  detalle?: string

  registros?: number

  estado?: string
}

type Device = {
  name: string
}

type SyncHistoryResponse = {
  items?: SyncRow[]

  devices?: Device[]
}

type SyncPayload = {
  device: string
  start: string
  end: string
}

type SyncResult = {
  candidates?: number
  inserts?: number
  updates?: number
  inserted?: number
  updated?: number
  unchanged?: number
  ignored?: number
  sql_confirmed?: boolean
  message?: string
  warning?: string
}

function today(): string {
  const date =
    new Date()

  return [
    date.getFullYear(),

    String(
      date.getMonth() +
        1,
    ).padStart(
      2,
      '0',
    ),

    String(
      date.getDate(),
    ).padStart(
      2,
      '0',
    ),
  ].join(
    '-',
  )
}

async function readJson<T>(
  response: Response,
): Promise<T> {
  const data =
    await response
      .json()
      .catch(
        () =>
          ({}),
      ) as
      T & {
        detail?: string
        message?: string
      }

  if (
    !response.ok
  ) {
    throw new Error(
      data.detail ??
      data.message ??
      `Error HTTP ${response.status}`,
    )
  }

  return data
}

async function loadSyncHistory(
  signal?: AbortSignal,
): Promise<SyncHistoryResponse> {
  /*
   * Este endpoint todavía pertenece al bridge F6.
   *
   * Será convertido a /api/ponches/sync-history
   * durante F9 después de terminar todos los
   * contratos Python.
   */
  return readJson<SyncHistoryResponse>(
    await authFetch(
      '/api/records/sync-history?limit=150',
      {
        signal,
      },
    ),
  )
}

async function previewSync(
  payload: SyncPayload,
): Promise<SyncResult> {
  return readJson<SyncResult>(
    await authFetch(
      '/api/records/sync-now',
      {
        method:
          'POST',

        headers: {
          'Content-Type':
            'application/json',
        },

        body:
          JSON.stringify({
            ...payload,

            dry_run:
              true,

            confirm:
              false,
          }),
      },
    ),
  )
}

async function executeSync(
  payload: SyncPayload,
): Promise<SyncResult> {
  return readJson<SyncResult>(
    await authFetch(
      '/api/records/sync-now',
      {
        method:
          'POST',

        headers: {
          'Content-Type':
            'application/json',
        },

        body:
          JSON.stringify({
            ...payload,

            dry_run:
              false,

            confirm:
              true,
          }),
      },
    ),
  )
}

export default function SyncHistory() {
  const client =
    useQueryClient()

  const [
    device,
    setDevice,
  ] =
    useState(
      '',
    )

  const [
    start,
    setStart,
  ] =
    useState(
      today(),
    )

  const [
    end,
    setEnd,
  ] =
    useState(
      today(),
    )

  const [
    preview,
    setPreview,
  ] =
    useState<{
      payload: SyncPayload
      result: SyncResult
    } | null>(
      null,
    )

  const [
    message,
    setMessage,
  ] =
    useState(
      '',
    )

  const historyQuery =
    useQuery({
      queryKey: [
        'ponches',
        'sync-history',
      ],

      queryFn:
        ({
          signal,
        }) =>
          loadSyncHistory(
            signal,
          ),

      staleTime:
        20_000,

      gcTime:
        5 * 60_000,

      refetchOnWindowFocus:
        false,
    })

  const inspectMutation =
    useMutation({
      mutationFn:
        previewSync,

      onSuccess:
        (
          result,
          payload,
        ) => {
          setMessage(
            '',
          )

          setPreview({
            payload,
            result,
          })
        },
    })

  const syncMutation =
    useMutation({
      mutationFn:
        executeSync,

      onSuccess:
        async result => {
          if (
            !result
              .sql_confirmed
          ) {
            throw new Error(
              'El servidor no confirmó la escritura en SQL.',
            )
          }

          setPreview(
            null,
          )

          setMessage(
            [
              result.message ??
                'Sincronización confirmada.',

              result.warning ??
                '',
            ]
              .filter(
                Boolean,
              )
              .join(
                ' ',
              ),
          )

          /*
           * Invalidamos las vistas relacionadas.
           */
          await Promise.all([
            client.invalidateQueries({
              queryKey: [
                'ponches',
                'sync-history',
              ],
            }),

            client.invalidateQueries({
              queryKey: [
                'ponches',
                'dashboard',
              ],
            }),

            client.invalidateQueries({
              queryKey: [
                'ponches',
                'records',
              ],
            }),

            client.invalidateQueries({
              queryKey: [
                'ponches',
                'employees',
              ],
            }),

            client.invalidateQueries({
              queryKey: [
                'ponches',
                'device-health',
              ],
            }),
          ])
        },
    })

  const data =
    historyQuery.data

  const items =
    data?.items ??
    []

  const devices =
    data?.devices ??
    []

  const completed =
    useMemo(
      () =>
        items.filter(
          item =>
            item.estado
              ?.toLowerCase() ===
            'ok',
        ).length,
      [
        items,
      ],
    )

  const errors =
    items.length -
    completed

  const busy =
    inspectMutation
      .isPending ||
    syncMutation
      .isPending

  const error =
    historyQuery.error ??
    inspectMutation.error ??
    syncMutation.error

  const selectedValid =
    devices.some(
      item =>
        item.name ===
        device,
    )

  const inspect =
    () => {
      if (
        !selectedValid
      ) {
        return
      }

      setMessage(
        '',
      )

      setPreview(
        null,
      )

      inspectMutation.mutate({
        device,
        start,
        end,
      })
    }

  return (
    <div className="space-y-5 page-enter">
      <PageHeader
        kicker="Sincronización"
        title="Reloj → TitanMDM"
        subtitle="Importación controlada de asistencia desde dispositivos biométricos."
        actions={
          <Btn
            tone="ghost"
            disabled={
              historyQuery
                .isFetching
            }
            onClick={() =>
              void historyQuery
                .refetch()
            }
          >
            <RefreshCw
              size={16}
              className={
                historyQuery
                  .isFetching
                  ? 'animate-spin'
                  : ''
              }
            />

            Actualizar
          </Btn>
        }
      />

      <div className="grid gap-3 sm:grid-cols-3">
        <Metric
          title="Operaciones"
          value={
            items.length
          }
        />

        <Metric
          title="Completadas"
          value={
            completed
          }
        />

        <Metric
          title="Otros estados"
          value={
            errors
          }
        />
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700"
        >
          {error instanceof Error
            ? error.message
            : 'No se pudo completar la operación.'}
        </div>
      )}

      {message && (
        <div
          role="status"
          className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700"
        >
          <CheckCircle2
            size={17}
          />

          {message}
        </div>
      )}

      <Panel>
        <div className="mb-4 flex items-center gap-2">
          <Download
            size={18}
            className="text-red-600"
          />

          <div>
            <h3 className="font-bold">
              Importar desde reloj
            </h3>

            <p className="text-xs text-zinc-500">
              Primero se realiza una vista previa. Nada se escribe hasta confirmar.
            </p>
          </div>
        </div>

        <div className="grid gap-3 lg:grid-cols-4">
          <label className="grid gap-1 text-xs font-semibold text-zinc-600">
            Dispositivo

            <select
              value={
                device
              }
              disabled={
                busy
              }
              onChange={
                event => {
                  setDevice(
                    event.target.value,
                  )

                  setPreview(
                    null,
                  )
                }
              }
              className="rounded-xl border border-zinc-200 bg-white px-3 py-2.5 text-sm"
            >
              <option value="">
                Selecciona un reloj
              </option>

              {devices.map(
                item => (
                  <option
                    key={
                      item.name
                    }
                    value={
                      item.name
                    }
                  >
                    {
                      item.name
                    }
                  </option>
                ),
              )}
            </select>
          </label>

          <label className="grid gap-1 text-xs font-semibold text-zinc-600">
            Desde

            <input
              type="date"
              value={
                start
              }
              max={
                end
              }
              disabled={
                busy
              }
              onChange={
                event => {
                  setStart(
                    event.target.value,
                  )

                  setPreview(
                    null,
                  )
                }
              }
              className="rounded-xl border border-zinc-200 px-3 py-2.5 text-sm"
            />
          </label>

          <label className="grid gap-1 text-xs font-semibold text-zinc-600">
            Hasta

            <input
              type="date"
              value={
                end
              }
              min={
                start
              }
              max={
                today()
              }
              disabled={
                busy
              }
              onChange={
                event => {
                  setEnd(
                    event.target.value,
                  )

                  setPreview(
                    null,
                  )
                }
              }
              className="rounded-xl border border-zinc-200 px-3 py-2.5 text-sm"
            />
          </label>

          <div className="flex items-end">
            <Btn
              tone="primary"
              disabled={
                busy ||
                !selectedValid
              }
              onClick={
                inspect
              }
            >
              <Database
                size={16}
              />

              {inspectMutation
                .isPending
                ? 'Leyendo…'
                : 'Previsualizar'}
            </Btn>
          </div>
        </div>
      </Panel>

      {preview && (
        <Panel>
          <div className="mb-4">
            <h3 className="font-bold">
              Vista previa
            </h3>

            <p className="text-xs text-zinc-500">
              {preview.payload.device}
              {' · '}
              {preview.payload.start}
              {' → '}
              {preview.payload.end}
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
            <PreviewMetric
              title="Candidatos"
              value={
                preview
                  .result
                  .candidates
              }
            />

            <PreviewMetric
              title="Nuevos"
              value={
                preview
                  .result
                  .inserts
              }
            />

            <PreviewMetric
              title="Actualizar"
              value={
                preview
                  .result
                  .updates
              }
            />

            <PreviewMetric
              title="Sin cambios"
              value={
                preview
                  .result
                  .unchanged
              }
            />

            <PreviewMetric
              title="Ignorados"
              value={
                preview
                  .result
                  .ignored
              }
            />
          </div>

          <div className="mt-5 flex items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4">
            <div>
              <strong className="text-sm text-amber-900">
                Confirmación requerida
              </strong>

              <p className="mt-1 text-xs text-amber-700">
                El preview no modifica SQL ni elimina datos del dispositivo.
              </p>
            </div>

            <Btn
              tone="primary"
              disabled={
                syncMutation
                  .isPending
              }
              onClick={() =>
                syncMutation.mutate(
                  preview.payload,
                )
              }
            >
              <ShieldCheck
                size={16}
              />

              {syncMutation
                .isPending
                ? 'Sincronizando…'
                : 'Confirmar importación'}
            </Btn>
          </div>
        </Panel>
      )}

      <Panel>
        <h3 className="mb-4 font-bold">
          Historial de sincronización
        </h3>

        <div className="max-h-[560px] overflow-auto">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-white">
              <tr className="border-b text-left text-[11px] uppercase tracking-wide text-zinc-500">
                <th className="py-3">
                  Fecha
                </th>

                <th>
                  Evento
                </th>

                <th>
                  Detalle
                </th>

                <th>
                  Registros
                </th>

                <th>
                  Estado
                </th>
              </tr>
            </thead>

            <tbody>
              {historyQuery
                .isLoading && (
                <tr>
                  <td
                    colSpan={5}
                    className="py-10 text-center text-zinc-400"
                  >
                    Cargando historial…
                  </td>
                </tr>
              )}

              {!historyQuery
                  .isLoading &&
                items.length ===
                  0 && (
                  <tr>
                    <td
                      colSpan={5}
                      className="py-12 text-center text-zinc-400"
                    >
                      Aún no existen operaciones registradas.
                    </td>
                  </tr>
                )}

              {items.map(
                (
                  row,
                  index,
                ) => {
                  const ok =
                    row.estado
                      ?.toLowerCase() ===
                    'ok'

                  return (
                    <tr
                      key={
                        row.id ??
                        index
                      }
                      className="border-b border-zinc-100"
                    >
                      <td className="py-3 text-xs text-zinc-500">
                        {
                          row.fecha
                        }
                      </td>

                      <td className="font-semibold">
                        {
                          row.evento
                        }
                      </td>

                      <td className="max-w-xl text-xs text-zinc-500">
                        {
                          row.detalle
                        }
                      </td>

                      <td>
                        {row.registros ??
                          0}
                      </td>

                      <td>
                        <span
                          className={
                            `rounded-full px-2.5 py-1 text-xs font-semibold ${
                              ok
                                ? 'bg-emerald-50 text-emerald-700'
                                : 'bg-amber-50 text-amber-700'
                            }`
                          }
                        >
                          {row.estado ||
                            'N/D'}
                        </span>
                      </td>
                    </tr>
                  )
                },
              )}
            </tbody>
          </table>
        </div>
      </Panel>
    </div>
  )
}

function Metric({
  title,
  value,
}: {
  title: string
  value: number
}) {
  return (
    <div className="rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
      <p className="text-xs font-semibold uppercase tracking-wide text-zinc-500">
        {title}
      </p>

      <p className="mt-2 text-3xl font-black text-zinc-900">
        {value}
      </p>
    </div>
  )
}

function PreviewMetric({
  title,
  value,
}: {
  title: string
  value?: number
}) {
  return (
    <div className="rounded-xl bg-zinc-50 p-4">
      <p className="text-xs text-zinc-500">
        {title}
      </p>

      <p className="mt-1 text-2xl font-bold">
        {value ??
          0}
      </p>
    </div>
  )
}