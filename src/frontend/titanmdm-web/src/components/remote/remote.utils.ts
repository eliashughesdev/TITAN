export function formatRemoteDate(
  value?: string | null,
): string {
  if (!value) {
    return '—'
  }

  return new Date(
    value,
  ).toLocaleString()
}

export function remoteStatusLabel(
  status?: string,
): string {
  switch (status) {
    case 'Requested':
      return 'Solicitada'

    case 'Connecting':
      return 'Conectando'

    case 'Connected':
      return 'Conectada'

    case 'Disconnecting':
      return 'Desconectando'

    case 'Completed':
      return 'Finalizada'

    case 'Failed':
      return 'Error'

    case 'Expired':
      return 'Expirada'

    case 'Cancelled':
      return 'Cancelada'

    default:
      return (
        status ??
        'Sin sesión'
      )
  }
}

export function isRemoteTerminal(
  status: string,
): boolean {
  return [
    'Completed',
    'Failed',
    'Expired',
    'Cancelled',
  ].includes(
    status,
  )
}