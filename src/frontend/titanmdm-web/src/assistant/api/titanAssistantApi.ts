import { isAxiosError } from 'axios'

import apiClient from '../../api/apiClient'

export type TitanToolKind =
  | 'read'
  | 'action'
  | 'sensitive'

export interface TitanToolDescriptor {
  name: string
  title: string
  description: string
  kind: number
  requiredPermissions: string[]
}

export interface TitanCapabilities {
  enabled: boolean
  available: boolean
  provider: string
  model: string
  tools: TitanToolDescriptor[]
}

export interface TitanProviderStatus {
  name: string
  enabled: boolean
  available: boolean
  model: string
  detail?: string | null
  checkedAtUtc?: string | null
  latencyMs?: number | null
}

export interface TitanProviderConfiguration {
  preferredProvider: string
  requestTimeoutSeconds: number
  maxTokens: number
  maxTurnsPerWindow: number
  quotaWindowSeconds: number
  providers: TitanProviderStatus[]
}

export interface TitanToolInvocation {
  tool: string
  kind: string
  startedAtUtc: string
  durationMs: number
  success: boolean
  errorCode?: string | null
}

export interface TitanTurnResponse {
  conversationId: string
  answer: string
  provider: string
  model: string
  toolInvocations: TitanToolInvocation[]
  availableTools: string[]
  cancelled: boolean
}

export interface TitanConversationSummary {
  id: string
  title: string
  createdAtUtc: string
  updatedAtUtc: string
  messageCount: number
}

export interface TitanConversationEntry {
  id: string
  role: string
  content: string
  createdAtUtc: string
  toolInvocations: TitanToolInvocation[]
}

export interface TitanConversation {
  id: string
  title: string
  createdAtUtc: string
  updatedAtUtc: string
  entries: TitanConversationEntry[]
}

export interface TitanTurnRequest {
  conversationId?: string | null
  message: string
  module?: string | null
}

export function toolKindOf(
  kind: number | string | undefined,
): TitanToolKind {
  if (kind === 1 || kind === 'Action') {
    return 'action'
  }

  if (kind === 2 || kind === 'Sensitive') {
    return 'sensitive'
  }

  return 'read'
}

/**
 * Cliente tipado de la superficie real del asistente
 * empresarial (`/api/titan-assistant`). El backend es la
 * única autoridad sobre permisos, alcance, herramientas y
 * proveedor activo; el frontend nunca decide esas reglas.
 */
export const titanAssistantApi = {
  async getProviderConfiguration(
    signal?: AbortSignal,
  ): Promise<TitanProviderConfiguration> {
    const { data } =
      await apiClient.get<TitanProviderConfiguration>(
        '/titan-assistant/providers',
        signal ? { signal } : undefined,
      )

    return {
      ...data,
      providers: data.providers ?? [],
    }
  },

  async getCapabilities(): Promise<TitanCapabilities> {
    const { data } =
      await apiClient.get<TitanCapabilities>(
        '/titan-assistant/capabilities',
      )

    return {
      ...data,
      provider: data.provider ?? 'none',
      model: data.model ?? '',
      tools: data.tools ?? [],
    }
  },

  async sendMessage(
    request: TitanTurnRequest,
    signal?: AbortSignal,
  ): Promise<TitanTurnResponse> {
    const { data } =
      await apiClient.post<TitanTurnResponse>(
        '/titan-assistant/messages',
        request,
        signal ? { signal } : undefined,
      )

    return {
      ...data,
      toolInvocations:
        data.toolInvocations ?? [],
      availableTools:
        data.availableTools ?? [],
      cancelled: Boolean(data.cancelled),
    }
  },

  async listConversations(): Promise<
    TitanConversationSummary[]
  > {
    const { data } = await apiClient.get<{
      conversations: TitanConversationSummary[]
    }>('/titan-assistant/conversations')

    return data.conversations ?? []
  },

  async getConversation(
    conversationId: string,
  ): Promise<TitanConversation> {
    const { data } =
      await apiClient.get<TitanConversation>(
        `/titan-assistant/conversations/${conversationId}`,
      )

    return {
      ...data,
      entries: data.entries ?? [],
    }
  },

  async deleteConversation(
    conversationId: string,
  ): Promise<void> {
    await apiClient.delete(
      `/titan-assistant/conversations/${conversationId}`,
    )
  },
}

export type TitanFailureKind =
  | 'cancelled'
  | 'forbidden'
  | 'unauthorized'
  | 'unavailable'
  | 'timeout'
  | 'network'
  | 'validation'
  | 'unknown'

export interface TitanFailure {
  kind: TitanFailureKind
  message: string
}

function serverMessage(
  error: unknown,
): string | null {
  if (!isAxiosError(error)) {
    return null
  }

  const data = error.response?.data as
    | { message?: string; detail?: string }
    | undefined

  return (
    data?.message ??
    data?.detail ??
    null
  )
}

/**
 * Traduce fallos de transporte/permiso a un estado que la
 * interfaz pueda mostrar sin inventar información. Nunca
 * expone la respuesta cruda del proveedor.
 */
export function describeTitanFailure(
  error: unknown,
): TitanFailure {
  if (!isAxiosError(error)) {
    return {
      kind: 'unknown',
      message:
        'No se pudo completar la solicitud con Titan.',
    }
  }

  if (error.code === 'ERR_CANCELED') {
    return {
      kind: 'cancelled',
      message: 'Consulta cancelada.',
    }
  }

  const status = error.response?.status
  const detail = serverMessage(error)

  switch (status) {
    case 401:
      return {
        kind: 'unauthorized',
        message:
          'Tu sesión expiró. Inicia sesión nuevamente.',
      }

    case 403:
      return {
        kind: 'forbidden',
        message:
          detail ??
          'Tu cuenta no tiene habilitado el asistente o le falta el permiso requerido.',
      }

    case 400:
      return {
        kind: 'validation',
        message:
          detail ??
          'La consulta no es válida para el asistente.',
      }

    case 503:
      return {
        kind: 'unavailable',
        message:
          detail ??
          'El asistente no está disponible en este momento.',
      }

    default:
      break
  }

  if (
    error.code === 'ECONNABORTED' ||
    error.code === 'ETIMEDOUT'
  ) {
    return {
      kind: 'timeout',
      message:
        'El asistente tardó demasiado en responder. Inténtalo de nuevo.',
    }
  }

  if (!error.response) {
    return {
      kind: 'network',
      message:
        'No hay conexión con el servicio del asistente.',
    }
  }

  return {
    kind: 'unknown',
    message:
      detail ??
      'El asistente no pudo completar la respuesta.',
  }
}
