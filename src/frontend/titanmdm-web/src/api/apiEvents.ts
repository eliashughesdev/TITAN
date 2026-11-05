/* Eventos globales emitidos por apiClient (sin dependencias para evitar ciclos). */
export const API_ERROR_EVENT = 'titanmdm:api-error'

export interface ApiErrorEventDetail {
  status?: number
  method?: string
  url?: string
  message: string
}
