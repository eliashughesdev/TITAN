import { createContext, useContext } from 'react'

export type CloseModal = (afterClose?: () => void) => void

export const ModalCloseContext = createContext<CloseModal | null>(null)

/** Cierra el IframeModal contenedor con animacion (contenido directo, sin iframe). */
export function useItamModalClose(): CloseModal {
  const close = useContext(ModalCloseContext)
  if (!close) throw new Error('useItamModalClose debe usarse dentro de IframeModal.')
  return close
}
