/*
 * ITAM - Filtros operativos (entregas del dia, devoluciones
 * pendientes / vencidas). Funciones puras sobre los datos del API.
 */
import type { Delivery, ReturnRecord } from './inventoryApi'
import type { OperationalReport } from './inventoryReportExport'

export type DeliveryFilter = 'all' | 'today' | 'pending' | 'overdue'
export type ReturnFilter = 'all' | 'today'

/*
 * Zona horaria del negocio. "Hoy" NO depende del reloj ni de la zona
 * configurada en el navegador del tecnico: un equipo en UTC veria las
 * entregas de 20:00-23:59 (RD) como del dia siguiente.
 */
export const BUSINESS_TIME_ZONE =
  import.meta.env.VITE_BUSINESS_TIMEZONE?.trim() || 'America/Santo_Domingo'

const dateKeyFormatters = new Map<string, Intl.DateTimeFormat>()

function dateKeyFormatter(timeZone: string): Intl.DateTimeFormat {
  let formatter = dateKeyFormatters.get(timeZone)
  if (!formatter) {
    // en-CA produce YYYY-MM-DD.
    formatter = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' })
    dateKeyFormatters.set(timeZone, formatter)
  }
  return formatter
}

/* Fecha calendario YYYY-MM-DD en la zona del negocio. */
export function localDateKey(value: Date = new Date(), timeZone: string = BUSINESS_TIME_ZONE): string {
  return dateKeyFormatter(timeZone).format(value)
}

const monthLabelFormatters = new Map<string, Intl.DateTimeFormat>()

/*
 * Etiqueta legible para las claves yyyy-MM que devuelve el backend.
 * No hay meses fijos: cualquier mes que llegue del API se formatea.
 */
export function monthKeyLabel(key: string, locale = 'es-DO'): string {
  const match = /^(\d{4})-(\d{2})$/.exec(key.trim())
  if (!match) return key

  let formatter = monthLabelFormatters.get(locale)
  if (!formatter) {
    formatter = new Intl.DateTimeFormat(locale, {
      month: 'short',
      year: '2-digit',
      timeZone: 'UTC',
    })
    monthLabelFormatters.set(locale, formatter)
  }

  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, 1))
  const label = formatter.format(date).replace(/\./g, '').trim()
  return label.charAt(0).toUpperCase() + label.slice(1)
}

/*
 * Normaliza fechas del API:
 * - "2026-11-04" (fecha calendario) se toma literal;
 * - ISO con zona ("2026-11-03T18:40:06.028520+00:00", "...Z") se
 *   convierte a la fecha de la zona del negocio;
 * - ISO sin zona (legado MongoDB) se interpreta como UTC, igual que el
 *   backend, en lugar de la hora local del navegador.
 */
export function toLocalDateKey(
  value: string | null | undefined,
  timeZone: string = BUSINESS_TIME_ZONE,
): string | null {
  if (!value) return null
  const trimmed = value.trim()
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(trimmed)
  const normalized = hasZone ? trimmed : `${trimmed.replace(' ', 'T')}Z`
  const parsed = new Date(normalized)
  return Number.isNaN(parsed.getTime()) ? null : localDateKey(parsed, timeZone)
}

export const isToday = (value: string | null | undefined, now: Date = new Date()) =>
  toLocalDateKey(value) === localDateKey(now)

/**
 * Las entregas anteriores a las devoluciones individuales solo tienen
 * `returned`: si es true, la entrega se cerró completa (misma regla que
 * DeliveryReturnState en el API).
 */
export const isHandheldReturned = (delivery: Delivery) =>
  Boolean(delivery.returnedHandheld || delivery.returned)

export const isRp4dReturned = (delivery: Delivery) =>
  !delivery.includeRp4d || Boolean(delivery.returnedRp4d || delivery.returned)

export const isDeliveryComplete = (delivery: Delivery) =>
  isHandheldReturned(delivery) && isRp4dReturned(delivery)

