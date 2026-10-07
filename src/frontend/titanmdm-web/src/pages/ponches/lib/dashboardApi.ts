import {
  titanFetch,
} from './api'

// ============================================================================
// PUBLIC TYPES
// ============================================================================

export type PunchRow = {
  id?: number | string
  codigo?: string
  nombre?: string | null
  departamento?: string | null
  fecha?: string | null
  entrada?: string | null
  salida?: string | null
  dispositivo_origen?: string | null
}

export type DeviceHealthItem = {
  name: string
  online: boolean
  latencyMs: number | null
  punchesToday: number
}

export type DashboardSnapshot = {
  generatedAt: string

  serviceOnline: boolean
  databaseOnline: boolean

  punchesToday: number
  punchesYesterday: number

  employeesToday: number
  employeesYesterday: number

  entriesToday: number
  exitsToday: number

  openShifts: number

  devicesTotal: number
  devicesOnline: number
  devicesOffline: number

  byDevice: Array<{
    name: string
    total: number
  }>

  byDepartment: Array<{
    name: string
    total: number
  }>

  byHour: Array<{
    hour: number
    total: number
  }>

  byDay: Array<{
    day: string
    total: number
  }>

  recentPunches: PunchRow[]

  activity: Array<{
    timestamp: string
    actor: string
    action: string
    target: string
  }>

  healthItems: DeviceHealthItem[]

  warnings: string[]
}

// ============================================================================
// INTERNAL TYPES
// ============================================================================

type UnknownRecord =
  Record<string, unknown>

type ServiceHealthSnapshot = {
  serviceOnline: boolean
  databaseOnline: boolean
}

// ============================================================================
// BASIC NORMALIZERS
// ============================================================================

function object(
  value: unknown,
): UnknownRecord {
  return (
    value != null &&
    typeof value === 'object' &&
    !Array.isArray(value)
  )
    ? value as UnknownRecord
    : {}
}

function array(
  value: unknown,
): unknown[] {
  return Array.isArray(value)
    ? value
    : []
}

function numberValue(
  value: unknown,
  fallback = 0,
): number {
  if (
    typeof value === 'number' &&
    Number.isFinite(value)
  ) {
    return value
  }

  if (
    typeof value === 'string' &&
    value.trim() !== ''
  ) {
    const parsed =
      Number(value)

    if (
      Number.isFinite(parsed)
    ) {
      return parsed
    }
  }

  return fallback
}

function stringValue(
  value: unknown,
  fallback = '',
): string {
  if (
    typeof value === 'string'
  ) {
    return value
  }

  if (
    value != null
  ) {
    return String(value)
  }

  return fallback
}

function booleanValue(
  value: unknown,
  fallback = false,
): boolean {
  if (
    typeof value === 'boolean'
  ) {
    return value
  }

  if (
    typeof value === 'number'
  ) {
    return value !== 0
  }

  if (
    typeof value === 'string'
  ) {
    const normalized =
      value
        .trim()
        .toLowerCase()

    if (
      [
        'true',
        '1',
        'online',
        'healthy',
        'ok',
        'available',
        'operativo',
        'connected',
        'up',
      ].includes(normalized)
    ) {
      return true
    }

    if (
      [
        'false',
        '0',
        'offline',
        'down',
        'error',
        'unavailable',
        'failed',
        'disconnected',
      ].includes(normalized)
    ) {
      return false
    }
  }

  return fallback
}

// ============================================================================
// RESPONSE PARSER
// ============================================================================

async function safeJson(
  response: Response,
): Promise<unknown> {
  const text =
    await response.text()

  if (
    !text.trim()
  ) {
    return {}
  }

  try {
    return JSON.parse(text)
  } catch {
    return {}
  }
}

async function requireJson(
  response: Response,
  description: string,
): Promise<unknown> {
  const payload =
    await safeJson(response)

  if (
    response.ok
  ) {
    return payload
  }

  const row =
    object(payload)

  const detail =
    stringValue(
      row.detail ??
      row.message ??
      row.code,
      '',
    )

  throw new Error(
    detail ||
    `${description} respondió HTTP ${response.status}.`,
  )
}

// ============================================================================
// SERVICE HEALTH
// ============================================================================

function normalizeServiceHealth(
  raw: unknown,
  responseOk: boolean,
): ServiceHealthSnapshot {
  const root =
    object(raw)

  const serviceOnline =
    responseOk &&
    booleanValue(
      root.status ??
      root.serviceStatus ??
      root.online,
      responseOk,
    )

  const databaseOnline =
    booleanValue(
      root.database ??
      root.databaseStatus ??
      root.sql ??
      root.sqlOnline,
      serviceOnline,
    )

  return {
    serviceOnline,
    databaseOnline,
  }
}

