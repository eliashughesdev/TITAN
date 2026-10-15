import {
  Monitor,
  Smartphone,
} from 'lucide-react'

export type KioskPlatform =
  | 'Windows'
  | 'Android'

interface Props {
  value:
    KioskPlatform

  onChange:
    (
      platform:
        KioskPlatform,
    ) => void
}

export function KioskPlatformSelector({
  value,
  onChange,
}: Props) {
  return (
    <div
      className="kiosk-platform-tabs"
      role="tablist"
      aria-label="Plataforma de kiosk"
    >
      <button
        type="button"
        className={
          value ===
          'Windows'
            ? 'active'
            : ''
        }
        role="tab"
        aria-selected={value === 'Windows'}
        onClick={() =>
          onChange(
            'Windows',
          )
        }
      >
        <Monitor size={18} />

        <div>
          <strong>
            Windows
          </strong>

          <span>
            Assigned Access
          </span>
        </div>
      </button>

      <button
        type="button"
        className={
          value ===
          'Android'
            ? 'active'
            : ''
        }
        role="tab"
        aria-selected={value === 'Android'}
        onClick={() =>
          onChange(
            'Android',
          )
        }
      >
        <Smartphone size={18} />

        <div>
          <strong>
            Android
          </strong>

          <span>
            Android Enterprise
          </span>
        </div>
      </button>
    </div>
  )
}
