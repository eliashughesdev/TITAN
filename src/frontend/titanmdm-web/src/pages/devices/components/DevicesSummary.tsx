import {
  CheckCircle2,
  Laptop,
  MonitorSmartphone,
  Smartphone,
  Wifi,
  WifiOff,
} from 'lucide-react'

interface Props {
  total: number

  onlineDevices:
    number

  offlineDevices:
    number

  compliantDevices:
    number

  isWindowsWorkspace:
    boolean

  isAndroidWorkspace:
    boolean
}

export function DevicesSummary({
  total,

  onlineDevices,
  offlineDevices,
  compliantDevices,

  isWindowsWorkspace,
  isAndroidWorkspace,
}: Props) {
  return (
    <section className="devices-summary">
      <article className="devices-summary-card">
        <div className="devices-summary-card__icon">
          {isWindowsWorkspace ? (
            <Laptop
              size={20}
            />
          ) : isAndroidWorkspace ? (
            <Smartphone
              size={20}
            />
          ) : (
            <MonitorSmartphone
              size={20}
            />
          )}
        </div>

        <div>
          <span>
            Total
          </span>

          <strong>
            {total}
          </strong>

          <small>
            {isWindowsWorkspace
              ? 'Equipos Windows'
              : isAndroidWorkspace
                ? 'Dispositivos Android'
                : 'Dispositivos administrados'}
          </small>
        </div>
      </article>

      <article className="devices-summary-card">
        <div className="devices-summary-card__icon devices-summary-card__icon--online">
          <Wifi
            size={20}
          />
        </div>

        <div>
          <span>
            En línea
          </span>

          <strong>
            {onlineDevices}
          </strong>

          <small>
            En la página actual
          </small>
        </div>
      </article>

      <article className="devices-summary-card">
        <div className="devices-summary-card__icon devices-summary-card__icon--offline">
          <WifiOff
            size={20}
          />
        </div>

        <div>
          <span>
            Fuera de línea
          </span>

          <strong>
            {offlineDevices}
          </strong>

          <small>
            En la página actual
          </small>
        </div>
      </article>

      <article className="devices-summary-card">
        <div className="devices-summary-card__icon devices-summary-card__icon--compliant">
          <CheckCircle2
            size={20}
          />
        </div>

        <div>
          <span>
            Conformes
          </span>

          <strong>
            {compliantDevices}
          </strong>

          <small>
            En la página actual
          </small>
        </div>
      </article>
    </section>
  )
}