// ============================================================================
// DEVICE HEALTH
// ============================================================================

function normalizeHealth(
  raw: unknown,
): {
  total: number
  online: number
  offline: number
  items: DeviceHealthItem[]
} {
  const root =
    object(raw)

  const rawItems =
    array(
      root.items ??
      root.devices ??
      root.relojes ??
      root.data,
    )

  const items =
    rawItems.map(
      item => {
        const row =
          object(item)

        const status =
          row.status ??
          row.estado

        return {
          name:
            stringValue(
              row.name ??
              row.nombre ??
              row.deviceName ??
              row.device_name ??
              row.device ??
              row.alias ??
              row.ip,
              'Reloj',
            ),

          online:
            booleanValue(
              row.online ??
              row.isOnline ??
              row.is_online ??
              row.connected ??
              row.is_connected ??
              status,
              false,
            ),

          latencyMs:
            (
              row.latencyMs == null &&
              row.latency_ms == null &&
              row.latency == null
            )
              ? null
              : numberValue(
                  row.latencyMs ??
                  row.latency_ms ??
                  row.latency,
                ),

          punchesToday:
            numberValue(
              row.punchesToday ??
              row.punches_today ??
              row.ponchesHoy ??
              row.ponches_hoy ??
              row.totalToday ??
              row.total_today,
            ),
        }
      },
    )

  const computedOnline =
    items.filter(
      item =>
        item.online,
    ).length

  const total =
    numberValue(
      root.total ??
      root.devicesTotal ??
      root.totalDevices ??
      root.count,
      items.length,
    )

  const online =
    numberValue(
      root.online ??
      root.devicesOnline ??
      root.onlineDevices,
      computedOnline,
    )

  const offline =
    numberValue(
      root.offline ??
      root.devicesOffline ??
      root.offlineDevices,
      Math.max(
        0,
        total - online,
      ),
    )

  return {
    total,
    online,
    offline,
    items,
  }
}

// ============================================================================
// DASHBOARD NORMALIZATION
//
// CONTRATO PRINCIPAL ACTUAL:
//
// Python:
//
// {
//   generatedAtUtc,
//   summary: {
//     totalPunchesToday,
//     totalPunchesYesterday,
//     recordsToday,
//     employeesToday,
//     employeesYesterday,
//     devicesToday,
//     entriesToday,
//     exitsToday,
//     openShifts
//   },
//   byHour,
//   byDay,
//   byDepartment,
//   byDevice,
//   recent
// }
//
// Se mantienen aliases antiguos solamente como defensa,
// NO como fuente principal.
// ============================================================================

