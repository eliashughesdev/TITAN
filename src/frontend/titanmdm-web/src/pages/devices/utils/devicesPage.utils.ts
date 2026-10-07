export function formatDeviceDateTime(
  value: string | null,
  emptyText = 'Nunca',
): string {
  if (!value) {
    return emptyText
  }

  const date =
    new Date(value)

  if (
    Number.isNaN(
      date.getTime(),
    )
  ) {
    return 'Sin información'
  }

  return date.toLocaleString()
}

export function getDevicesErrorMessage(
  error: unknown,
  fallback: string,
): string {
  if (
    typeof error ===
      'object'
    &&
    error !== null
    &&
    'response' in error
  ) {
    const response =
      (
        error as {
          response?: {
            data?: {
              message?: string
            }
          }
        }
      ).response

    if (
      response
        ?.data
        ?.message
      &&
      typeof response
        .data
        .message ===
        'string'
    ) {
      return response
        .data
        .message
    }
  }

  if (
    error instanceof Error
    &&
    error.message
  ) {
    return error.message
  }

  return fallback
}