import type { TitanModuleId } from '../../config/moduleRegistry'
import type { KioskPlatform } from './components/KioskPlatformSelector'

export function resolveKioskPlatform(
  workspaceQuery: string | null,
  activeWorkspaceId: TitanModuleId | null,
): KioskPlatform {
  const requested = workspaceQuery?.trim().toLowerCase()

  if (requested === 'windows') return 'Windows'
  if (requested === 'android') return 'Android'
  if (activeWorkspaceId === 'windows') return 'Windows'

  return 'Android'
}
