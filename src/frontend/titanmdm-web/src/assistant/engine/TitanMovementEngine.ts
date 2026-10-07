export type TitanLocomotionState =
  | 'idle'
  | 'walking'
  | 'prepare-takeoff'
  | 'takeoff'
  | 'flying'
  | 'landing'

export type TitanDirection =
  | 'left'
  | 'right'

export interface TitanCoordinates {
  x: number
  y: number
}

export interface TitanMovementSnapshot {
  position:
    TitanCoordinates

  velocity:
    TitanCoordinates

  speed: number

  direction:
    TitanDirection

  state:
    TitanLocomotionState

  moving: boolean

  target:
    TitanCoordinates | null
}

interface MoveOptions {
  allowFlight?: boolean

  forceFlight?: boolean
}

type SnapshotListener = (
  snapshot:
    TitanMovementSnapshot,
) => void

type ArrivalListener = (
  snapshot:
    TitanMovementSnapshot,
) => void

const WALK_SPEED = 150

const FLIGHT_SPEED = 410

const WALK_ACCELERATION = 500

const FLIGHT_ACCELERATION = 720

const DECELERATION = 700

const ARRIVAL_RADIUS = 5

const FLIGHT_DISTANCE = 500

export class TitanMovementEngine {
  private position:
    TitanCoordinates

  private velocity:
    TitanCoordinates = {
      x: 0,
      y: 0,
    }

  private target:
    TitanCoordinates | null =
    null

  private state:
    TitanLocomotionState =
    'idle'

  private direction:
    TitanDirection =
    'right'

  private allowFlight = true

  private frameId:
    number | null = null

  private previousTime:
    number | null = null

  private stateStartedAt =
    performance.now()

  private listener:
    SnapshotListener | null =
    null

  private arrivalListener:
    ArrivalListener | null =
    null

  constructor(
    initialPosition:
      TitanCoordinates,
  ) {
    this.position = {
      ...initialPosition,
    }
  }

  setListener(
    listener:
      SnapshotListener | null,
  ) {
    this.listener = listener
  }

  setArrivalListener(
    listener:
      ArrivalListener | null,
  ) {
    this.arrivalListener =
      listener
  }

  setAllowFlight(
    allow: boolean,
  ) {
    this.allowFlight = allow
  }

  getSnapshot():
    TitanMovementSnapshot {
    return {
      position: {
        ...this.position,
      },

      velocity: {
        ...this.velocity,
      },

      speed:
        Math.hypot(
          this.velocity.x,
          this.velocity.y,
        ),

      direction:
        this.direction,

      state:
        this.state,

      moving:
        this.target !== null,

      target:
        this.target
          ? {
              ...this.target,
            }
          : null,
    }
  }

  setPosition(
    position:
      TitanCoordinates,
  ) {
    this.position = {
      ...position,
    }

    this.emit()
  }

  moveTo(
    target:
      TitanCoordinates,

    options:
      MoveOptions = {},
  ) {
    this.target = {
      ...target,
    }

    const distance =
      Math.hypot(
        target.x -
          this.position.x,

        target.y -
          this.position.y,
      )

    const shouldFly =
      options.forceFlight ===
        true ||
      (
        options.allowFlight !==
          false &&
        this.allowFlight &&
        distance >=
          FLIGHT_DISTANCE
      )

    if (shouldFly) {
      this.transitionTo(
        'prepare-takeoff',
      )
    } else {
      this.transitionTo(
        'walking',
      )
    }

    this.updateDirection(
      target.x -
        this.position.x,
    )

    this.start()
  }

  stop() {
    this.target = null

    this.velocity = {
      x: 0,
      y: 0,
    }

    this.transitionTo(
      'idle',
    )

    this.stopFrame()

    this.emit()
  }

  destroy() {
    this.stopFrame()

    this.listener = null

    this.arrivalListener =
      null
  }

  private start() {
    if (
      this.frameId !== null
    ) {
      return
    }

    this.previousTime =
      null

    this.frameId =
      requestAnimationFrame(
        this.tick,
      )
  }

  private stopFrame() {
    if (
      this.frameId !== null
    ) {
      cancelAnimationFrame(
        this.frameId,
      )
    }

    this.frameId = null

    this.previousTime =
      null
  }

  private tick = (
    timestamp: number,
  ) => {
    if (
      this.previousTime ===
      null
    ) {
      this.previousTime =
        timestamp

      this.frameId =
        requestAnimationFrame(
          this.tick,
        )

      return
    }

    const deltaTime =
      Math.min(
        (
          timestamp -
          this.previousTime
        ) / 1000,

        0.033,
      )

    this.previousTime =
      timestamp

    this.update(
      deltaTime,
      timestamp,
    )

    if (
      this.target ||
      this.state !== 'idle'
    ) {
      this.frameId =
        requestAnimationFrame(
          this.tick,
        )
    } else {
      this.stopFrame()
    }
  }

