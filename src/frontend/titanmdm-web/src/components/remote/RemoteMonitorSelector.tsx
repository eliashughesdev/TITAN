import type {
  RemoteMonitorInfo,
} from '../../api/remoteSupportSignalR'

interface Props {
  monitors: RemoteMonitorInfo[]
  selectedMonitorIndex: number
  disabled?: boolean

  onPrevious: () => void
  onNext: () => void

  onSelect: (
    index: number,
  ) => void
}

export default function RemoteMonitorSelector({
  monitors,
  selectedMonitorIndex,
  disabled = false,
  onPrevious,
  onNext,
  onSelect,
}: Props) {
  if (
    monitors.length <=
    1
  ) {
    return null
  }

  const selected =
    monitors.find(
      monitor =>
        monitor.index ===
        selectedMonitorIndex,
    )
    ??
    monitors[0]

  return (
    <div
      style={{
        display:
          'flex',

        alignItems:
          'center',

        gap:
          8,

        padding:
          '7px 9px',

        border:
          '1px solid #cbd5e1',

        borderRadius:
          9,

        background:
          '#ffffff',
      }}
    >
      <button
        type="button"
        disabled={
          disabled
        }
        onClick={
          onPrevious
        }
        title="Monitor anterior"
      >
        ◀
      </button>

      <select
        value={
          selected.index
        }
        disabled={
          disabled
        }
        onChange={(
          event,
        ) =>
          onSelect(
            Number(
              event.target.value,
            ),
          )
        }
      >
        {monitors.map(
          monitor => (
            <option
              key={
                monitor.index
              }
              value={
                monitor.index
              }
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

      <span
        style={{
          fontSize:
            12,

          opacity:
            0.7,
        }}
      >
        {selectedMonitorIndex + 1}
        /
        {monitors.length}
      </span>

      <button
        type="button"
        disabled={
          disabled
        }
        onClick={
          onNext
        }
        title="Monitor siguiente"
      >
        ▶
      </button>
    </div>
  )
}