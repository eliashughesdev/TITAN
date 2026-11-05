import {
  render,
  screen,
} from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'
import { ModuleNavigation } from './ModuleNavigation'

const authorization = vi.hoisted(() => ({
  permissions: new Set<string>(),
}))

vi.mock('../../auth/AuthContext', () => ({
  useAuth: () => ({
    hasPermission: (permission: string) =>
      authorization.permissions.has(permission),
  }),
}))

vi.mock('../../workspace/WorkspaceContext', () => ({
  useWorkspace: () => ({
    activeWorkspaceId: 'android',
  }),
}))

describe('ModuleNavigation Android', () => {
  beforeEach(() => {
    authorization.permissions = new Set([
      'workspace.android.view',
      'android.enterprise.view',
    ])
  })

  it('shows the control center in the active Android top bar', () => {
    render(
      <MemoryRouter initialEntries={['/android/control-center?workspace=android']}>
        <ModuleNavigation />
      </MemoryRouter>,
    )

    expect(
      screen.getByRole('link', { name: /Centro de Control/ }),
    ).toHaveAttribute(
      'href',
      '/android/control-center?workspace=android',
    )
  })

  it('links the fleet map and geofencing modules for Android geolocation', () => {
    authorization.permissions.add('geofencing.view')

    render(
      <MemoryRouter initialEntries={['/android/control-center?workspace=android']}>
        <ModuleNavigation />
      </MemoryRouter>,
    )

    expect(
      screen.getByRole('link', { name: /Mapa de Flota/ }),
    ).toHaveAttribute(
      'href',
      '/fleet-map?workspace=android',
    )

    expect(
      screen.getByRole('link', { name: /Geofencing/ }),
    ).toHaveAttribute(
      'href',
      '/geofencing?workspace=android',
    )
  })

  it('hides both geolocation modules without geofencing.view', () => {
    render(
      <MemoryRouter initialEntries={['/dashboard?workspace=android']}>
        <ModuleNavigation />
      </MemoryRouter>,
    )

    expect(
      screen.queryByRole('link', { name: /Mapa de Flota/ }),
    ).not.toBeInTheDocument()

    expect(
      screen.queryByRole('link', { name: /Geofencing/ }),
    ).not.toBeInTheDocument()
  })

  it('hides the control center without Android Enterprise view access', () => {
    authorization.permissions = new Set(['workspace.android.view'])

    render(
      <MemoryRouter initialEntries={['/dashboard?workspace=android']}>
        <ModuleNavigation />
      </MemoryRouter>,
    )

    expect(
      screen.queryByRole('link', { name: /Centro de Control/ }),
    ).not.toBeInTheDocument()
  })
})
