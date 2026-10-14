import { useMemo } from 'react'

import {
  hasHtmlMarkup,
  resolveCidReferences,
  sanitizeEmailHtml,
  stripUnresolvedCidImages,
} from './emailHtml'

interface EmailContentProps {
  text: string

  html?: string | null

  cidUrls?: Record<string, string>

  className?: string
}

/*
 * Renderiza el contenido de un correo de helpdesk de forma segura:
 * HTML saneado con resolución de imágenes inline (cid:) cuando
 * existe, o texto plano preservando saltos de línea en su defecto.
 */
export function EmailContent({
  text,
  html,
  cidUrls,
  className,
}: EmailContentProps) {
  const rendered =
    useMemo(() => {
      if (
        !html ||
        !hasHtmlMarkup(html)
      ) {
        return null
      }

      const clean =
        sanitizeEmailHtml(
          html,
        )

      const resolved =
        cidUrls
          ? resolveCidReferences(
              clean,
              (contentId) =>
                cidUrls[contentId],
            )
          : clean

      /*
       * Evita iconos de imagen rota para cid: que no pudieron
       * resolverse (p.ej. sin mapa de adjuntos).
       */
      return stripUnresolvedCidImages(
        resolved,
      )
    }, [
      html,
      cidUrls,
    ])

  if (!rendered) {
    return (
      <div
        className={className}
        style={{
          whiteSpace: 'pre-wrap',
        }}
      >
        {text}
      </div>
    )
  }

  return (
    <div
      className={className}
      /*
       * `rendered` es SIEMPRE el resultado de sanitizeEmailHtml
       * (DOMPurify) y resolveCidReferences; nunca HTML crudo.
       */
      dangerouslySetInnerHTML={{
        __html: rendered,
      }}
    />
  )
}
