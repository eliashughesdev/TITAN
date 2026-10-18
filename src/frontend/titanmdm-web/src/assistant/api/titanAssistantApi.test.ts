import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import apiClient from '../../api/apiClient'
import {
  describeTitanFailure,
  titanAssistantApi,
} from './titanAssistantApi'

vi.mock('../../api/apiClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}))

function axiosError(
  overrides: Record<string, unknown>,
) {
  return {
    isAxiosError: true,
    ...overrides,
  }
}

describe('titanAssistantApi contracts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads capabilities from the real assistant service', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({
      data: {
        enabled: true,
        available: true,
        provider: 'OpenRouter',
        model: 'openrouter/free',
        tools: [
          {
            name: 'devices.list',
            title: 'Listar dispositivos',
            description: 'Inventario',
            kind: 0,
            requiredPermissions: ['devices.view'],
          },
        ],
      },
    } as never)

    const capabilities =
      await titanAssistantApi.getCapabilities()

    expect(apiClient.get).toHaveBeenCalledWith(
      '/titan-assistant/capabilities',
    )

    expect(capabilities.provider).toBe('OpenRouter')
    expect(capabilities.tools).toHaveLength(1)
  })

  it('normalizes missing capability fields safely', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({
      data: {
        enabled: false,
        available: false,
      },
    } as never)

    const capabilities =
      await titanAssistantApi.getCapabilities()

    expect(capabilities.provider).toBe('none')
    expect(capabilities.model).toBe('')
    expect(capabilities.tools).toEqual([])
  })

  it('sends a message with conversation, module and abort signal', async () => {
    const controller = new AbortController()

    vi.mocked(apiClient.post).mockResolvedValue({
      data: {
        conversationId: 'conv-1',
        answer: 'Hay 3 dispositivos.',
        provider: 'OpenRouter',
        model: 'openrouter/free',
        toolInvocations: [
          {
            tool: 'devices.list',
            kind: 'Read',
            startedAtUtc: '2026-01-01T00:00:00Z',
            durationMs: 12,
            success: true,
          },
        ],
        availableTools: ['devices.list'],
        cancelled: false,
      },
    } as never)

    const response =
      await titanAssistantApi.sendMessage(
        {
          conversationId: null,
          message: '¿Cuántos dispositivos?',
          module: 'devices',
        },
        controller.signal,
      )

    expect(apiClient.post).toHaveBeenCalledWith(
      '/titan-assistant/messages',
      {
        conversationId: null,
        message: '¿Cuántos dispositivos?',
        module: 'devices',
      },
      { signal: controller.signal },
    )

    expect(response.toolInvocations).toHaveLength(1)
    expect(response.cancelled).toBe(false)
  })

  it('unwraps conversation summaries', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({
      data: {
        conversations: [
          {
            id: 'conv-1',
            title: 'Dispositivos',
            createdAtUtc: '2026-01-01T00:00:00Z',
            updatedAtUtc: '2026-01-01T00:05:00Z',
            messageCount: 4,
          },
        ],
      },
    } as never)

    const items =
      await titanAssistantApi.listConversations()

    expect(apiClient.get).toHaveBeenCalledWith(
      '/titan-assistant/conversations',
    )

    expect(items).toHaveLength(1)
  })

  it('normalizes a conversation with missing entries', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({
      data: {
        id: 'conv-1',
        title: 'Sesión',
        createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z',
      },
    } as never)

    const conversation =
      await titanAssistantApi.getConversation('conv-1')

    expect(apiClient.get).toHaveBeenCalledWith(
      '/titan-assistant/conversations/conv-1',
    )

    expect(conversation.entries).toEqual([])
  })

  it('deletes a conversation through the tenant-scoped route', async () => {
    vi.mocked(apiClient.delete).mockResolvedValue({
      data: undefined,
    } as never)

    await titanAssistantApi.deleteConversation('conv-9')

    expect(apiClient.delete).toHaveBeenCalledWith(
      '/titan-assistant/conversations/conv-9',
    )
  })
})

describe('describeTitanFailure', () => {
  it('maps permission denials to forbidden', () => {
    const failure = describeTitanFailure(
      axiosError({
        response: {
          status: 403,
          data: {
            message:
              'Tu cuenta no tiene habilitado el asistente.',
          },
        },
      }),
    )

    expect(failure.kind).toBe('forbidden')
    expect(failure.message).toContain('no tiene')
  })

  it('maps expired sessions to unauthorized', () => {
    const failure = describeTitanFailure(
      axiosError({ response: { status: 401 } }),
    )

    expect(failure.kind).toBe('unauthorized')
  })

  it('maps provider outages to unavailable', () => {
    const failure = describeTitanFailure(
      axiosError({
        response: {
          status: 503,
          data: {
            message: 'El asistente no está disponible.',
          },
        },
      }),
    )

    expect(failure.kind).toBe('unavailable')
  })

  it('maps absent responses to network failures', () => {
    const failure = describeTitanFailure(
      axiosError({ response: undefined }),
    )

    expect(failure.kind).toBe('network')
  })

  it('maps aborted requests to cancelled', () => {
    const failure = describeTitanFailure(
      axiosError({ code: 'ERR_CANCELED' }),
    )

    expect(failure.kind).toBe('cancelled')
  })

  it('maps timeouts to timeout', () => {
    const failure = describeTitanFailure(
      axiosError({ code: 'ETIMEDOUT' }),
    )

    expect(failure.kind).toBe('timeout')
  })

  it('never echoes unknown payloads to the user', () => {
    const failure = describeTitanFailure(
      new Error('some internal provider detail'),
    )

    expect(failure.kind).toBe('unknown')
    expect(failure.message).not.toContain('internal provider')
  })
})
