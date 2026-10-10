import axios, {
  type AxiosRequestConfig,
  type AxiosResponse,
} from 'axios'

import apiClient from '../../../api/apiClient'

// ============================================================================
// CONSTANTS
// ============================================================================

const API_PREFIX = '/api/'

const PONCHES_PREFIX =
  '/api/ponches/'

const LEGACY_PREFIX =
  '/ponches/module/'

const DEFAULT_TIMEOUT_MS =
  200_000

const JSON_CONTENT_TYPE =
  'application/json'

// ============================================================================
// TYPES
// ============================================================================

type TitanApiErrorPayload = {
  detail?: unknown
  message?: string
  code?: string
}

type TitanRequestOptions = RequestInit & {
  /**
   * Permite ajustar timeout para operaciones lentas
   * como sincronización, BioTime o ZKTeco.
   */
  timeoutMs?: number

  /**
   * Indica explícitamente que la operación
   * no debe utilizar caché HTTP del navegador.
   */
  noCache?: boolean
}

// ============================================================================
// AXIOS -> FETCH RESPONSE ADAPTER
//
// Varias pantallas heredadas trabajan todavía con Response.
// Esta capa permite seguir usando:
// response.ok
// response.status
// response.json()
// response.text()
// response.blob()
//
// sin perder el apiClient central de TitanMDM.
// ============================================================================

function asResponse(
  result: AxiosResponse<ArrayBuffer>,
): Response {
  const headers =
    new Headers()

  const allowedHeaders = [
    'content-type',
    'content-disposition',
    'retry-after',
    'cache-control',
    'etag',
    'last-modified',
  ]

  for (
    const name
    of allowedHeaders
  ) {
    const value =
      result.headers[name]

    if (
      value != null
    ) {
      headers.set(
        name,
        String(
          value,
        ),
      )
    }
  }

  const empty =
    [
      204,
      205,
      304,
    ].includes(
      result.status,
    )

  return new Response(
    empty
      ? null
      : result.data,
    {
      status:
        result.status,

      statusText:
        result.statusText,

      headers,
    },
  )
}

// ============================================================================
// PATH VALIDATION
//
// Nunca se aceptan URLs arbitrarias desde las pantallas.
// Todas las llamadas deben permanecer dentro de /api/.
// ============================================================================

function validateApiPath(
  path: string,
): string {
  const cleaned =
    path.trim()

  if (
    !cleaned.startsWith(
      API_PREFIX,
    ) ||
    cleaned.startsWith(
      '/api//',
    )
  ) {
    throw new Error(
      'Ruta inválida de Ponches.',
    )
  }

  const relative =
    cleaned.slice(
      API_PREFIX.length,
    )

  const pathOnly =
    relative
      .split('?')[0]

  const segments =
    pathOnly
      .split('/')

  const invalidSegments =
    segments.some(
      segment =>
        segment === '..'
        ||
        segment === '.'
        ||
        segment.includes('\\'),
    )

  if (
    invalidSegments
  ) {
    throw new Error(
      'Ruta inválida de Ponches.',
    )
  }

  const parsed =
    new URL(
      relative,
      'https://titan.invalid/',
    )

  if (
    parsed.origin !==
    'https://titan.invalid'
  ) {
    throw new Error(
      'Ruta inválida de Ponches.',
    )
  }

  return relative
}

// ============================================================================
// BODY NORMALIZATION
// ============================================================================

function normalizeBody(
  body:
    | BodyInit
    | null
    | undefined,
): BodyInit | undefined {
  if (
    body == null
  ) {
    return undefined
  }

  return body
}

// ============================================================================
// CONTENT TYPE
// ============================================================================

function resolveContentType(
  headers: Headers,
  body:
    | BodyInit
    | null
    | undefined,
): string | null {
  const explicit =
    headers.get(
      'Content-Type',
    )

  if (
    explicit
  ) {
    return explicit
  }

  if (
    body == null
  ) {
    return null
  }

  if (
    typeof FormData !==
      'undefined'
    &&
    body instanceof FormData
  ) {
    /*
     * No establecer manualmente Content-Type
     * con FormData.
     *
     * El navegador/Axios genera automáticamente
     * el boundary multipart.
     */
    return null
  }

  if (
    typeof Blob !==
      'undefined'
    &&
    body instanceof Blob
  ) {
    return (
      body.type ||
      null
    )
  }

  if (
    typeof URLSearchParams !==
      'undefined'
    &&
    body instanceof URLSearchParams
  ) {
    return (
      'application/x-www-form-urlencoded;charset=UTF-8'
    )
  }

  if (
    typeof body ===
    'string'
  ) {
    return JSON_CONTENT_TYPE
  }

  return null
}

