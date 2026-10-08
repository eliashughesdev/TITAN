import axios from 'axios'

import apiClient
  from './apiClient'

export interface HelpdeskAttachment {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  isInternal: boolean
  isInline: boolean
  contentId?: string | null
  createdAtUtc: string
  canPreview: boolean
}

export function getAttachmentErrorMessage(
  error: unknown,
) {
  if (
    axios.isAxiosError(
      error,
    )
  ) {
    const status =
      error.response?.status

    if (
      status === 401
    ) {
      return 'La sesión expiró o no está autenticada.'
    }

    if (
      status === 403
    ) {
      return 'No tienes permisos para visualizar los adjuntos de este ticket.'
    }

    if (
      status === 404
    ) {
      return 'Uno de los archivos ya no está disponible en almacenamiento.'
    }

    const responseMessage =
      (
        error.response?.data as
          | {
              message?: string
            }
          | undefined
      )
        ?.message

    if (
      responseMessage
    ) {
      return responseMessage
    }
  }

  return 'No se pudieron cargar los archivos del ticket.'
}

async function getBlob(
  ticketId: string,
  attachmentId: string,
  mode:
    | 'preview'
    | 'download',
) {
  const response =
    await apiClient.get<Blob>(
      `/helpdesk/tickets/${ticketId}/attachments/${attachmentId}/${mode}`,
      {
        responseType:
          'blob',
      },
    )

  return response.data
}

export const helpdeskAttachmentsApi = {
  async list(
    ticketId: string,
  ) {
    const response =
      await apiClient.get<
        HelpdeskAttachment[]
      >(
        `/helpdesk/tickets/${ticketId}/attachments`,
      )

    return response.data
  },

  async preview(
    ticketId: string,
    attachmentId: string,
  ) {
    return getBlob(
      ticketId,
      attachmentId,
      'preview',
    )
  },

  async download(
    ticketId: string,
    attachmentId: string,
  ) {
    return getBlob(
      ticketId,
      attachmentId,
      'download',
    )
  },
}