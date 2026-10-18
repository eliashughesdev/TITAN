export interface MapProviderConfig {
  url: string;
  attribution: string;
}

export const MAP_PROVIDERS: Record<string, MapProviderConfig> = {
  openstreetmap: {
    url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
  },
  // Corporate tiles can be added here for production
  // corporate: {
  //   url: 'https://tiles.titanmdm.internal/{z}/{x}/{y}.png',
  //   attribution: 'TitanMDM Corporate Cartography',
  // },
};

export const DEFAULT_MAP_PROVIDER = 'openstreetmap';

export const getActiveProvider = () => {
  // This could be tied to an environment variable in the future
  return MAP_PROVIDERS[DEFAULT_MAP_PROVIDER];
};