  private update(
    deltaTime: number,
    timestamp: number,
  ) {
    if (
      this.state ===
      'prepare-takeoff'
    ) {
      this.velocity.x *=
        0.82

      this.velocity.y *=
        0.82

      if (
        timestamp -
          this.stateStartedAt >
        350
      ) {
        this.transitionTo(
          'takeoff',
        )
      }

      this.emit()

      return
    }

    if (
      this.state ===
      'takeoff'
    ) {
      this.velocity.y =
        this.approach(
          this.velocity.y,
          -130,
          600 * deltaTime,
        )

      this.position.y +=
        this.velocity.y *
        deltaTime

      if (
        timestamp -
          this.stateStartedAt >
        280
      ) {
        this.transitionTo(
          'flying',
        )
      }

      this.emit()

      return
    }

    if (
      this.state ===
      'landing'
    ) {
      this.velocity.x =
        this.approach(
          this.velocity.x,
          0,
          DECELERATION *
            deltaTime,
        )

      this.velocity.y =
        this.approach(
          this.velocity.y,
          0,
          DECELERATION *
            deltaTime,
        )

      if (
        timestamp -
          this.stateStartedAt >
        300
      ) {
        this.finishArrival()
      }

      this.emit()

      return
    }

    if (!this.target) {
      this.velocity.x =
        this.approach(
          this.velocity.x,
          0,
          DECELERATION *
            deltaTime,
        )

      this.velocity.y =
        this.approach(
          this.velocity.y,
          0,
          DECELERATION *
            deltaTime,
        )

      if (
        Math.hypot(
          this.velocity.x,
          this.velocity.y,
        ) < 1
      ) {
        this.velocity = {
          x: 0,
          y: 0,
        }

        this.transitionTo(
          'idle',
        )
      }

      this.emit()

      return
    }

    const dx =
      this.target.x -
      this.position.x

    const dy =
      this.target.y -
      this.position.y

    const distance =
      Math.hypot(
        dx,
        dy,
      )

    if (
      distance <=
      ARRIVAL_RADIUS
    ) {
      this.position = {
        ...this.target,
      }

      if (
        this.state ===
        'flying'
      ) {
        this.transitionTo(
          'landing',
        )
      } else {
        this.finishArrival()
      }

      this.emit()

      return
    }

    const normalX =
      dx / distance

    const normalY =
      dy / distance

    const flying =
      this.state ===
      'flying'

    const maxSpeed =
      flying
        ? FLIGHT_SPEED
        : WALK_SPEED

    const acceleration =
      flying
        ? FLIGHT_ACCELERATION
        : WALK_ACCELERATION

    const currentSpeed =
      Math.hypot(
        this.velocity.x,
        this.velocity.y,
      )

    const brakingDistance =
      (
        currentSpeed *
        currentSpeed
      ) /
      (
        2 *
        DECELERATION
      )

    let desiredSpeed =
      maxSpeed

    if (
      distance <
      brakingDistance + 50
    ) {
      desiredSpeed =
        Math.max(
          flying
            ? 90
            : 25,

          maxSpeed *
            (
              distance /
              Math.max(
                brakingDistance +
                  50,
                1,
              )
            ),
        )
    }

    const desiredVelocityX =
      normalX *
      desiredSpeed

    const desiredVelocityY =
      normalY *
      desiredSpeed

    this.velocity.x =
      this.approach(
        this.velocity.x,
        desiredVelocityX,
        acceleration *
          deltaTime,
      )

    this.velocity.y =
      this.approach(
        this.velocity.y,
        desiredVelocityY,
        acceleration *
          deltaTime,
      )

    this.position.x +=
      this.velocity.x *
      deltaTime

    this.position.y +=
      this.velocity.y *
      deltaTime

    this.updateDirection(
      this.velocity.x,
    )

    this.emit()
  }

  private finishArrival() {
    this.target = null

    this.velocity = {
      x: 0,
      y: 0,
    }

    this.transitionTo(
      'idle',
    )

    this.emit()

    this.arrivalListener?.(
      this.getSnapshot(),
    )
  }

  private transitionTo(
    state:
      TitanLocomotionState,
  ) {
    if (
      this.state === state
    ) {
      return
    }

    this.state = state

    this.stateStartedAt =
      performance.now()
  }

  private updateDirection(
    horizontalVelocity:
      number,
  ) {
    if (
      Math.abs(
        horizontalVelocity,
      ) < 1
    ) {
      return
    }

    this.direction =
      horizontalVelocity < 0
        ? 'left'
        : 'right'
  }

  private approach(
    current: number,
    target: number,
    amount: number,
  ): number {
    if (
      current < target
    ) {
      return Math.min(
        current + amount,
        target,
      )
    }

    if (
      current > target
    ) {
      return Math.max(
        current - amount,
        target,
      )
    }

    return target
  }

  private emit() {
    this.listener?.(
      this.getSnapshot(),
    )
  }
}