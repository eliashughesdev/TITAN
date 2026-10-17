import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { TitanBehaviorEngine } from '../../engine/TitanBehaviorEngine'

describe('TitanBehaviorEngine', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('blocks pointer reaction by default when conditions not met', () => {
    const engine = new TitanBehaviorEngine()
    const can = engine.canReactToPointer({
      autonomous: true,
      followPointer: true,
      doNotDisturb: false,
      reducedMotion: false,
    })
    expect(can).toBe(false)
  })

  it('allows contextual reaction when idle time passed', () => {
    const engine = new TitanBehaviorEngine()
    vi.advanceTimersByTime(11_000)
    expect(
      engine.canReactContextually({
        autonomous: true,
        doNotDisturb: false,
        followPointer: true,
        reducedMotion: false,
      }),
    ).toBe(true)
  })

  it('should sleep after long inactivity', () => {
    const engine = new TitanBehaviorEngine()
    vi.advanceTimersByTime(181_000)
    expect(engine.shouldSleep()).toBe(true)
  })
})
