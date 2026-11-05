export interface MapProviderConfig {
  name: string
  url: string
  attribution: string
  maxZoom: number
  productionGuidance: string
}

export const MAP_PROVIDERS: Record<string, MapProviderConfig> = {
  openstreetmap: {
    name: 'OpenStreetMap',
    url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution:
      '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 19,
    productionGuidance:
      'Las teselas públicas de OpenStreetMap no ofrecen SLA para cargas de flota. Use un servidor propio o un proveedor contratado en producción.',
  },
}

export const DEFAULT_MAP_PROVIDER = 'openstreetmap'

export function getActiveProvider(): MapProviderConfig {
  const corporateUrl = import.meta.env.VITE_MAP_TILE_URL?.trim()

  if (!corporateUrl) {
    return MAP_PROVIDERS[DEFAULT_MAP_PROVIDER]
  }

  return {
    name:
      import.meta.env.VITE_MAP_TILE_PROVIDER_NAME?.trim() ||
      'Proveedor corporativo',
    url: corporateUrl,
    attribution:
      import.meta.env.VITE_MAP_TILE_ATTRIBUTION?.trim() ||
      'Cartografía corporativa',
    maxZoom: 19,
    productionGuidance:
      'Proveedor de teselas configurado por la organización.',
  }
}