export const isPendingReturn = (delivery: Delivery) =>
  !isDeliveryComplete(delivery) && !delivery.deleted

export function overdueDays(delivery: Delivery, now: Date = new Date()): number {
  if (!isPendingReturn(delivery)) return 0
  const expected = toLocalDateKey(delivery.expectedReturn)
  if (!expected) return 0
  const today = localDateKey(now)
  if (expected >= today) return 0
  // Diferencia entre fechas calendario (UTC puro: sin horario de verano).
  const diff = Date.parse(`${today}T00:00:00Z`) - Date.parse(`${expected}T00:00:00Z`)
  return Math.round(diff / 86_400_000)
}

export function filterDeliveries(rows: Delivery[], filter: DeliveryFilter, now: Date = new Date()): Delivery[] {
  switch (filter) {
    case 'today':
      return rows.filter(row => isToday(row.deliveryDate, now))
    case 'pending':
      return rows.filter(isPendingReturn)
    case 'overdue':
      return rows.filter(row => overdueDays(row, now) > 0)
    default:
      return rows
  }
}

export function filterReturns(rows: ReturnRecord[], filter: ReturnFilter, now: Date = new Date()): ReturnRecord[] {
  return filter === 'today' ? rows.filter(row => isToday(row.returnDate, now)) : rows
}

/** Valor del parámetro `period` que entiende GET /api/itam/deliveries. */
export const deliveryFilterParams: Record<DeliveryFilter, string> = {
  all: 'all',
  today: 'today',
  pending: 'pending-returns',
  overdue: 'expired',
}

export const deliveryFilterLabels: Record<DeliveryFilter, string> = {
  all: 'Todas',
  today: 'Entregas de hoy',
  pending: 'Devoluciones pendientes',
  overdue: 'Vencidas',
}

export function deliveryReport(rows: Delivery[], filter: DeliveryFilter, now: Date = new Date()): OperationalReport {
  const scope =
    filter === 'today'
      ? `Entregas del ${localDateKey(now)}`
      : filter === 'all'
        ? 'Todas las entregas'
        : deliveryFilterLabels[filter]

  return {
    title: filter === 'pending' || filter === 'overdue' ? 'Devoluciones pendientes' : 'Entregas de equipos',
    fileBase: `TitanMDM-${filter === 'today' ? 'Entregas-Hoy' : filter === 'all' ? 'Entregas' : 'Devoluciones-Pendientes'}`,
    scope,
    headers: ['Activo', 'Equipo', 'Serie', 'Empleado', 'Código', 'Fecha entrega', 'Retorno previsto', 'Días vencida', 'Entregado por', 'Estado'],
    rows: rows.map(row => [
      row.assetTag ?? '',
      row.assetName ?? '',
      row.serialNumber ?? '',
      row.assignedToName ?? '',
      row.employeeCode ?? '',
      toLocalDateKey(row.deliveryDate) ?? '',
      toLocalDateKey(row.expectedReturn) ?? '',
      overdueDays(row, now) || '',
      row.deliveredBy ?? '',
      isDeliveryComplete(row) ? 'Completada' : 'Pendiente',
    ]),
  }
}

export function returnReport(rows: ReturnRecord[], filter: ReturnFilter, now: Date = new Date()): OperationalReport {
  return {
    title: 'Devoluciones recibidas',
    fileBase: filter === 'today' ? 'TitanMDM-Devoluciones-Hoy' : 'TitanMDM-Devoluciones',
    scope: filter === 'today' ? `Devoluciones del ${localDateKey(now)}` : 'Todas las devoluciones',
    headers: ['Activo', 'Equipo', 'Devuelve', 'Código', 'Condición', 'Fecha', 'Recibe', 'Notas'],
    rows: rows.map(row => [
      row.assetTag ?? '',
      row.assetName ?? '',
      row.returnedBy ?? '',
      row.employeeCode ?? '',
      row.condition ?? '',
      toLocalDateKey(row.returnDate) ?? '',
      row.receivedBy ?? '',
      row.notes ?? '',
    ]),
  }
}
