import axios from 'axios'

export function formatDate(
  value: string | null | undefined,
): string {
  if (!value) {
    return 'Nunca'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return 'N/D'
  }

  return date.toLocaleString()
}

export function formatDuration(
  value: number | null | undefined,
): string {
  if (
    value === null ||
    value === undefined
  ) {
    return '—'
  }

  if (value < 1000) {
    return `${value} ms`
  }

  return `${(value / 1000).toFixed(2)} s`
}

export function isPlatformUnavailable(
  error: unknown,
): boolean {
  return (
    axios.isAxiosError(error) &&
    error.response?.status === 503
  )
}

export function describeAutomationError(
  error: unknown,
  fallback: string,
): string {
  if (isPlatformUnavailable(error)) {
    return (
      'El motor de automatizaciones ' +
      'empresarial no está registrado ' +
      'en el servidor (HTTP 503).'
    )
  }

  if (axios.isAxiosError(error)) {
    const status = error.response?.status

    if (status === 403) {
      return 'No tienes permisos para esta operación.'
    }

    if (status === 404) {
      return 'El recurso solicitado no existe.'
    }

    if (status === 401) {
      return 'La sesión no es válida. Vuelve a iniciar sesión.'
    }
  }

  return fallback
}
