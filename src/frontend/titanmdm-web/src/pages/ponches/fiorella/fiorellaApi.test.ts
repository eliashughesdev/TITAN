import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import apiClient from '../../../api/apiClient'
import { fiorellaApi } from './fiorellaApi'

vi.mock('../../../api/apiClient', () => ({
  default: {
    post: vi.fn(),
  },
}))

describe('fiorellaApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('uses the authenticated TitanMDM gateway contract', async () => {
    const controller = new AbortController()

    vi.mocked(apiClient.post).mockResolvedValue({
      data: {
        respuesta: 'Se registraron 42 ponches hoy.',
        animacion: 'point',
        conversation_id: 17,
        tools_used: ['punch_summary'],
        provider: 'openrouter',
        model_used: 'openrouter/free',
      },
    } as never)

    const response = await fiorellaApi.sendMessage(
      {
        message: '¿Cuántos ponches hay hoy?',
        activeModule: '/ponches?section=dashboard',
        conversationId: '17',
      },
      controller.signal,
    )

    expect(apiClient.post).toHaveBeenCalledWith(
      '/ponches/fiorella/chat',
      {
        message: '¿Cuántos ponches hay hoy?',
        active_module: '/ponches?section=dashboard',
        conversation_id: 17,
      },
      { signal: controller.signal },
    )
    expect(response.answer).toContain('42')
    expect(response.toolsUsed).toEqual(['punch_summary'])
    expect(response.conversationId).toBe('17')
  })

  it('does not forward an invalid local conversation id', async () => {
    vi.mocked(apiClient.post).mockResolvedValue({
      data: {
        respuesta: 'Sin registros.',
        conversation_id: null,
      },
    } as never)

    await fiorellaApi.sendMessage({
      message: 'Consulta',
      activeModule: '/dashboard',
      conversationId: 'invalid',
    })

    expect(apiClient.post).toHaveBeenCalledWith(
      '/ponches/fiorella/chat',
      {
        message: 'Consulta',
        active_module: '/dashboard',
        conversation_id: null,
      },
      undefined,
    )
  })
})
