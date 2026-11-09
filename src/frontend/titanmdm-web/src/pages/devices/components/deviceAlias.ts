/** Mismo límite que Device.MaxAliasLength en el API. */
export const DEVICE_ALIAS_MAX_LENGTH = 64

// eslint-disable-next-line no-control-regex
const CONTROL_CHARS = /[\u0000-\u001f\u007f]/

/** Alias recortado; vacío = quitar el alias (null). */
export function normalizeDeviceAlias(raw: string): { value: string | null; error?: string } {
  const value = raw.trim()
  if (!value) return { value: null }
  if (value.length > DEVICE_ALIAS_MAX_LENGTH) {
    return { value: null, error: `Máximo ${DEVICE_ALIAS_MAX_LENGTH} caracteres.` }
  }
  if (CONTROL_CHARS.test(value)) return { value: null, error: 'El alias contiene caracteres no válidos.' }
  return { value }
}

/** Alias si existe; si no, el nombre reportado por el equipo y, en último caso, el modelo. */
export const deviceDisplayName = (device: {
  alias?: string | null
  deviceName: string
  model?: string | null
}) => device.alias?.trim() || device.deviceName.trim() || device.model?.trim() || 'Dispositivo'