// ============================================================================
// CORE EXECUTOR
// ============================================================================

async function execute(
  url: string,
  init:
    TitanRequestOptions =
      {},
): Promise<Response> {
  const headers =
    new Headers(
      init.headers,
    )

  /*
   * La autenticación nunca se recibe
   * manualmente desde las pantallas.
   *
   * apiClient añade el JWT de la sesión
   * y ejecuta refresh automático.
   */
  headers.delete(
    'Authorization',
  )

  headers.delete(
    'authorization',
  )

  const body =
    normalizeBody(
      init.body,
    )

  const contentType =
    resolveContentType(
      headers,
      body,
    )

  if (
    contentType
  ) {
    headers.set(
      'Content-Type',
      contentType,
    )
  } else {
    headers.delete(
      'Content-Type',
    )
  }

  if (
    init.noCache
  ) {
    headers.set(
      'Cache-Control',
      'no-cache',
    )

    headers.set(
      'Pragma',
      'no-cache',
    )
  }

  const config:
    AxiosRequestConfig =
    {
      url,

      method:
        init.method ??
        'GET',

      data:
        body,

      headers:
        Object.fromEntries(
          headers.entries(),
        ),

      /*
       * Usamos ArrayBuffer porque algunas rutas
       * retornan:
       *
       * JSON
       * PDF
       * Excel
       * CSV
       * archivos binarios
       *
       * La conversión a Response ocurre arriba.
       */
      responseType:
        'arraybuffer',

      timeout:
        init.timeoutMs ??
        DEFAULT_TIMEOUT_MS,

      signal:
        init.signal ??
        undefined,

      /*
       * apiClient tiene baseURL="/api".
       *
       * Por eso execute recibe rutas relativas
       * a /api, por ejemplo:
       *
       * /ponches/dashboard
       * /ponches/records
       */
      validateStatus:
        () => true,
    }

  let result:
    AxiosResponse<ArrayBuffer>

  try {
    result =
      await apiClient
        .request<ArrayBuffer>(
          config,
        )
  } catch (
    error
  ) {
    if (
      axios.isCancel(
        error,
      )
    ) {
      throw error
    }

    if (
      axios.isAxiosError(
        error,
      )
      &&
      error.code ===
        'ECONNABORTED'
    ) {
      throw new Error(
        'La operación de Ponches superó el tiempo de espera.',
        {
          cause:
            error,
        },
      )
    }

    throw new Error(
      'No se pudo conectar con TitanMDM.',
      {
        cause:
          error,
      },
    )
  }

  const response =
    asResponse(
      result,
    )

  if (
    result.status <
      400
  ) {
    return response
  }

  /*
   * Normalizamos errores porque varias
   * pantallas heredadas esperan "detail"
   * mientras que TitanMDM utiliza
   * principalmente "message".
   */
  try {
    const text =
      await response
        .clone()
        .text()

    if (
      text
    ) {
      const data =
        JSON.parse(
          text,
        ) as
          TitanApiErrorPayload

      if (
        !data.detail
        &&
        data.message
      ) {
        return new Response(
          JSON.stringify(
            {
              ...data,

              detail:
                data.message,
            },
          ),
          {
            status:
              response.status,

            statusText:
              response.statusText,

            headers: {
              'Content-Type':
                JSON_CONTENT_TYPE,

              'Cache-Control':
                'no-store',
            },
          },
        )
      }
    }
  } catch {
    /*
     * No todos los errores son JSON.
     *
     * Las exportaciones o respuestas
     * binarias pueden devolver contenido
     * diferente.
     */
  }

  return response
}

// ============================================================================
// TITAN FETCH
//
// API definitiva.
// React -> TitanMDM API -> Python Edge.
// ============================================================================

export async function titanFetch(
  path: string,
  init:
    TitanRequestOptions =
      {},
): Promise<Response> {
  validateApiPath(
    path,
  )

  if (
    !path.startsWith(
      PONCHES_PREFIX,
    )
  ) {
    throw new Error(
      'titanFetch solamente admite endpoints de /api/ponches.',
    )
  }

  /*
   * apiClient tiene:
   *
   * baseURL: /api
   *
   * por tanto:
   *
   * /api/ponches/dashboard
   *
   * se transforma en:
   *
   * /ponches/dashboard
   */
  const axiosPath =
    path.replace(
      /^\/api/,
      '',
    )

  return execute(
    axiosPath,
    init,
  )
}

