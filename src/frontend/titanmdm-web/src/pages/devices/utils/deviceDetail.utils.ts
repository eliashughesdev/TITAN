export function formatDeviceDate(
  value:
    | string
    | null
    | undefined,
): string {
  if (!value) {
    return 'N/D'
  }

  const date =
    new Date(value)

  if (
    Number.isNaN(
      date.getTime(),
    )
  ) {
    return 'N/D'
  }

  return date
    .toLocaleString()
}

export function getBatteryText(
  batteryLevel:
    | number
    | null,
): string {
  return batteryLevel ===
    null
    ? 'N/D'
    : `${batteryLevel}%`
}

export function getYesNo(
  value: boolean,
): string {
  return value
    ? 'Sí'
    : 'No'
}

export function getCommandStatusClass(
  status: string,
): string {
  return status
    .toLowerCase()
}