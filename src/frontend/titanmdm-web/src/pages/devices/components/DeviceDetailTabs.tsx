import {
  CheckCircle2,
  Cpu,
  Database,
  Info,
  Network,
  RefreshCw,
  ShieldCheck,
  Smartphone,
  Terminal,
} from 'lucide-react'

export type DeviceDetailTab =
  | 'overview'
  | 'enterprise'
  | 'hardware'
  | 'system'
  | 'security'
  | 'policy'
  | 'sync'
  | 'network'
  | 'commands'

import type {
  ReactNode,
} from 'react'

interface Props {
  activeTab:
    DeviceDetailTab

  isAndroid:
    boolean

  commandCount:
    number

  onChange:
    (
      tab:
        DeviceDetailTab,
    ) => void
}

export function DeviceDetailTabs({
  activeTab,
  isAndroid,
  commandCount,
  onChange,
}: Props) {
  function tab(
    name:
      DeviceDetailTab,
    label:
      string,
    icon:
      ReactNode,
    count?:
      number,
  ) {
    return (
      <button
        type="button"
        className={
          activeTab ===
          name
            ? 'active'
            : ''
        }
        onClick={() =>
          onChange(name)
        }
      >
        {icon}
        {label}

        {count !==
          undefined && (
          <span>
            {count}
          </span>
        )}
      </button>
    )
  }

  return (
    <nav className="device-detail-tabs">
      {tab(
        'overview',
        'Resumen',
        <Info size={16} />,
      )}

      {isAndroid &&
        tab(
          'enterprise',
          'Android Enterprise',
          <Smartphone
            size={16}
          />,
        )}

      {tab(
        'hardware',
        'Hardware',
        <Cpu size={16} />,
      )}

      {isAndroid && (
        <>
          {tab(
            'system',
            'Sistema',
            <Database
              size={16}
            />,
          )}

          {tab(
            'security',
            'Seguridad',
            <ShieldCheck
              size={16}
            />,
          )}

          {tab(
            'policy',
            'Política',
            <CheckCircle2
              size={16}
            />,
          )}

          {tab(
            'sync',
            'Sincronización',
            <RefreshCw
              size={16}
            />,
          )}
        </>
      )}

      {tab(
        'network',
        'Red',
        <Network size={16} />,
      )}

      {tab(
        'commands',
        'Comandos',
        <Terminal
          size={16}
        />,
        commandCount,
      )}
    </nav>
  )
}