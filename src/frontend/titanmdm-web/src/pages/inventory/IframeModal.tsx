import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'

import { ModalCloseContext } from './itamModalContext'

type FormSavedMessage = {
  type: 'FORM_SAVED'
  record?: unknown
}

type CloseMessage = {
  type: 'CLOSE_IFRAME'
}

/* Debe coincidir con la duracion de itam-fade-out / itam-sink. */
const CLOSE_ANIMATION_MS = 180

export function IframeModal({
  title,
  subtitle,
  src,
  onClose,
  onSaved,
  children,
  size = 'large',
}: {
  title: string
  subtitle?: string
  /** Formulario embebido (iframe). Si se omite se renderiza `children`. */
  src?: string
  onClose: () => void
  onSaved?: (record?: unknown) => void
  /** Contenido directo; usa useItamModalClose() para cerrar con animacion. */
  children?: ReactNode
  size?: 'large' | 'compact'
}) {
  const frameRef = useRef<HTMLIFrameElement>(null)
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const closingRef = useRef(false)
  const [closing, setClosing] = useState(false)
  const [loaded, setLoaded] = useState(!src)

  // Cierre animado: se desmonta al terminar la animacion de salida.
  const requestClose = useCallback(
    (afterClose?: () => void) => {
      if (closingRef.current) return
      closingRef.current = true
      setClosing(true)

      const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
      window.setTimeout(
        () => {
          afterClose?.()
          onClose()
        },
        reduced ? 0 : CLOSE_ANIMATION_MS,
      )
    },
    [onClose],
  )

  useEffect(() => {
    const previousFocus = document.activeElement as HTMLElement | null

    const onMessage = (event: MessageEvent) => {
      if (event.origin !== window.location.origin) return
      // Solo se aceptan mensajes del formulario de este modal.
      if (frameRef.current && event.source !== frameRef.current.contentWindow) return

      const data = event.data as FormSavedMessage | CloseMessage | null
      if (!data || typeof data !== 'object') return

      if (data.type === 'CLOSE_IFRAME') {
        requestClose()
        return
      }

      if (data.type === 'FORM_SAVED') {
        requestClose(() => onSaved?.(data.record))
      }
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') requestClose()
    }

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    window.addEventListener('message', onMessage)
    window.addEventListener('keydown', onKeyDown)
    closeButtonRef.current?.focus()

    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('message', onMessage)
      window.removeEventListener('keydown', onKeyDown)
      previousFocus?.focus?.()
    }
  }, [onSaved, requestClose])

  return (
    <div
      className={closing ? 'itam-modal itam-modal--closing' : 'itam-modal'}
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onMouseDown={event => {
        if (event.target === event.currentTarget) requestClose()
      }}
    >
      <div className={size === 'compact' ? 'itam-modal__dialog itam-modal__dialog--compact' : 'itam-modal__dialog'}>
        <div className="itam-modal__head">
          <div className="itam-modal__title">
            <span className="itam-modal__icon" aria-hidden="true">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z" />
                <path d="m3.3 7 8.7 5 8.7-5" />
                <path d="M12 22V12" />
              </svg>
            </span>
            <div>
              <strong>{title}</strong>
              {subtitle && <small>{subtitle}</small>}
            </div>
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            className="itam-modal__close"
            onClick={() => requestClose()}
            aria-label="Cerrar formulario"
          >
            ×
          </button>
        </div>
        <div className="itam-modal__body">
          {!loaded && (
            <div className="itam-modal__loading" aria-live="polite">
              <span className="itam-modal__spinner" aria-hidden="true" />
              Cargando formulario…
            </div>
          )}
          {src ? (
            <iframe
              ref={frameRef}
              className="itam-modal__frame"
              src={src}
              title={title}
              onLoad={() => setLoaded(true)}
            />
          ) : (
            <div className="itam-modal__content">
              <ModalCloseContext.Provider value={requestClose}>{children}</ModalCloseContext.Provider>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
