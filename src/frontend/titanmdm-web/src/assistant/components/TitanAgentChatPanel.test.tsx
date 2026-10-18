import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'
import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'

import type {
  TitanCapabilities,
  TitanTurnResponse,
} from '../api/titanAssistantApi'

const contextMocks = vi.hoisted(() => ({
  setAnimation: vi.fn(),
  clearMessage: vi.fn(),
}))

vi.mock('../context/TitanAssistantContext', () => ({
  useTitanAssistant: () => ({
    user: {
      id: 'user-1',
      organizationId: 'org-1',
      firstName: 'Cesar',
      lastName: 'Iglesias',
      fullName: 'Cesar Iglesias',
      email: 'cesar@example.com',
      roles: ['Operator'],
      permissions: ['devices.view'],
    },
    page: {
      pathname: '/devices',
      module: 'devices',
    },
    preferences: {
      reducedMotion: false,
    },
    setAnimation: contextMocks.setAnimation,
    clearMessage: contextMocks.clearMessage,
  }),
}))

vi.mock('../api/titanAssistantApi', async (
  importOriginal,
) => {
  const actual =
    await importOriginal<
      typeof import('../api/titanAssistantApi')
    >()

  return {
    ...actual,
    titanAssistantApi: {
      getCapabilities: vi.fn(),
      sendMessage: vi.fn(),
      listConversations: vi.fn(),
      getConversation: vi.fn(),
      deleteConversation: vi.fn(),
    },
  }
})

import { titanAssistantApi } from '../api/titanAssistantApi'
import { TitanAgentChatPanel } from './TitanAgentChatPanel'

const onlineCapabilities: TitanCapabilities = {
  enabled: true,
  available: true,
  provider: 'OpenRouter',
  model: 'openrouter/free',
  tools: [
    {
      name: 'devices.list',
      title: 'Listar dispositivos',
      description: 'Inventario de dispositivos',
      kind: 0,
      requiredPermissions: ['devices.view'],
    },
  ],
}

const successfulTurn: TitanTurnResponse = {
  conversationId: 'conv-1',
  answer: 'Hay 3 dispositivos registrados.',
  provider: 'OpenRouter',
  model: 'openrouter/free',
  toolInvocations: [
    {
      tool: 'devices.list',
      kind: 'Read',
      startedAtUtc: '2026-01-01T00:00:00Z',
      durationMs: 14,
      success: true,
    },
  ],
  availableTools: ['devices.list'],
  cancelled: false,
}

function axiosError(
  overrides: Record<string, unknown>,
) {
  return {
    isAxiosError: true,
    ...overrides,
  }
}

function renderPanel() {
  const onClose = vi.fn()

  return {
    onClose,
    ...render(
      <TitanAgentChatPanel onClose={onClose} />,
    ),
  }
}

describe('TitanAgentChatPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()

    Element.prototype.scrollIntoView =
      vi.fn() as unknown as typeof Element.prototype.scrollIntoView
  })

  it('reports the active provider and authorized tools', async () => {
    vi.mocked(
      titanAssistantApi.getCapabilities,
    ).mockResolvedValue(onlineCapabilities)

    renderPanel()

    expect(
      await screen.findByText(/OpenRouter ·/),
    ).toBeInTheDocument()

    expect(
      screen.getByText('Listar dispositivos'),
    ).toBeInTheDocument()

    expect(titanAssistantApi.getCapabilities).toHaveBeenCalledTimes(
      1,
    )
  })

  it('blocks the conversation when the account is not enabled', async () => {
    vi.mocked(
      titanAssistantApi.getCapabilities,
    ).mockRejectedValue(
      axiosError({ response: { status: 403 } }),
    )

    const { container } = renderPanel()

    expect(
      await screen.findByText(
        /no tiene habilitado el asistente/i,
      ),
    ).toBeInTheDocument()

    expect(
      (container.ownerDocument ?? document).querySelector(
        '.titan-agent-chat__status--forbidden',
      ),
    ).not.toBeNull()
  })

  it('surfaces network failures without inventing a tool answer', async () => {
    vi.mocked(
      titanAssistantApi.getCapabilities,
    ).mockResolvedValue(onlineCapabilities)

    vi.mocked(
      titanAssistantApi.sendMessage,
    ).mockRejectedValue(
      axiosError({ response: undefined }),
    )

    renderPanel()

    await screen.findByText(/OpenRouter ·/)

    fireEvent.change(
      screen.getByLabelText('Mensaje para Titan'),
      { target: { value: 'cuantos dispositivos' } },
    )

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Enviar mensaje',
      }),
    )

    const failures = await screen.findAllByText(
      /No hay conexión con el servicio del asistente/i,
    )

    expect(failures.length).toBeGreaterThanOrEqual(1)

    expect(
      titanAssistantApi.sendMessage,
    ).toHaveBeenCalledWith(
      {
        conversationId: null,
        message: 'cuantos dispositivos',
        module: 'devices',
      },
      expect.any(AbortSignal),
    )
  })

  it('renders the assistant answer with tool provenance', async () => {
    vi.mocked(
      titanAssistantApi.getCapabilities,
    ).mockResolvedValue(onlineCapabilities)

    vi.mocked(
      titanAssistantApi.sendMessage,
    ).mockResolvedValue(successfulTurn)

    renderPanel()

    await screen.findByText(/OpenRouter ·/)

    fireEvent.change(
      screen.getByLabelText('Mensaje para Titan'),
      { target: { value: 'cuantos dispositivos hay' } },
    )

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Enviar mensaje',
      }),
    )

    expect(
      await screen.findByText(
        'Hay 3 dispositivos registrados.',
      ),
    ).toBeInTheDocument()

    await waitFor(() => {
      expect(
        screen.getByText(/devices\.list · 14 ms · OK/),
      ).toBeInTheDocument()
    })
  })

  it('cancels an in-flight consultation through the abort signal', async () => {
    vi.mocked(
      titanAssistantApi.getCapabilities,
    ).mockResolvedValue(onlineCapabilities)

    vi.mocked(
      titanAssistantApi.sendMessage,
    ).mockImplementation(
      (_request, signal) =>
        new Promise((_resolve, reject) => {
          signal?.addEventListener('abort', () => {
            reject(
              axiosError({ code: 'ERR_CANCELED' }),
            )
          })
        }),
    )

    renderPanel()

    await screen.findByText(/OpenRouter ·/)

    fireEvent.change(
      screen.getByLabelText('Mensaje para Titan'),
      { target: { value: 'consulta en curso' } },
    )

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Enviar mensaje',
      }),
    )

    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Cancelar consulta',
      }),
    )

    expect(
      await screen.findByText('Consulta cancelada.'),
    ).toBeInTheDocument()
  })
})
