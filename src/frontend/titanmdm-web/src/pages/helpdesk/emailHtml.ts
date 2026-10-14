import DOMPurify from 'dompurify'

/*
 * ============================================================
 * TITANMDM HELPDESK — CONTENIDO DE CORREO SEGURO
 * ============================================================
 *
 * El cuerpo de los correos entrantes puede contener HTML de
 * firma con imágenes inline (cid:). Antes de renderizar:
 *
 *  - se sanea con DOMPurify (script/handlers/urls peligrosas),
 *  - se resuelven las referencias cid: hacia los adjuntos,
 *  - si no hay HTML se muestra el texto plano tal cual.
 *
 * Nunca se inyecta HTML sin sanear (dangerouslySetInnerHTML solo
 * recibe el resultado de estas funciones).
 */

const ALLOWED_TAGS = [
  'a', 'abbr', 'b', 'blockquote', 'br', 'code', 'div', 'em',
  'font', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'hr', 'i', 'img',
  'li', 'ol', 'p', 'pre', 'span', 'strong', 'sub', 'sup',
  'table', 'tbody', 'td', 'tfoot', 'th', 'thead', 'tr', 'u', 'ul',
]

const ALLOWED_ATTR = [
  'alt', 'class', 'colspan', 'height', 'href', 'rel', 'rowspan',
  'src', 'style', 'target', 'title', 'width',
]

/*
 * Permite cid: (imágenes inline) además de los esquemas seguros
 * habituales. Cualquier otro esquema (javascript:, data:, ...)
 * queda bloqueado por DOMPurify.
 */
const ALLOWED_URI_REGEXP =
  /^(?:(?:https?|mailto|tel|cid):|[^a-z]|[a-z+.-]+(?:[^a-z+.\-:]|$))/i

const CID_REFERENCE_PATTERN =
  /cid:([^\s"'<>()]+)/gi

const HTML_MARKUP_PATTERN =
  /<\/?[a-z][^>]*>/i

export function hasHtmlMarkup(
  value: string,
): boolean {
  return HTML_MARKUP_PATTERN.test(value)
}

function withSafeAnchorAttributes(
  html: string,
): string {
  if (
    typeof DOMParser ===
    'undefined'
  ) {
    return html
  }

  const document =
    new DOMParser()
      .parseFromString(
        html,
        'text/html',
      )

  document
    .querySelectorAll('a')
    .forEach((anchor) => {
      if (
        anchor.getAttribute('href')
      ) {
        anchor.setAttribute(
          'target',
          '_blank',
        )

        anchor.setAttribute(
          'rel',
          'noopener noreferrer',
        )
      }
    })

  return document.body.innerHTML
}

export function sanitizeEmailHtml(
  html: string,
): string {
  const clean =
    DOMPurify.sanitize(
      html,
      {
        ALLOWED_TAGS,
        ALLOWED_ATTR,
        ALLOW_DATA_ATTR: false,
        ALLOWED_URI_REGEXP,
        ADD_ATTR: [
          'target',
        ],
      },
    )

  return withSafeAnchorAttributes(
    clean,
  )
}

export function stripUnresolvedCidImages(
  html: string,
): string {
  if (
    typeof DOMParser ===
    'undefined'
  ) {
    return html.replace(
      /<img\b[^>]*src=["']cid:[^"']*["'][^>]*>/gi,
      '',
    )
  }

  const document =
    new DOMParser()
      .parseFromString(
        html,
        'text/html',
      )

  document
    .querySelectorAll('img')
    .forEach((image) => {
      const source =
        image.getAttribute('src') ??
        ''

      if (
        source
          .toLowerCase()
          .startsWith(
            'cid:',
          )
      ) {
        image.remove()
      }
    })

  return document.body.innerHTML
}

export function resolveCidReferences(
  html: string,
  resolve: (
    contentId: string,
  ) => string | undefined,
): string {
  return html.replace(
    CID_REFERENCE_PATTERN,
    (match, rawContentId: string) => {
      const contentId =
        decodeURIComponent(
          rawContentId,
        )
          .replace(
            /^<|>$/g,
            '',
          )
          .trim()
          .toLowerCase()

      const resolved =
        resolve(
          contentId,
        )

      return resolved ?? match
    },
  )
}
