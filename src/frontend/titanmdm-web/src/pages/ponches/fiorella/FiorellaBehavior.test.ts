import {
  describe,
  expect,
  it,
} from 'vitest'

import { FiorellaBehaviorEngine } from './FiorellaBehavior'

describe('FiorellaBehaviorEngine', () => {
  it('reacciona a un error de sincronización con alert', () => {
    const engine = new FiorellaBehaviorEngine()

    expect(
      engine.decide(
        { syncError: true },
        () => 'idle',
      ),
    ).toBe('alert')
  })

  it('se sorprende cuando hay dispositivos offline', () => {
    const engine = new FiorellaBehaviorEngine()

    expect(
      engine.decide(
        { offlineDevices: 2 },
        () => 'idle',
      ),
    ).toBe('surprised')
  })

  it('deambula cuando no hay señales reales', () => {
    const engine = new FiorellaBehaviorEngine()

    expect(
      engine.decide({}, () => 'walk'),
    ).toBe('walk')
  })
})
