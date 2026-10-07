import {
  Activity,
  Battery,
  Clock3,
  ShieldCheck,
} from 'lucide-react'

import type {
  DeviceDetails,
} from '../../../types/device'

import {
  formatDeviceDate,
  getBatteryText,
} from '../utils/deviceDetail.utils'

interface Props {
  device: DeviceDetails
}

export function DeviceDetailKpis({
  device,
}: Props) {
  return (
    <section className="device-detail-kpis">
      <article>
        <Activity
          size={19}
        />

        <div>
          <span>
            Estado
          </span>

          <strong>
            {device.status}
          </strong>
        </div>
      </article>

      <article>
        <ShieldCheck
          size={19}
        />

        <div>
          <span>
            Cumplimiento
          </span>

          <strong>
            {
              device
                .complianceStatus
            }
          </strong>
        </div>
      </article>

      <article>
        <Battery
          size={19}
        />

        <div>
          <span>
            Batería
          </span>

          <strong>
            {getBatteryText(
              device.batteryLevel,
            )}
          </strong>
        </div>
      </article>

      <article>
        <Clock3
          size={19}
        />

        <div>
          <span>
            Última comunicación
          </span>

          <strong>
            {formatDeviceDate(
              device.lastSeenAtUtc,
            )}
          </strong>
        </div>
      </article>
    </section>
  )
}