import {
  Activity,
  ArrowLeft,
  CheckCircle2,
  Laptop,
  Play,
  RadioTower,
  RefreshCw,
  ShieldCheck,
  SquareTerminal,
  Wifi,
  XCircle,
} from 'lucide-react'

import type {
  DeviceDetails,
} from '../../types/device'

import type {
  WindowsControlTab,
} from './windowsControl.types'

interface WindowsControlChromeProps {
  device:
    DeviceDetails

  activeTab:
    WindowsControlTab

  activeCommands:
    number

  successfulCommands:
    number

  failedCommands:
    number

  sendingCommand:
    string | null

  onBack:
    () => void

  onRefresh:
    () => void

  onRemote:
    () => void

  onPing:
    () => void

  onTabChange:
    (
      tab:
        WindowsControlTab,
    ) => void
}

const tabs:
  Array<{
    id: WindowsControlTab
    label: string
  }> = [
    {
      id: 'overview',
      label: 'Resumen',
    },
    {
      id: 'inventory',
      label: 'Inventario',
    },
    {
      id: 'security',
      label: 'Seguridad',
    },
    {
      id: 'updates',
      label: 'Windows Update',
    },
    {
      id: 'processes',
      label: 'Procesos',
    },
    {
      id: 'services',
      label: 'Servicios',
    },
    {
      id: 'software',
      label: 'Software',
    },
    {
      id: 'actions',
      label: 'Acciones',
    },
    {
      id: 'commands',
      label: 'Historial',
    },
  ]

export function WindowsControlChrome({
  device,
  activeTab,
  activeCommands,
  successfulCommands,
  failedCommands,
  sendingCommand,
  onBack,
  onRefresh,
  onRemote,
  onPing,
  onTabChange,
}: WindowsControlChromeProps) {
  return (
    <>
      <button
        type="button"
        className="windows-control-back"
        onClick={
          onBack
        }
      >
        <ArrowLeft
          size={16}
        />

        Dispositivos Windows
      </button>

      <header className="windows-control-hero">
        <div className="windows-control-device">
          <div className="windows-control-device__icon">
            <Laptop
              size={28}
            />
          </div>

          <div>
            <span className="windows-control-eyebrow">
              WINDOWS ENDPOINT
            </span>

            <div className="windows-control-title">
              <h1>
                {device.deviceName}
              </h1>

              <span
                className={
                  `windows-control-status windows-control-status--${device.status.toLowerCase()}`
                }
              >
                {device.status ===
                'Online'
                  ? (
                    <Wifi
                      size={13}
                    />
                  )
                  : (
                    <Activity
                      size={13}
                    />
                  )}

                {device.status}
              </span>
            </div>

            <p>
              {device.operatingSystem ??
                'Windows'}

              {' · '}

              {device.manufacturer ??
                'N/D'}

              {' · '}

              {device.model ??
                'N/D'}
            </p>
          </div>
        </div>

        <div className="windows-control-hero__actions">
          <button
            type="button"
            className="windows-control-secondary"
            onClick={
              onRefresh
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar
          </button>

          <button
            type="button"
            className="windows-control-secondary"
            onClick={
              onRemote
            }
          >
            <RadioTower
              size={16}
            />

            Soporte remoto
          </button>

          <button
            type="button"
            className="windows-control-primary"
            disabled={
              sendingCommand !==
              null
            }
            onClick={
              onPing
            }
          >
            <Play
              size={16}
            />

            PING
          </button>
        </div>
      </header>

      <section className="windows-control-kpis">
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
              Compliance
            </span>

            <strong>
              {device.complianceStatus}
            </strong>
          </div>
        </article>

        <article>
          <SquareTerminal
            size={19}
          />

          <div>
            <span>
              Activos
            </span>

            <strong>
              {activeCommands}
            </strong>
          </div>
        </article>

        <article>
          <CheckCircle2
            size={19}
          />

          <div>
            <span>
              Correctos
            </span>

            <strong>
              {successfulCommands}
            </strong>
          </div>
        </article>

        <article>
          <XCircle
            size={19}
          />

          <div>
            <span>
              Fallidos
            </span>

            <strong>
              {failedCommands}
            </strong>
          </div>
        </article>
      </section>

      <nav className="windows-control-tabs">
        {tabs.map(
          tab => (
            <button
              key={
                tab.id
              }
              type="button"
              className={
                activeTab ===
                tab.id
                  ? 'active'
                  : ''
              }
              onClick={() =>
                onTabChange(
                  tab.id,
                )
              }
            >
              {tab.label}
            </button>
          ),
        )}
      </nav>
    </>
  )
}