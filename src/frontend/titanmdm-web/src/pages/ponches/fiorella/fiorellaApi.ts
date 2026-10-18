import apiClient from '../../../api/apiClient'

export interface FiorellaChatResponse {
  answer: string
  animation: string
  action?: unknown
  conversationId: string | null
  toolsUsed: string[]
  provider: string
  model: string
}

interface FiorellaGatewayResponse {
  respuesta?: string
  animacion?: string
  action?: unknown
  conversation_id?: number | null
  tools_used?: string[]
  provider?: string
  model_used?: string
  requested_model?: string
}

export const fiorellaApi = {
  async sendMessage(
    request: {
      message: string
      activeModule: string
      conversationId: string | null
    },
    signal?: AbortSignal,
  ): Promise<FiorellaChatResponse> {
    const parsedConversationId = request.conversationId
      ? Number(request.conversationId)
      : null

    const { data } =
      await apiClient.post<FiorellaGatewayResponse>(
        '/ponches/fiorella/chat',
        {
          message: request.message,
          active_module: request.activeModule,
          conversation_id:
            Number.isSafeInteger(parsedConversationId)
              ? parsedConversationId
              : null,
        },
        signal ? { signal } : undefined,
      )

    return {
      answer:
        typeof data.respuesta === 'string'
          ? data.respuesta
          : '',
      animation:
        typeof data.animacion === 'string'
          ? data.animacion
          : 'point',
      action: data.action,
      conversationId:
        data.conversation_id == null
          ? null
          : String(data.conversation_id),
      toolsUsed: Array.isArray(data.tools_used)
        ? data.tools_used
        : [],
      provider: data.provider ?? 'ponches',
      model:
        data.model_used ??
        data.requested_model ??
        '',
    }
  },
}
