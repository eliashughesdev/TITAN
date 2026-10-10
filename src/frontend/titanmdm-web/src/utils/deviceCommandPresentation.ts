import type {
  DeviceCommand,
  DeviceCommandStatus,
} from '../api/deviceCommandsApi'

const labels:
  Record<DeviceCommandStatus, string> = {
    Pending: 'Solicitando',
    Queued: 'En cola',
    Dispatching: 'Preparando entrega',
    Sent: 'Enviado al agente',
    Delivered: 'Entregado al agente',
    Executing: 'Ejecutando',
    Success: 'Ejecutado correctamente',
    Failed: 'Fallido',
    Timeout: 'Expirado',
    Cancelled: 'Cancelado',
  }

export function getCommandStatusLabel(
  status: DeviceCommandStatus,
): string {
  return labels[status]
}

export function isTerminalCommand(
  status: DeviceCommandStatus,
): boolean {
  return status === 'Success' ||
    status === 'Failed' ||
    status === 'Timeout' ||
    status === 'Cancelled'
}

export function mergeCommand(
  commands: DeviceCommand[],
  update: DeviceCommand,
): DeviceCommand[] {
  const existing = commands.find(
    command => command.id === update.id,
  )

  if (
    existing &&
    new Date(existing.updatedAtUtc).getTime() >
      new Date(update.updatedAtUtc).getTime()
  ) {
    return commands
  }

  return [
    update,
    ...commands.filter(
      command => command.id !== update.id,
    ),
  ].sort(
    (left, right) =>
      new Date(right.createdAtUtc).getTime() -
      new Date(left.createdAtUtc).getTime(),
  )
}

export function formatCommandElapsed(
  command: DeviceCommand,
  now = Date.now(),
): string {
  const start = new Date(command.createdAtUtc).getTime()
  const completed = command.completedAtUtc
    ? new Date(command.completedAtUtc).getTime()
    : now

  const seconds = Math.max(
    0,
    Math.floor((completed - start) / 1000),
  )

  if (seconds < 60) {
    return `${seconds} s`
  }

  const minutes = Math.floor(seconds / 60)
  const remainder = seconds % 60
  return `${minutes} min ${remainder} s`
}

export function commandProgressMessage(
  command: DeviceCommand,
): string {
  const label = getCommandStatusLabel(command.status)

  if (command.status === 'Failed') {
    return command.errorMessage
      ? `${label}: ${command.errorMessage}`
      : label
  }

  if (command.status === 'Timeout') {
    return 'El comando expiró sin confirmación de ejecución.'
  }

  if (command.status === 'Cancelled') {
    return 'El comando fue cancelado antes de ejecutarse.'
  }

  return `${command.commandType}: ${label}.`
}
