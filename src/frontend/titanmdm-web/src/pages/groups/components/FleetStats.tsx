import {
  Activity,
  Cpu,
  Laptop,
  ShieldCheck,
  Smartphone,
  UsersRound,
  Wifi,
} from 'lucide-react'
import type { ReactNode } from 'react'

interface FleetStatsProps {
  groupCount: number
  deviceCount: number
  windowsDevices: number
  androidDevices: number
  onlineDevices: number
  compliantDevices: number
  totalMembers: number
}

export function FleetStats({
  groupCount,
  deviceCount,
  windowsDevices,
  androidDevices,
  onlineDevices,
  compliantDevices,
  totalMembers,
}: FleetStatsProps) {
  return (
    <section className="fleet-stats">
      <Stat
        icon={<UsersRound size={20} />}
        label="Grupos"
        value={groupCount}
      />

      <Stat
        icon={<Cpu size={20} />}
        label="Dispositivos"
        value={deviceCount}
      />

      <Stat
        icon={<Laptop size={20} />}
        label="Windows"
        value={windowsDevices}
      />

      <Stat
        icon={<Smartphone size={20} />}
        label="Android"
        value={androidDevices}
      />

      <Stat
        icon={<Wifi size={20} />}
        label="En línea"
        value={onlineDevices}
      />

      <Stat
        icon={<ShieldCheck size={20} />}
        label="Conformes"
        value={compliantDevices}
      />

      <Stat
        icon={<Activity size={20} />}
        label="Membresías"
        value={totalMembers}
      />
    </section>
  )
}

function Stat({
  icon,
  label,
  value,
}: {
  icon: ReactNode
  label: string
  value: number
}) {
  return (
    <article className="fleet-stat">
      <div>{icon}</div>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  )
}
