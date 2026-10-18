const GOOGLE_MAPS_SCRIPT_ID = 'titanmdm-google-maps-script'

const GOOGLE_MAPS_API_KEY =
  import.meta.env.VITE_GOOGLE_MAPS_API_KEY as
    | string
    | undefined

let loadPromise: Promise<boolean> | null = null

export function getGoogleMapsApiKey():
  string | null {
  return GOOGLE_MAPS_API_KEY?.trim() || null
}

export function isGoogleMapsConfigured(): boolean {
  return getGoogleMapsApiKey() !== null
}

type WindowWithGoogle =
  Window & {
    google?: {
      maps?: unknown
    }
  }

/**
 * Carga la libreria de Google Maps de forma
 * perezosa y con un unico script.
 *
 * - Sin clave configurada resuelve null (el
 *   consumidor muestra "configuracion pendiente").
 * - Si el script ya existe reutiliza la promesa
 *   en curso o resuelve inmediatamente.
 */
export function loadGoogleMaps(): Promise<boolean> {
  if (!isGoogleMapsConfigured()) {
    return Promise.resolve(false)
  }

  if (
    typeof window !== 'undefined' &&
    (window as WindowWithGoogle).google?.maps
  ) {
    return Promise.resolve(true)
  }

  if (loadPromise) {
    return loadPromise
  }

  loadPromise = new Promise<boolean>((resolve) => {
    const existing =
      document.getElementById(
        GOOGLE_MAPS_SCRIPT_ID,
      ) as HTMLScriptElement | null

    if (existing) {
      existing.addEventListener(
        'load',
        () => resolve(true),
      )
      existing.addEventListener(
        'error',
        () => resolve(false),
      )
      return
    }

    const script = document.createElement('script')
    script.id = GOOGLE_MAPS_SCRIPT_ID
    script.async = true
    script.defer = true

    const key = getGoogleMapsApiKey()
    script.src =
      'https://maps.googleapis.com/maps/api/js' +
      `?key=${encodeURIComponent(key ?? '')}` +
      '&v=weekly'

    script.addEventListener('load', () => {
      resolve(true)
    })

    script.addEventListener('error', () => {
      loadPromise = null
      resolve(false)
    })

    document.head.appendChild(script)
  })

  return loadPromise
}
