import {
  describe,
  expect,
  it,
} from 'vitest'
import { resolveRequiredWorkspacePermission } from './workspaceAccess'

describe('resolveRequiredWorkspacePermission', () => {
  it('classifies the Android control center as Android', () => {
    expect(
      resolveRequiredWorkspacePermission('/android/control-center', ''),
    ).toBe('workspace.android.view')
  })

  it('keeps per-device Windows control centers in the Windows workspace', () => {
    expect(
      resolveRequiredWorkspacePermission(
        '/devices/device-1/control-center',
        '',
      ),
    ).toBe('workspace.windows.view')
  })

  it('classifies the fleet map as Android without relying on the query', () => {
    expect(
      resolveRequiredWorkspacePermission('/fleet-map', ''),
    ).toBe('workspace.android.view')
  })
})
