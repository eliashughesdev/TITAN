// Fiorella dentro del módulo Ponches de TitanMDM.

export type FiorellaAction =
  | 'idle'
  | 'walk'
  | 'jump'
  | 'point'
  | 'wave'
  | 'celebrate'
  | 'surprised'
  | 'sleep'
  | 'think'
  | 'alert'
  | 'peek'
  | 'hide'

export interface FiorellaEvents {
  offlineDevices?: number
  syncError?: boolean
  syncSuccess?: boolean
  hasDifferences?: boolean
  newPunch?: boolean
  attendanceHealthy?: boolean
  userIdle?: boolean
}

/*
 * Solo existen cuatro sprites en disco. Los estados nuevos
 * reutilizan el sprite más cercano hasta que haya arte dedicado.
 */
export const SPRITE_MAP: Record<FiorellaAction, string> = {
  idle: '/fiorella-idle.png',
  walk: '/fiorella-walk.png',
  jump: '/fiorella-jump.png',
  point: '/fiorella-point.png',
  wave: '/fiorella-idle.png',
  celebrate: '/fiorella-jump.png',
  surprised: '/fiorella-jump.png',
  sleep: '/fiorella-idle.png',
  think: '/fiorella-point.png',
  alert: '/fiorella-point.png',
  peek: '/fiorella-idle.png',
  hide: '/fiorella-idle.png',
}
