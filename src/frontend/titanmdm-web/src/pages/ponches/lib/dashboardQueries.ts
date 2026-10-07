import {
  queryOptions,
} from '@tanstack/react-query'

import {
  loadDashboardSnapshot,
} from './dashboardApi'

export const ponchesDashboardKey =
  [
    'ponches',
    'dashboard',
    'snapshot',
  ] as const

export function ponchesDashboardQueryOptions() {
  return queryOptions({
    queryKey:
      ponchesDashboardKey,

    queryFn:
      ({
        signal,
      }) =>
        loadDashboardSnapshot(
          signal,
        ),

    /*
     * Al volver al dashboard durante 45 segundos,
     * React Query entrega inmediatamente el dato
     * almacenado sin bloquear la pantalla.
     */
    staleTime:
      45_000,

    /*
     * El dashboard puede permanecer fuera de pantalla
     * durante diez minutos sin perder el snapshot.
     */
    gcTime:
      10 * 60_000,

    retry:
      1,

    refetchOnWindowFocus:
      false,

    refetchOnMount:
      false,

    refetchOnReconnect:
      true,
  })
}