// ============================================================================
// AUTH FETCH
//
// Compatibilidad temporal con pantallas heredadas.
//
// React
//   ↓
// /api/ponches/legacy/*
//   ↓
// TitanMDM API
//   ↓
// Python Edge
//
// Estas llamadas se irán eliminando cuando
// cada submódulo tenga endpoint TitanMDM definitivo.
// ============================================================================

export async function authFetch(
  path: string,
  init:
    TitanRequestOptions =
      {},
): Promise<Response> {
  const relative =
    validateApiPath(
      path,
    )

  const target =
    LEGACY_PREFIX +
    relative

  return execute(
    target,
    init,
  )
}

// ============================================================================
// JSON HELPERS
//
// Evitan repetir:
//
// response.ok
// response.json()
// parse de errores
//
// en cada pantalla.
// ============================================================================

export async function readJson<T>(
  response: Response,
): Promise<T> {
  const text =
    await response.text()

  if (
    !text
  ) {
    if (
      response.ok
    ) {
      return undefined as T
    }

    throw new Error(
      `TitanMDM respondió HTTP ${response.status}.`,
    )
  }

  let payload:
    unknown

  try {
    payload =
      JSON.parse(
        text,
      )
  } catch {
    if (
      response.ok
    ) {
      throw new Error(
        'TitanMDM devolvió una respuesta JSON inválida.',
      )
    }

    throw new Error(
      `TitanMDM respondió HTTP ${response.status}.`,
    )
  }

  if (
    response.ok
  ) {
    return payload as T
  }

  const error =
    payload as
      TitanApiErrorPayload

  const message =
    typeof error.detail ===
      'string'
      ? error.detail
      : error.message
        ??
        `TitanMDM respondió HTTP ${response.status}.`

  throw new Error(
    message,
  )
}

// ============================================================================
// TITAN JSON
// ============================================================================

export async function titanJson<T>(
  path: string,
  init:
    TitanRequestOptions =
      {},
): Promise<T> {
  const response =
    await titanFetch(
      path,
      init,
    )

  return readJson<T>(
    response,
  )
}

// ============================================================================
// LEGACY JSON
// ============================================================================

export async function authJson<T>(
  path: string,
  init:
    TitanRequestOptions =
      {},
): Promise<T> {
  const response =
    await authFetch(
      path,
      init,
    )

  return readJson<T>(
    response,
  )
}

// ============================================================================
// JSON BODY BUILDER
// ============================================================================

export function jsonRequest(
  body: unknown,
  init:
    TitanRequestOptions =
      {},
): TitanRequestOptions {
  const headers =
    new Headers(
      init.headers,
    )

  headers.set(
    'Content-Type',
    JSON_CONTENT_TYPE,
  )

  return {
    ...init,

    headers,

    body:
      JSON.stringify(
        body,
      ),
  }
}

// ============================================================================
// NO CACHE REQUEST
//
// Útil para:
// - health
// - dashboards manual refresh
// - estado de relojes
// - operaciones posteriores a escritura
// ============================================================================

export function noCacheRequest(
  init:
    TitanRequestOptions =
      {},
): TitanRequestOptions {
  return {
    ...init,

    noCache:
      true,
  }
}

// ============================================================================
// DOWNLOAD HELPERS
// ============================================================================

export async function downloadResponse(
  response: Response,
  fallbackFileName:
    string,
): Promise<void> {
  if (
    !response.ok
  ) {
    await readJson<unknown>(
      response,
    )

    return
  }

  const blob =
    await response.blob()

  const disposition =
    response.headers.get(
      'content-disposition',
    )

  let fileName =
    fallbackFileName

  if (
    disposition
  ) {
    const utf8 =
      disposition.match(
        /filename\*=UTF-8''([^;]+)/i,
      )

    const simple =
      disposition.match(
        /filename="?([^";]+)"?/i,
      )

    if (
      utf8?.[1]
    ) {
      try {
        fileName =
          decodeURIComponent(
            utf8[1],
          )
      } catch {
        fileName =
          utf8[1]
      }
    } else if (
      simple?.[1]
    ) {
      fileName =
        simple[1]
    }
  }

  const objectUrl =
    URL.createObjectURL(
      blob,
    )

  try {
    const anchor =
      document.createElement(
        'a',
      )

    anchor.href =
      objectUrl

    anchor.download =
      fileName

    anchor.style.display =
      'none'

    document.body.appendChild(
      anchor,
    )

    anchor.click()

    anchor.remove()
  } finally {
    window.setTimeout(
      () => {
        URL.revokeObjectURL(
          objectUrl,
        )
      },
      1_000,
    )
  }
}