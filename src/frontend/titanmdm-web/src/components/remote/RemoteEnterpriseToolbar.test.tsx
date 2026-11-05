import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import type { RemoteSupportSignalRClient } from '../../api/remoteSupportSignalR'
import { RemoteEnterpriseToolbar, rttLevel } from './RemoteEnterpriseToolbar'

function fakeClient() {
  return {
    ping: vi.fn().mockResolvedValue(42),
    setStreamProfile: vi.fn().mockResolvedValue(undefined),
    sendSecureAttention: vi.fn().mockResolvedValue({ commandId: 'c1', status: 'Queued' }),
    keyboard: vi.fn().mockResolvedValue(undefined),
    sendFile: vi.fn().mockResolvedValue({ fileName: 'a.txt', savedAs: 'a.txt', bytes: 3 }),
  }
}

const setup = (overrides: Partial<Parameters<typeof RemoteEnterpriseToolbar>[0]> = {}) => {
  const client = fakeClient()
  const onError = vi.fn()
  const onNotice = vi.fn()

  render(
    <RemoteEnterpriseToolbar
      client={client as unknown as RemoteSupportSignalRClient}
      sessionId="s1"
      connected
      ownsControl
      allowKeyboard
      allowFileTransfer
      onError={onError}
      onNotice={onNotice}
      {...overrides}
    />,
  )

  return { client, onError, onNotice }
}

describe('RemoteEnterpriseToolbar', () => {
  it('clasifica la latencia', () => {
    expect(rttLevel(null)).toBe('unknown')
    expect(rttLevel(40)).toBe('good')
    expect(rttLevel(150)).toBe('fair')
    expect(rttLevel(400)).toBe('poor')
  })

  it('muestra el RTT medido', async () => {
    setup()
    expect(await screen.findByText('42 ms')).toBeInTheDocument()
  })

  it('aplica el perfil de calidad elegido', async () => {
    const { client } = setup()

    fireEvent.change(screen.getByLabelText('Perfil de calidad'), { target: { value: 'speed' } })

    await waitFor(() =>
      expect(client.setStreamProfile).toHaveBeenCalledWith('s1', { preset: 'speed', maxWidth: 0, maxFps: 0 }),
    )
  })

  it('envia Ctrl+Alt+Supr por el servicio del agente', async () => {
    const { client, onNotice } = setup()

    fireEvent.click(screen.getByRole('button', { name: /Ctrl\+Alt\+Supr/ }))

    await waitFor(() => expect(client.sendSecureAttention).toHaveBeenCalledWith('s1'))
    expect(onNotice).toHaveBeenCalled()
  })

  it('envia atajos presionando y soltando en orden inverso', async () => {
    const { client } = setup()

    fireEvent.change(screen.getByLabelText('Enviar combinación de teclas'), { target: { value: 'task-manager' } })

    await waitFor(() => expect(client.keyboard).toHaveBeenCalledTimes(6))
    expect(client.keyboard.mock.calls.map(call => [call[1], call[2]])).toEqual([
      [0x11, true], [0x10, true], [0x1b, true],
      [0x1b, false], [0x10, false], [0x11, false],
    ])
  })

  it('bloquea control sin la propiedad de la sesion', () => {
    setup({ ownsControl: false })

    expect(screen.getByRole('button', { name: /Ctrl\+Alt\+Supr/ })).toBeDisabled()
    expect(screen.getByRole('button', { name: /Enviar archivo/ })).toBeDisabled()
    expect(screen.getByLabelText('Perfil de calidad')).toBeDisabled()
  })

  it('deshabilita archivos si la sesion no los permite', () => {
    setup({ allowFileTransfer: false })
    expect(screen.getByRole('button', { name: /Enviar archivo/ })).toBeDisabled()
  })
})
