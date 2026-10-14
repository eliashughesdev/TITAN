import {
  useEffect,
  useState,
} from 'react'

import {
  helpdeskAttachmentsApi,
} from '../../api/helpdeskAttachmentsApi'

/*
 * Construye un mapa contentId -> objectURL para las imágenes
 * inline (firmas) del correo, de forma que el HTML saneado pueda
 * resolver sus referencias cid: sin exponer el contenido crudo.
 */
export function useInlineAttachmentUrls(
  ticketId?: string | null,
): Record<string, string> {
  const [
    urls,
    setUrls,
  ] =
    useState<
      Record<string, string>
    >({})

  useEffect(() => {
    if (!ticketId) {
      setUrls(
        {},
      )

      return
    }

    let cancelled =
      false

    const created: string[] =
      []

    const load =
      async () => {
        try {
          const attachments =
            await helpdeskAttachmentsApi
              .list(
                ticketId,
              )

          const inline =
            attachments.filter(
              (item) =>
                item.isInline &&
                !!item.contentId &&
                item.contentType
                  .toLowerCase()
                  .startsWith(
                    'image/',
                  ),
            )

          const next: Record<
            string,
            string
          > = {}

          for (
            const item
            of inline
          ) {
            const blob =
              await helpdeskAttachmentsApi
                .download(
                  ticketId,
                  item.id,
                )

            if (cancelled) {
              return
            }

            const url =
              URL.createObjectURL(
                blob,
              )

            created.push(
              url,
            )

            next[
              item.contentId!
                .trim()
                .toLowerCase()
            ] = url
          }

          if (!cancelled) {
            setUrls(
              next,
            )
          }
        } catch {
          if (!cancelled) {
            setUrls(
              {},
            )
          }
        }
      }

    void load()

    return () => {
      cancelled =
        true

      created.forEach(
        (url) => {
          URL.revokeObjectURL(
            url,
          )
        },
      )
    }
  }, [
    ticketId,
  ])

  return urls
}
