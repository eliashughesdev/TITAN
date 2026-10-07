import type {
  TitanPageContext,
  TitanUserContext,
} from '../context/TitanAssistantContext'

import {
  getAllowedTitanCapabilities,
  type TitanCapability,
} from '../config/titanCapabilities'

export interface TitanConversationContext {
  identity: {
    userId: string
    organizationId: string
    displayName: string
    roles: string[]
  }

  page: TitanPageContext

  capabilities: TitanCapability[]
}

export function buildTitanConversationContext(
  user: TitanUserContext | null,
  page: TitanPageContext,
): TitanConversationContext | null {
  if (!user) {
    return null
  }

  return {
    identity: {
      userId: user.id,
      organizationId:
        user.organizationId,
      displayName:
        user.fullName ||
        user.email,
      roles: user.roles,
    },

    page,

    capabilities:
      getAllowedTitanCapabilities(
        user.permissions,
      ),
  }
}