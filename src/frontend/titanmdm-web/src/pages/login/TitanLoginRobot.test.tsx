import {
  afterEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'
import {
  act,
  render,
  screen,
} from '@testing-library/react'

import {
  TitanLoginRobot,
  type TitanLoginRobotMode,
} from './TitanLoginRobot'

const BOOT_MS = 1000

function mockMatchMedia(matches: boolean): void {
  window.matchMedia = vi.fn().mockImplementation(
    (query: string) => ({
      matches,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }),
  ) as unknown as typeof window.matchMedia
}

function renderRobot(
  mode: TitanLoginRobotMode,
  overrides: {
    emailValue?: string
    showPassword?: boolean
  } = {},
) {
  return render(
    <TitanLoginRobot
      mode={mode}
      emailValue={overrides.emailValue ?? ''}
      showPassword={overrides.showPassword ?? false}
    />,
  )
}

function rootOf(container: HTMLElement): HTMLElement {
  return container.firstElementChild as HTMLElement
}

describe('TitanLoginRobot', () => {
  afterEach(() => {
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  it('arranca con la secuencia de encendido y luego queda en espera', () => {
    vi.useFakeTimers()
    mockMatchMedia(false)

    const { container } = renderRobot('idle')
    const root = rootOf(container)

    expect(root.className).toContain('titan-mech--idle')
    expect(root.className).toContain('titan-mech--booting')
    expect(
      screen.getByText('Sistema Titan encendiendo...'),
    ).toBeInTheDocument()

    act(() => {
      vi.advanceTimersByTime(BOOT_MS)
    })

    expect(root.className).not.toContain('titan-mech--booting')
    expect(
      screen.getByText(
        'Hola. Soy Titan, tu guardián de acceso.',
      ),
    ).toBeInTheDocument()

    expect(root).toHaveAttribute(
      'data-animation-state',
      'greeting',
    )

    act(() => {
      vi.advanceTimersByTime(800)
    })

    expect(
      screen.getByText(
        'Sistema de acceso Titan en espera.',
      ),
    ).toBeInTheDocument()
  })

  it('omite la secuencia de encendido con movimiento reducido', () => {
    vi.useFakeTimers()
    mockMatchMedia(true)

    const { container } = renderRobot('idle')

    expect(rootOf(container).className).not.toContain(
      'titan-mech--booting',
    )
    expect(rootOf(container)).toHaveAttribute(
      'data-animation-state',
      'reduced-motion',
    )
  })

  it.each<[TitanLoginRobotMode, string]>([
    ['email', 'Identificación del operador.'],
    [
      'authenticating',
      'Validando identidad y autorización...',
    ],
    ['error', 'Acceso no validado.'],
    [
      'success',
      'Identidad confirmada. Acceso autorizado.',
    ],
    ['launch', 'Inicializando TitanMDM...'],
  ])(
    'muestra el mensaje esperado para el modo %s',
    (mode, expected) => {
      vi.useFakeTimers()
      mockMatchMedia(false)

      renderRobot(mode)

      act(() => {
        vi.advanceTimersByTime(BOOT_MS)
      })

      expect(
        screen.getByText(expected),
      ).toBeInTheDocument()
    },
  )

  it('mantiene el mensaje de privacidad sin exponer la clave', () => {
    vi.useFakeTimers()
    mockMatchMedia(false)

    const { container } = renderRobot('password')

    act(() => {
      vi.advanceTimersByTime(BOOT_MS)
    })

    expect(rootOf(container).className).toContain(
      'titan-mech--password',
    )
    expect(
      screen.getByText(
        'Protocolo de privacidad activo.',
      ),
    ).toBeInTheDocument()
  })

  it('reacciona brevemente al cambiar de campo y luego se relaja', () => {
    vi.useFakeTimers()
    mockMatchMedia(false)

    const { container, rerender } = renderRobot('email')

    act(() => {
      vi.advanceTimersByTime(BOOT_MS)
    })

    rerender(
      <TitanLoginRobot
        mode="password"
        emailValue=""
        showPassword={false}
      />,
    )

    const root = rootOf(container)

    expect(root.className).toContain(
      'titan-mech--react-field',
    )

    act(() => {
      vi.advanceTimersByTime(800)
    })

    expect(root.className).not.toContain(
      'titan-mech--react-field',
    )
  })

  it('interrumpe el encendido si entra en autenticación', () => {
    vi.useFakeTimers()
    mockMatchMedia(false)

    const { container, rerender } = renderRobot('idle')

    act(() => {
      vi.advanceTimersByTime(100)
    })

    rerender(
      <TitanLoginRobot
        mode="authenticating"
        emailValue=""
        showPassword={false}
      />,
    )

    expect(rootOf(container).className).not.toContain(
      'titan-mech--booting',
    )
  })
})
