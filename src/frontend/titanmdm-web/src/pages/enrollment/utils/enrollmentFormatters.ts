import type {
  AndroidEnterpriseStatus,
  AndroidEnrollmentMode,
} from '../../../types/androidEnterprise'

export function formatDate(
  value: string,
): string {
  return new Intl.DateTimeFormat('es-DO', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function getStatusLabel(
  status: string,
): string {
  switch (status) {
    case 'Active':
      return 'Activo'

    case 'Completed':
      return 'Completado'

    case 'Expired':
      return 'Expirado'

    case 'Revoked':
      return 'Revocado'

    case 'Pending':
      return 'Pendiente'

    case 'NotConfigured':
      return 'No configurado'

    case 'Suspended':
      return 'Suspendido'

    case 'Error':
      return 'Error'

    default:
      return status
  }
}

export function getAndroidModeLabel(
  mode: AndroidEnrollmentMode | string,
): string {
  switch (mode) {
    case 'FullyManaged':
      return 'Totalmente administrado'

    case 'Dedicated':
      return 'Dedicado / Kiosk'

    case 'WorkProfile':
      return 'Perfil de trabajo'

    default:
      return mode
  }
}

export function getAndroidModeDescription(
  mode: AndroidEnrollmentMode,
): string {
  switch (mode) {
    case 'FullyManaged':
      return (
        'Dispositivo corporativo completamente ' +
        'administrado por TitanMDM. Recomendado ' +
        'para teléfonos y tabletas propiedad de la empresa.'
      )

    case 'Dedicated':
      return (
        'Dispositivo corporativo destinado a una ' +
        'función específica. Será la base para ' +
        'terminales Kiosk, POS, recepción y ' +
        'dispositivos compartidos.'
      )

    case 'WorkProfile':
      return (
        'Separa aplicaciones y datos empresariales ' +
        'de la información personal del usuario mediante ' +
        'un perfil de trabajo administrado.'
      )

    default:
      return ''
  }
}

export function isAndroidEnterpriseActive(
  status: AndroidEnterpriseStatus | null,
): boolean {
  return (
    status?.status === 'Active' &&
    Boolean(status.enterpriseName)
  )
}

export function extractRequestError(
  error: unknown,
  fallback: string,
): string {
  if (
    typeof error === 'object' &&
    error !== null &&
    'response' in error
  ) {
    const response = (
      error as {
        response?: {
          data?: {
            message?: string
          }
        }
      }
    ).response

    if (response?.data?.message) {
      return response.data.message
    }
  }

  return fallback
}

export function getEffectiveTokenStatus(
  status: string,
  expiresAtUtc: string,
): string {
  if (
    status === 'Active' &&
    new Date(expiresAtUtc) <= new Date()
  ) {
    return 'Expired'
  }

  return status
}