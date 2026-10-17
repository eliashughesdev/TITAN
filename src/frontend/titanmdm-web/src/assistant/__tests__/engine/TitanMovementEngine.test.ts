import { describe, it, expect, vi } from 'vitest'
import { TitanMovementEngine } from '../../engine/TitanMovementEngine'

describe('TitanMovementEngine', () => {
  it('transitions from idle to walking when moving short distance', () => {
    vi.useFakeTimers()
    const engine = new TitanMovementEngine({ x: 0, y: 0 })
    engine.moveTo({ x: 100, y: 0 })
    expect(engine.getSnapshot().state).toBe('walking')
    vi.restoreAllMocks()
  })

  it('prepares takeoff for long distances when flight allowed', () => {
    vi.useFakeTimers()
    const engine = new TitanMovementEngine({ x: 0, y: 0 })
    engine.moveTo({ x: 600, y: 0 })
    expect(engine.getSnapshot().state).toBe('prepare-takeoff')
    vi.restoreAllMocks()
  })

  it('can move to target and stop', () => {
    vi.useFakeTimers()
    const engine = new TitanMovementEngine({ x: 0, y: 0 })
    engine.moveTo({ x: 200, y: 0 })
    engine.stop()
    const snap = engine.getSnapshot()
    expect(snap.target).toBeNull()
    expect(snap.speed).toBe(0)
    vi.restoreAllMocks()
  })
})
