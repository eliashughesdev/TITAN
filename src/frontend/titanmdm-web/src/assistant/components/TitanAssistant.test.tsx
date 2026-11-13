import { act, fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { getTitanModule, type TitanModuleId } from '../../config/moduleRegistry'
import { resolveTitanTheme } from '../config/titanTheme'

const state = vi.hoisted(() => ({
  pathname: '/devices',
  workspace: 'android' as string | null,
}))

vi.mock('../context/TitanAssistantContext', () => ({
  useTitanAssistant: () => ({
    user: { id: 'u1', firstName: 'Ana' },
    page: { pathname: state.pathname, module: 'devices' },
    preferences: {
      visible: true,
      doNotDisturb: false,
      autonomousBehavior: true,
      proactiveComments: false,
      followPointer: false,
      reducedMotion: true,
      allowFlight: false,
      soundEnabled: false,
    },
    runtime: { message: null, priority: 'idle', animation: 'idle', direction: 'right' },
    say: vi.fn(),
    clearMessage: vi.fn(),
  }),
}))

vi.mock('../../workspace/WorkspaceContext', async () => {
  const registry = await vi.importActual<typeof import('../../config/moduleRegistry')>(
    '../../config/moduleRegistry',
  )
  return {
    useOptionalWorkspace: () => ({
      activeWorkspaceId: state.workspace,
      activeModule: state.workspace
        ? registry.getTitanModule(state.workspace as TitanModuleId) ?? null
        : null,
    }),
  }
})

vi.mock('./TitanAgentChatPanel', () => ({
  TitanAgentChatPanel: () => <div data-testid="titan-chat">chat</div>,
}))

vi.mock('./TitanPreferencesPanel', () => ({
  TitanPreferencesPanel: () => <div data-testid="titan-preferences" />,
}))

import { TitanAssistant } from './TitanAssistant'

function dock(): HTMLElement {
  const element = document.querySelector<HTMLElement>('.titan-dock')
  if (!element) throw new Error('Titan no está montado')
  return element
}

describe('TitanAssistant', () => {
  beforeEach(() => {
    state.pathname = '/devices'
    state.workspace = 'android'
    window.localStorage.clear()
    document.body.innerHTML = ''
  })

  it('no aparece en Ponches, ni por ruta ni por espacio de trabajo', () => {
    state.pathname = '/ponches/records'
    const { container, rerender } = render(<TitanAssistant />)
    expect(container.querySelector('.titan-dock')).toBeNull()

    state.pathname = '/dashboard'
    state.workspace = 'ponches'
    rerender(<TitanAssistant />)
    expect(container.querySelector('.titan-dock')).toBeNull()
  })

  it('toma el color del módulo activo en sus LED', () => {
    const { rerender } = render(<TitanAssistant />)
    const android = resolveTitanTheme(getTitanModule('android'))
    expect(dock().style.getPropertyValue('--titan-led')).toBe(android.led)
    expect(dock().style.getPropertyValue('--titan-accent')).toBe('#16a34a')

    state.workspace = 'inventory'
    rerender(<TitanAssistant />)
    expect(dock().style.getPropertyValue('--titan-accent')).toBe(
      getTitanModule('inventory')?.theme.primary,
    )
  })

  it('se vuelve translúcido mientras el usuario escribe en un formulario', () => {
    const field = document.createElement('input')
    document.body.appendChild(field)
    render(<TitanAssistant />)
    expect(dock().dataset.mode).toBe('active')

    act(() => field.focus())
    expect(dock().dataset.mode).toBe('translucent')
  })

  it('se aparta cuando hay un diálogo abierto', async () => {
    render(<TitanAssistant />)
    const dialog = document.createElement('div')
    dialog.setAttribute('role', 'dialog')

    await act(async () => {
      document.body.appendChild(dialog)
      await new Promise(resolve => setTimeout(resolve, 30))
    })

    expect(dock().dataset.mode).toBe('hidden')
  })

  it('se minimiza a una burbuja y lo recuerda', () => {
    render(<TitanAssistant />)
    fireEvent.click(screen.getByRole('button', { name: 'Minimizar a Titan' }))

    expect(dock().dataset.minimized).toBe('true')
    expect(window.localStorage.getItem('titanmdm:titan-minimized')).toBe('true')
    expect(screen.getByRole('button', { name: /Mostrar a Titan/ })).toBeTruthy()
  })

  it('Alt+T abre la conversación aunque esté minimizado', () => {
    window.localStorage.setItem('titanmdm:titan-minimized', 'true')
    render(<TitanAssistant />)
    expect(screen.queryByTestId('titan-chat')).toBeNull()

    fireEvent.keyDown(window, { key: 't', altKey: true })
    expect(screen.getByTestId('titan-chat')).toBeTruthy()
    expect(dock().dataset.minimized).toBe('false')
  })
})
