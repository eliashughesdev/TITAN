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

async function getBlob(
  ticketId: string,
  attachmentId: string,
  mode:
    'preview' |
    'download',
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