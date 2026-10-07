import {
  useEffect,
  useRef,
  useState,
} from 'react'

import {
  useSearchParams,
} from 'react-router-dom'

import {
  ShieldCheck,
} from 'lucide-react'

import {
  tokenStorage,
} from '../auth/tokenStorage'

import type {
  LoginResponse,
} from '../types/auth'

export function EntraLoginCallbackPage() {
  const started =
    useRef(
      false,
    )

  const [
    searchParams,
  ] =
    useSearchParams()

  const [
    error,
    setError,
  ] =
    useState(
      '',
    )

  const microsoftError =
    searchParams.get(
      'error',
    )

  useEffect(
    () => {
      if (started.current) {
        return
      }

      started.current =
        true

      if (microsoftError) {
        setError(
          microsoftError,
        )

        return
      }

      async function finishLogin() {
        try {
          const response =
            await fetch(
              '/api/auth/entra/exchange',
              {
                method:
                  'POST',

                credentials:
                  'same-origin',

                headers: {
                  Accept:
                    'application/json',
                },
              },
            )

          if (!response.ok) {
            const payload =
              await response
                .json()
                .catch(
                  () =>
                    null,
                ) as
                | {
                    message?: string
                  }
                | null

            throw new Error(
              payload?.message
              ??
              'No se pudo completar el ingreso con Microsoft.',
            )
          }

          const session =
            await response
              .json() as
              LoginResponse

          tokenStorage.setTokens(
            session.accessToken,
            session.refreshToken,
          )

          window.location
            .replace(
              '/',
            )
        }
        catch (
          cause
        ) {
          setError(
            cause instanceof Error
              ? cause.message
              : 'No se pudo completar el ingreso.',
          )
        }
      }

      void finishLogin()
    },
    [
      microsoftError,
    ],
  )

  return (
    <main
      className="titan-page"
      style={{
        maxWidth:
          560,

        margin:
          '80px auto',

        padding:
          28,

        border:
          '1px solid #dfe6f3',

        borderRadius:
          18,

        background:
          '#fff',
      }}
    >
      <div
        style={{
          display:
            'flex',

          alignItems:
            'center',

          gap:
            10,
        }}
      >
        <ShieldCheck
          size={24}
        />

        <h1>
          Ingreso con Microsoft
        </h1>
      </div>

      {error ? (
        <>
          <p
            role="alert"
            style={{
              color:
                '#b42318',

              lineHeight:
                1.6,
            }}
          >
            {error}
          </p>

          <a
            href="/login"
          >
            Volver al inicio de sesión
          </a>
        </>
      ) : (
        <p>
          Comprobando tu identidad
          corporativa y tus permisos
          en TitanMDM…
        </p>
      )}
    </main>
  )
}