function normalizeDashboard(
  raw: unknown,
  serviceHealth: ServiceHealthSnapshot,
  deviceHealthRaw: unknown,
): DashboardSnapshot {
  const root =
    object(raw)

  const summary =
    object(
      root.summary,
    )

  const legacyOverview =
    object(
      root.overview,
    )

  const legacyToday =
    object(
      legacyOverview.today ??
      root.today,
    )

  const legacyYesterday =
    object(
      legacyOverview.yesterday ??
      root.yesterday,
    )

  const legacyStats =
    object(
      root.stats,
    )

  const health =
    normalizeHealth(
      deviceHealthRaw,
    )

  // ==========================================================================
  // CORE KPI VALUES
  // ==========================================================================

  const punchesToday =
    numberValue(
      summary.totalPunchesToday ??
      summary.total_punches_today ??
      legacyToday.total ??
      summary.total_hoy ??
      root.punchesToday ??
      root.punches_today,
    )

  const punchesYesterday =
    numberValue(
      summary.totalPunchesYesterday ??
      summary.total_punches_yesterday ??
      legacyYesterday.total ??
      root.punchesYesterday ??
      root.punches_yesterday,
    )

  const employeesToday =
    numberValue(
      summary.employeesToday ??
      summary.employees_today ??
      legacyToday.empleados ??
      summary.empleados_hoy ??
      root.employeesToday ??
      root.employees_today,
    )

  const employeesYesterday =
    numberValue(
      summary.employeesYesterday ??
      summary.employees_yesterday ??
      legacyYesterday.empleados ??
      root.employeesYesterday ??
      root.employees_yesterday,
    )

  const entriesToday =
    numberValue(
      summary.entriesToday ??
      summary.entries_today ??
      summary.con_entrada ??
      root.entriesToday ??
      root.entries_today,
    )

  const exitsToday =
    numberValue(
      summary.exitsToday ??
      summary.exits_today ??
      summary.con_salida ??
      root.exitsToday ??
      root.exits_today,
    )

  const openShifts =
    numberValue(
      summary.openShifts ??
      summary.open_shifts ??
      legacyToday.sin_salida ??
      root.openShifts ??
      root.open_shifts,
      array(
        legacyOverview.open_shifts,
      ).length,
    )

  // ==========================================================================
  // BY DEVICE
  // ==========================================================================

  const byDevice =
    array(
      root.byDevice ??
      root.by_device ??
      legacyStats.by_device,
    )
      .map(
        item => {
          const row =
            object(item)

          return {
            name:
              stringValue(
                row.label ??
                row.name ??
                row.device ??
                row.dispositivo,
                'Sin identificar',
              ),

            total:
              numberValue(
                row.total ??
                row.count ??
                row.registros,
              ),
          }
        },
      )
      .filter(
        item =>
          item.total > 0,
      )

  // ==========================================================================
  // BY DEPARTMENT
  // ==========================================================================

  const byDepartment =
    array(
      root.byDepartment ??
      root.by_department ??
      legacyOverview.by_dept ??
      root.by_dept,
    )
      .map(
        item => {
          const row =
            object(item)

          return {
            name:
              stringValue(
                row.label ??
                row.depto ??
                row.department ??
                row.departamento ??
                row.name,
                'Sin departamento',
              ),

            total:
              numberValue(
                row.total ??
                row.count,
              ),
          }
        },
      )
      .filter(
        item =>
          item.total > 0,
      )

  // ==========================================================================
  // BY HOUR
  // ==========================================================================

  const byHour =
    array(
      root.byHour ??
      root.by_hour ??
      legacyStats.by_hour,
    )
      .map(
        item => {
          const row =
            object(item)

          return {
            hour:
              numberValue(
                row.hour ??
                row.hora,
              ),

            total:
              numberValue(
                row.total ??
                row.count,
              ),
          }
        },
      )
      .filter(
        item =>
          item.total > 0,
      )
      .sort(
        (
          a,
          b,
        ) =>
          a.hour -
          b.hour,
      )

  // ==========================================================================
  // BY DAY
  // ==========================================================================

  const byDay =
    array(
      root.byDay ??
      root.by_day ??
      legacyStats.by_day,
    )
      .map(
        item => {
          const row =
            object(item)

          return {
            day:
              stringValue(
                row.label ??
                row.day ??
                row.dia ??
                row.date ??
                row.fecha,
                '—',
              ),

            total:
              numberValue(
                row.total ??
                row.count,
              ),
          }
        },
      )
      .filter(
        item =>
          item.total > 0,
      )

  // ==========================================================================
  // RECENT PUNCHES
  // ==========================================================================

  const recentPunches =
    array(
      root.recent ??
      root.recentPunches ??
      root.recent_punches,
    ).map(
      item => {
        const row =
          object(item)

        return {
          id:
            row.id as
              | string
              | number
              | undefined,

          codigo:
            stringValue(
              row.codigo ??
              row.code,
            ),

          nombre:
            stringValue(
              row.nombre ??
              row.name,
            ) || null,

          departamento:
            stringValue(
              row.departamento ??
              row.department,
            ) || null,

          fecha:
            stringValue(
              row.fecha ??
              row.date,
            ) || null,

          entrada:
            stringValue(
              row.entrada ??
              row.entry,
            ) || null,

          salida:
            stringValue(
              row.salida ??
              row.exit,
            ) || null,

          dispositivo_origen:
            stringValue(
              row.dispositivo_origen ??
              row.device ??
              row.deviceName,
            ) || null,
        }
      },
    )

  // ==========================================================================
  // ACTIVITY / AUDIT
  // ==========================================================================

  const activity =
    array(
      root.activity ??
      legacyStats.activity,
    ).map(
      item => {
        const row =
          object(item)

        return {
          timestamp:
            stringValue(
              row.timestamp ??
              row.createdAt ??
              row.created_at,
            ),

          actor:
            stringValue(
              row.actor ??
              row.user ??
              row.username,
              'Sistema',
            ),

          action:
            stringValue(
              row.action ??
              row.event ??
              row.accion,
            ),

          target:
            stringValue(
              row.target ??
              row.resource ??
              row.objetivo,
            ),
        }
      },
    )

  // ==========================================================================
  // WARNINGS
  // ==========================================================================

  const warnings: string[] =
    []

  if (
    !serviceHealth.serviceOnline
  ) {
    warnings.push(
      'El servicio interno de Ponches no respondió correctamente.',
    )
  }

  if (
    !serviceHealth.databaseOnline
  ) {
    warnings.push(
      'La conexión con BioTime/SQL Server requiere revisión.',
    )
  }

  if (
    health.offline > 0
  ) {
    warnings.push(
      `${health.offline} reloj(es) biométrico(s) aparecen fuera de línea.`,
    )
  }

  if (
    openShifts > 0
  ) {
    warnings.push(
      `${openShifts} colaborador(es) tienen una entrada sin salida registrada.`,
    )
  }

  if (
    punchesToday === 0 &&
    entriesToday === 0 &&
    exitsToday === 0 &&
    recentPunches.length > 0
  ) {
    warnings.push(
      'Existen registros biométricos recientes, pero no se detectaron marcas correspondientes al día actual.',
    )
  }

  // ==========================================================================
  // DEVICE COUNTS
  //
  // device-health manda el inventario operativo real.
  //
  // Si por cualquier razón todavía no tiene datos utilizamos
  // devicesToday como fallback, pero nunca al revés.
  // ==========================================================================

  const devicesToday =
    numberValue(
      summary.devicesToday ??
      summary.devices_today ??
      summary.relojes_hoy,
    )

  const devicesTotal =
    health.total > 0
      ? health.total
      : devicesToday

  const devicesOnline =
    health.total > 0
      ? health.online
      : devicesToday

  const devicesOffline =
    health.total > 0
      ? health.offline
      : 0

  return {
    generatedAt:
      stringValue(
        root.generatedAtUtc ??
        root.generatedAt ??
        root.generated_at_utc ??
        root.generated_at ??
        root.timestamp,
        new Date()
          .toISOString(),
      ),

    serviceOnline:
      serviceHealth.serviceOnline,

    databaseOnline:
      serviceHealth.databaseOnline,

    punchesToday,
    punchesYesterday,

    employeesToday,
    employeesYesterday,

    entriesToday,
    exitsToday,

    openShifts,

    devicesTotal,
    devicesOnline,
    devicesOffline,

    byDevice,
    byDepartment,
    byHour,
    byDay,

    recentPunches,
    activity,

    healthItems:
      health.items,

    warnings,
  }
}

