import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'

import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  androidEnterpriseApi,
} from '../../../api/androidEnterpriseApi'

import {
  AndroidRemoteActionsPanel,
} from './AndroidRemoteActionsPanel'

const auth = vi.hoisted(() => ({
  allowed: true,
}))

vi.mock('../../../auth/AuthContext', () => ({
  useAuth: () => ({
    hasPermission: () => auth.allowed,
  }),
}))

vi.mock('../../../api/androidEnterpriseApi', () => ({
  androidEnterpriseApi: {
    executeDeviceAction: vi.fn(),
  },
}))

describe('AndroidRemoteActionsPanel', () => {
  beforeEach(() => {
    auth.allowed = true
    vi.clearAllMocks()
  })

  it('does not expose controls without the actions permission', () => {
    auth.allowed = false

    render(
      <AndroidRemoteActionsPanel deviceId="device-1" />,
    )

    expect(
      screen.getByText(/android\.enterprise\.actions/),
    ).toBeInTheDocument()

    expect(
      screen.queryByRole('button', {
        name: /Bloquear/,
      }),
    ).not.toBeInTheDocument()
  })

  it('confirms reset, clears the password, and reports only issuance', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)

    vi.mocked(
      androidEnterpriseApi.executeDeviceAction,
    ).mockResolvedValue({
      deviceId: 'device-1',
      action: 'ResetPassword',
      status: 'Issued',
      googleCommandName: 'operations/1',
      correlationId: 'correlation-1',
      issuedAtUtc: '2026-10-14T12:00:00Z',
    })

    render(
      <AndroidRemoteActionsPanel deviceId="device-1" />,
    )

    const password =
      screen.getByLabelText('Nueva contraseña')

    fireEvent.change(password, {
      target: {
        value: 'temporary-secret',
      },
    })

    fireEvent.click(
      screen.getByRole('button', {
        name: /Restablecer contraseña/,
      }),
    )

    expect(window.confirm).toHaveBeenCalledOnce()

    await waitFor(() =>
      expect(
        androidEnterpriseApi.executeDeviceAction,
      ).toHaveBeenCalledWith(
        'device-1',
        {
          action: 'reset-password',
          newPassword: 'temporary-secret',
        },
      ),
    )

    expect(password).toHaveValue('')
    expect(
      await screen.findByText('Acción emitida'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/no confirma que el dispositivo haya completado/i),
    ).toBeInTheDocument()
    expect(
      screen.queryByText('temporary-secret'),
    ).not.toBeInTheDocument()
  })

  it('requires confirmation before requesting wipe or deletion', () => {
    const confirm =
      vi.spyOn(window, 'confirm')
        .mockReturnValue(false)

    render(
      <AndroidRemoteActionsPanel deviceId="device-1" />,
    )

    fireEvent.click(
      screen.getByRole('button', {
        name: /Borrar dispositivo/,
      }),
    )

    fireEvent.click(
      screen.getByRole('button', {
        name: /Eliminar de Android Enterprise/,
      }),
    )

    expect(confirm).toHaveBeenCalledTimes(2)
    expect(
      androidEnterpriseApi.executeDeviceAction,
    ).not.toHaveBeenCalled()
  })
})
