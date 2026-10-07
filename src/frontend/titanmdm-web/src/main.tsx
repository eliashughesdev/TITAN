import {
  StrictMode,
} from 'react'

import {
  createRoot,
} from 'react-dom/client'

import {
  BrowserRouter,
} from 'react-router-dom'

import {
  QueryClient,
  QueryClientProvider,
} from '@tanstack/react-query'

import App from './App'

import {
  AuthProvider,
} from './auth/AuthContext'

import './index.css'

import './styles/titan-modules-a.css'
import './styles/titan-modules-b.css'
import './styles/titan-modules-c.css'
import './styles/titan-modules-d.css'

/*
 * ============================================================
 * TITANMDM QUERY CACHE
 * ============================================================
 *
 * El cache global evita volver a consultar endpoints costosos
 * cada vez que el usuario cambia de submódulo.
 *
 * Para Ponches esto es especialmente importante porque:
 *
 * Browser
 *   -> TitanMDM API
 *   -> Gateway Python
 *   -> BioTime / relojes biométricos
 *
 * staleTime:
 *   durante este tiempo los datos se consideran frescos.
 *
 * gcTime:
 *   mantiene el resultado en memoria aunque el componente
 *   haya sido desmontado.
 */

const queryClient =
  new QueryClient({
    defaultOptions: {
      queries: {
        staleTime:
          30_000,

        gcTime:
          10 * 60_000,

        retry:
          1,

        refetchOnWindowFocus:
          false,

        refetchOnReconnect:
          true,

        refetchOnMount:
          false,
      },

      mutations: {
        retry:
          0,
      },
    },
  })

createRoot(
  document.getElementById(
    'root',
  )!,
).render(
  <StrictMode>
    <QueryClientProvider
      client={
        queryClient
      }
    >
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)