// ============================================================================
// STABLE TITANMDM DASHBOARD LOADER
//
// NO fallback silencioso a:
// /api/records/dashboard-combined
//
// Si el contrato estable falla, queremos verlo.
// No debemos convertir una avería en ceros falsos.
// ============================================================================

export async function loadDashboardSnapshot(
  signal?: AbortSignal,
): Promise<DashboardSnapshot> {
  const [
    healthResponse,
    dashboardResponse,
    deviceHealthResponse,
  ] =
    await Promise.all([
      titanFetch(
        '/api/ponches/health',
        {
          signal,
        },
      ),

      titanFetch(
        '/api/ponches/dashboard',
        {
          signal,
        },
      ),

      titanFetch(
        '/api/ponches/device-health',
        {
          signal,
        },
      ),
    ])

  // ==========================================================================
  // HEALTH
  //
  // Health puede fallar independientemente.
  // El Dashboard, en cambio, es obligatorio.
  // ==========================================================================

  let healthRaw:
    unknown = {}

  if (
    healthResponse.ok
  ) {
    healthRaw =
      await safeJson(
        healthResponse,
      )
  }

  const serviceHealth =
    normalizeServiceHealth(
      healthRaw,
      healthResponse.ok,
    )

  // ==========================================================================
  // DASHBOARD
  // ==========================================================================

  const dashboardRaw =
    await requireJson(
      dashboardResponse,
      'El dashboard de Ponches',
    )

  // ==========================================================================
  // DEVICE HEALTH
  //
  // No bloqueamos todo el dashboard si únicamente falla el diagnóstico
  // de dispositivos.
  // ==========================================================================

  let deviceHealthRaw:
    unknown = {}

  if (
    deviceHealthResponse.ok
  ) {
    deviceHealthRaw =
      await safeJson(
        deviceHealthResponse,
      )
  }

  return normalizeDashboard(
    dashboardRaw,
    serviceHealth,
    deviceHealthRaw,
  )
}