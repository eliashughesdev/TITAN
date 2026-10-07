import {
  CloudCog,
  RefreshCw,
} from 'lucide-react'

interface Props {
  eyebrow: string
  description: string

  isAndroidWorkspace:
    boolean

  isLoading:
    boolean

  isSyncingAndroid:
    boolean

  onRefresh:
    () => void

  onAndroidSync:
    () => void
}

export function DevicesHeader({
  eyebrow,
  description,

  isAndroidWorkspace,

  isLoading,
  isSyncingAndroid,

  onRefresh,
  onAndroidSync,
}: Props) {
  return (
    <div className="devices-heading">
      <div>
        <span className="devices-heading__eyebrow">
          {eyebrow}
        </span>

        <h1>
          Dispositivos
        </h1>

        <p>
          {description}
        </p>
      </div>

      <div className="devices-heading__actions">
        <button
          type="button"
          className="devices-refresh-button"
          onClick={
            onRefresh
          }
          disabled={
            isLoading
            ||
            isSyncingAndroid
          }
        >
          <RefreshCw
            size={17}
            className={
              isLoading
                ? 'devices-icon-spinning'
                : ''
            }
          />

          Actualizar
        </button>

        {isAndroidWorkspace && (
          <button
            type="button"
            className="devices-android-sync-button"
            onClick={
              onAndroidSync
            }
            disabled={
              isSyncingAndroid
            }
          >
            <CloudCog
              size={17}
              className={
                isSyncingAndroid
                  ? 'devices-icon-spinning'
                  : ''
              }
            />

            {isSyncingAndroid
              ? 'Sincronizando...'
              : 'Sincronizar Android'}
          </button>
        )}
      </div>
    </div>
  )
}