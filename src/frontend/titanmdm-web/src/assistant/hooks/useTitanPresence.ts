import { useEffect, useState } from 'react'

/** Zonas propias de Titan: su foco no cuenta como "usuario trabajando". */
const TITAN_ZONES = '.titan-dock, .titan-agent-chat'

const NON_TEXT_INPUTS = new Set([
  'button', 'checkbox', 'color', 'file', 'image', 'radio', 'range', 'reset', 'submit',
])

/** ¿El elemento es un campo donde el usuario escribe o elige valores? */
export function isFormField(element: Element | null): boolean {
  if (!element || element.closest(TITAN_ZONES)) return false

  if (element instanceof HTMLInputElement) {
    return !NON_TEXT_INPUTS.has(element.type)
  }

  return (
    element instanceof HTMLTextAreaElement ||
    element instanceof HTMLSelectElement ||
    (element instanceof HTMLElement && element.isContentEditable)
  )
}

/** ¿Hay un diálogo o menú de la aplicación abierto (fuera de Titan)? */
export function hasBlockingOverlay(root: ParentNode = document): boolean {
  return Array.from(
    root.querySelectorAll(
      '[role="dialog"], [aria-modal="true"], [role="menu"], [data-titan-blocking="true"]',
    ),
  ).some(element => !element.closest(TITAN_ZONES))
}

export interface TitanPresence {
  /** El usuario escribe en un formulario: Titan se vuelve translúcido. */
  typing: boolean
  /** Hay un diálogo abierto: Titan se aparta por completo. */
  overlay: boolean
}

/**
 * Observa el trabajo del usuario para que Titan nunca estorbe:
 * se atenúa mientras se escribe y desaparece bajo los diálogos.
 */
export function useTitanPresence(): TitanPresence {
  const [typing, setTyping] = useState(() => isFormField(document.activeElement))
  const [overlay, setOverlay] = useState(() => hasBlockingOverlay())

  useEffect(() => {
    let release = 0

    const onFocusIn = (event: FocusEvent) => {
      window.clearTimeout(release)
      setTyping(isFormField(event.target as Element | null))
    }

    // Pequeña espera: pasar de un campo a otro no hace parpadear a Titan.
    const onFocusOut = () => {
      window.clearTimeout(release)
      release = window.setTimeout(() => {
        setTyping(isFormField(document.activeElement))
      }, 450)
    }

    document.addEventListener('focusin', onFocusIn)
    document.addEventListener('focusout', onFocusOut)

    return () => {
      window.clearTimeout(release)
      document.removeEventListener('focusin', onFocusIn)
      document.removeEventListener('focusout', onFocusOut)
    }
  }, [])

  useEffect(() => {
    let frame = 0
    const check = () => {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(() => setOverlay(hasBlockingOverlay()))
    }

    const observer = new MutationObserver(check)
    observer.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['role', 'aria-modal', 'data-titan-blocking'],
    })

    return () => {
      cancelAnimationFrame(frame)
      observer.disconnect()
    }
  }, [])

  return { typing, overlay }
}
