export type TitanBehaviorState =
  | 'observing'
  | 'contextual'
  | 'following-pointer'
  | 'peeking'
  | 'sleeping'

export interface TitanBehaviorSettings {
  autonomous: boolean

  followPointer: boolean

  doNotDisturb: boolean

  reducedMotion: boolean
}

export interface TitanPointer {
  x: number
  y: number
}

export interface TitanBehaviorDecision {
  state:
    TitanBehaviorState

  durationMs: number
}

export class TitanBehaviorEngine {
  private lastUserInteraction =
    Date.now()

  private lastPointerReaction =
    0

  private lastContextReaction =
    0

  private pointer:
    TitanPointer = {
      x: 0,
      y: 0,
    }

  registerUserInteraction() {
    this.lastUserInteraction =
      Date.now()
  }

  registerPointer(
    x: number,
    y: number,
  ) {
    this.pointer = {
      x,
      y,
    }
  }

  getPointer():
    TitanPointer {
    return {
      ...this.pointer,
    }
  }

  canReactToPointer(
    settings:
      TitanBehaviorSettings,
  ): boolean {
    if (
      !settings.autonomous ||
      !settings.followPointer ||
      settings.doNotDisturb ||
      settings.reducedMotion
    ) {
      return false
    }

    const now =
      Date.now()

    const userIdleFor =
      now -
      this.lastUserInteraction

    const pointerCooldown =
      now -
      this.lastPointerReaction

    return (
      userIdleFor >
        12_000 &&
      pointerCooldown >
        45_000
    )
  }

  beginPointerReaction() {
    this.lastPointerReaction =
      Date.now()
  }

  canReactContextually(
    settings:
      TitanBehaviorSettings,
  ): boolean {
    if (
      !settings.autonomous ||
      settings.doNotDisturb
    ) {
      return false
    }

    return (
      Date.now() -
        this.lastContextReaction >
      10_000
    )
  }

  beginContextReaction() {
    this.lastContextReaction =
      Date.now()
  }

  shouldSleep(): boolean {
    return (
      Date.now() -
        this.lastUserInteraction >
      180_000
    )
  }
}