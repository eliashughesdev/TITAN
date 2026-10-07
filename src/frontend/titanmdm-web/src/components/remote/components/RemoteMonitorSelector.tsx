import {
  ChevronLeft,
  ChevronRight,
  Monitor,
} from 'lucide-react'

import type {
  RemoteMonitorInfo,
} from '../../../api/remoteSupportSignalR'

interface Props {
  monitors:
    RemoteMonitorInfo[]

  selectedMonitorIndex:
    number

  canControl:
    boolean

  onSelect:
    (
      index: number,
    ) => Promise<void>

  onPrevious:
    () => Promise<void>

  onNext:
    () => Promise<void>
}

export function RemoteMonitorSelector({
  monitors,
  selectedMonitorIndex,
  canControl,
  onSelect,
  onPrevious,
  onNext,
}: Props) {
  if (
    monitors.length ===
    0
  ) {
    return null
  }

  return (
    <section className="remote-monitor-selector">
      <button
        type="button"
        disabled={
          !canControl
          ||
          monitors.length <= 1
        }
        onClick={() =>
          void onPrevious()
        }
      >
        <ChevronLeft
          size={16}
        />
      </button>

      <div>
        <Monitor size={16} />

        <select
          value={
            selectedMonitorIndex
          }
          disabled={
            !canControl
          }
          onChange={
            event =>
              void onSelect(
                Number(
                  event.target.value,
                ),
              )
          }
        >
          {monitors.map(
            monitor => (
              <option
                key={monitor.index}
                value={monitor.index}
              >
                {monitor.label}
                {' · '}
                {monitor.width}
                ×
                {monitor.height}
              </option>
            ),
          )}
        </select>
      </div>

      <button
        type="button"
        disabled={
          !canControl
          ||
          monitors.length <= 1
        }
        onClick={() =>
          void onNext()
        }
      >
        <ChevronRight
          size={16}
        />
      </button>
    </section>
  )
}