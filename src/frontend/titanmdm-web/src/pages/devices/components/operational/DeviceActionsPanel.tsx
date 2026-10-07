import {
  Activity,
  AppWindow,
  Info,
  LockKeyhole,
  Network,
  Power,
  RefreshCw,
  RotateCcw,
  ShieldCheck,
  Terminal,
  Wrench,
} from 'lucide-react'

import type {
  LucideIcon,
} from 'lucide-react'

interface Props {
  busy: boolean

  onFullRefresh:
    () => Promise<void>

  onExecute:
    (
      commandType: string,
      payloadJson?: string,
    ) => Promise<void>
}

interface ActionDefinition {
  title: string
  description: string
  commandType: string
  risk:
    | 'safe'
    | 'warning'
    | 'danger'
  icon: LucideIcon
}

const diagnosticActions:
  ActionDefinition[] = [
    {
      title:
        'Información del dispositivo',

      description:
        'Consulta identidad y versión del agente.',

      commandType:
        'DEVICE_INFO',

      risk:
        'safe',

      icon:
        Info,
    },

    {
      title:
        'Estado de red',

      description:
        'Actualiza adaptadores, IP, DNS y gateways.',

      commandType:
        'NETWORK_INFO',

      risk:
        'safe',

      icon:
        Network,
    },

    {
      title:
        'Estado de seguridad',

      description:
        'Consulta Defender, firewall, TPM y BitLocker.',

      commandType:
        'SECURITY_STATUS',

      risk:
        'safe',

      icon:
        ShieldCheck,
    },

    {
      title:
        'Procesos',

      description:
        'Obtiene los procesos activos del endpoint.',

      commandType:
        'PROCESS_INVENTORY',

      risk:
        'safe',

      icon:
        Activity,
    },

    {
      title:
        'Servicios',

      description:
        'Obtiene los servicios registrados en Windows.',

      commandType:
        'SERVICE_INVENTORY',

      risk:
        'safe',

      icon:
        Wrench,
    },

    {
      title:
        'Software',

      description:
        'Actualiza el inventario de aplicaciones.',

      commandType:
        'APP_INVENTORY',

      risk:
        'safe',

      icon:
        AppWindow,
    },
  ]

const systemActions:
  ActionDefinition[] = [
    {
      title:
        'Bloquear equipo',

      description:
        'Bloquea la sesión interactiva actual.',

      commandType:
        'LOCK_DEVICE',

      risk:
        'warning',

      icon:
        LockKeyhole,
    },

    {
      title:
        'Reiniciar',

      description:
        'Reinicia Windows de forma remota.',

      commandType:
        'RESTART_DEVICE',

      risk:
        'danger',

      icon:
        RotateCcw,
    },

    {
      title:
        'Apagar',

      description:
        'Apaga completamente el endpoint.',

      commandType:
        'SHUTDOWN_DEVICE',

      risk:
        'danger',

      icon:
        Power,
    },
  ]

export function DeviceActionsPanel({
  busy,
  onFullRefresh,
  onExecute,
}: Props) {
  async function run(
    action:
      ActionDefinition,
  ) {
    if (
      action.risk !==
      'safe'
    ) {
      const confirmed =
        window.confirm(
          `${action.title}\n\n${action.description}\n\n¿Deseas continuar?`,
        )

      if (!confirmed) {
        return
      }
    }

    await onExecute(
      action.commandType,
    )
  }

  return (
    <article className="op-panel">
      <header>
        <Terminal
          size={18}
        />

        <div>
          <strong>
            Acciones
          </strong>

          <span>
            Operaciones remotas del endpoint
          </span>
        </div>

        <button
          type="button"
          className="op-refresh-all"
          disabled={busy}
          onClick={() =>
            void onFullRefresh()
          }
        >
          <RefreshCw
            size={14}
          />

          Actualizar todo
        </button>
      </header>

      <div className="op-action-section">
        <div className="op-action-section-title">
          Diagnóstico
        </div>

        <div className="op-action-grid">
          {diagnosticActions.map(
            action => {
              const Icon =
                action.icon

              return (
                <button
                  type="button"
                  disabled={busy}
                  className="op-action-card"
                  key={
                    action
                      .commandType
                  }
                  onClick={() =>
                    void run(
                      action,
                    )
                  }
                >
                  <span className="op-action-icon">
                    <Icon
                      size={20}
                    />
                  </span>

                  <strong>
                    {action.title}
                  </strong>

                  <small>
                    {
                      action
                        .description
                    }
                  </small>

                  <span className="op-risk safe">
                    Seguro
                  </span>
                </button>
              )
            },
          )}
        </div>
      </div>

      <div className="op-action-section">
        <div className="op-action-section-title">
          Sistema
        </div>

        <div className="op-action-grid">
          {systemActions.map(
            action => {
              const Icon =
                action.icon

              return (
                <button
                  type="button"
                  disabled={busy}
                  className={
                    `op-action-card ${action.risk}`
                  }
                  key={
                    action
                      .commandType
                  }
                  onClick={() =>
                    void run(
                      action,
                    )
                  }
                >
                  <span className="op-action-icon">
                    <Icon
                      size={20}
                    />
                  </span>

                  <strong>
                    {action.title}
                  </strong>

                  <small>
                    {
                      action
                        .description
                    }
                  </small>

                  <span
                    className={
                      `op-risk ${action.risk}`
                    }
                  >
                    {action.risk ===
                    'danger'
                      ? 'Crítico'
                      : 'Advertencia'}
                  </span>
                </button>
              )
            },
          )}
        </div>
      </div>
    </article>
  )
}