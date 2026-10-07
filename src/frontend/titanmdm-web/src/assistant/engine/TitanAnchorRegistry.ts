export interface TitanAnchorPosition {
  name: string

  element: HTMLElement

  rect: DOMRect

  x: number

  y: number
}

export interface TitanSafePoint {
  x: number
  y: number
}

const BLOCKING_SELECTOR = [
  'input',
  'textarea',
  'select',
  '[role="dialog"]',
  '[role="menu"]',
  '[data-titan-blocking="true"]',
].join(',')

export class TitanAnchorRegistry {
  static get(
    name: string,
  ): TitanAnchorPosition | null {
    const element =
      document.querySelector<
        HTMLElement
      >(
        `[data-titan-anchor="${name}"]`,
      )

    if (!element) {
      return null
    }

    return this.fromElement(
      name,
      element,
    )
  }

  static getAll():
    TitanAnchorPosition[] {
    return Array.from(
      document.querySelectorAll<
        HTMLElement
      >('[data-titan-anchor]'),
    )
      .map((element) => {
        const name =
          element.dataset
            .titanAnchor

        if (!name) {
          return null
        }

        return this.fromElement(
          name,
          element,
        )
      })
      .filter(
        (
          item,
        ): item is TitanAnchorPosition =>
          item !== null,
      )
  }

  static exists(
    name: string,
  ): boolean {
    return this.get(name) !== null
  }

  static getSafePointNear(
    name: string,
    distance = 85,
  ): TitanSafePoint | null {
    const anchor =
      this.get(name)

    if (!anchor) {
      return null
    }

    const candidates:
      TitanSafePoint[] = [
      {
        x:
          anchor.rect.right +
          distance,

        y:
          anchor.y - 60,
      },

      {
        x:
          anchor.rect.left -
          distance -
          80,

        y:
          anchor.y - 60,
      },

      {
        x:
          anchor.x - 40,

        y:
          anchor.rect.bottom +
          distance,
      },

      {
        x:
          anchor.x - 40,

        y:
          anchor.rect.top -
          distance -
          120,
      },
    ]

    for (
      const candidate
      of candidates
    ) {
      const clamped =
        this.clampToViewport(
          candidate,
        )

      if (
        this.isSafePoint(
          clamped,
        )
      ) {
        return clamped
      }
    }

    return this.clampToViewport({
      x:
        anchor.rect.right +
        40,

      y:
        anchor.rect.bottom +
        20,
    })
  }

  static isSafePoint(
    point: TitanSafePoint,
  ): boolean {
    const samplePoints = [
      {
        x:
          point.x + 35,
        y:
          point.y + 40,
      },

      {
        x:
          point.x + 70,
        y:
          point.y + 80,
      },

      {
        x:
          point.x + 35,
        y:
          point.y + 125,
      },
    ]

    for (
      const sample
      of samplePoints
    ) {
      const element =
        document.elementFromPoint(
          sample.x,
          sample.y,
        )

      if (
        element?.closest(
          BLOCKING_SELECTOR,
        )
      ) {
        return false
      }
    }

    return true
  }

  static clampToViewport(
    point: TitanSafePoint,
  ): TitanSafePoint {
    return {
      x: Math.max(
        8,
        Math.min(
          window.innerWidth -
            125,
          point.x,
        ),
      ),

      y: Math.max(
        72,
        Math.min(
          window.innerHeight -
            185,
          point.y,
        ),
      ),
    }
  }

  private static fromElement(
    name: string,
    element: HTMLElement,
  ): TitanAnchorPosition {
    const rect =
      element.getBoundingClientRect()

    return {
      name,
      element,
      rect,

      x:
        rect.left +
        rect.width / 2,

      y:
        rect.top +
        rect.height / 2,
    }
  }
}