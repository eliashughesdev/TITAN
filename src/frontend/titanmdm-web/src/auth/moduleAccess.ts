import type {
  TitanModuleDefinition,
} from '../config/moduleRegistry'

export function canAccessModule(
  module:
    TitanModuleDefinition,

  permissions:
    readonly string[],
): boolean {
  if (
    !module.enabled
  ) {
    return false
  }

  return module.permissions.some(
    requiredPermission =>
      permissions.includes(
        requiredPermission,
      ),
  )
}