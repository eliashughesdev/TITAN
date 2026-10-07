import {
  Lock,
  Power,
  RotateCw,
  Terminal,
} from 'lucide-react'

import type {
  Dispatch,
  SetStateAction,
} from 'react'

import type {
  SendWindowsCommand,
} from '../windowsControl.types'

interface Props {
  scriptPath: string

  setScriptPath:
    Dispatch<
      SetStateAction<string>
    >

  scriptSha256: string

  setScriptSha256:
    Dispatch<
      SetStateAction<string>
    >

  scriptTimeout: string

  setScriptTimeout:
    Dispatch<
      SetStateAction<string>
    >

  sendCommand:
    SendWindowsCommand
}

export function WindowsActionsSection({
  scriptPath,
  setScriptPath,
  scriptSha256,
  setScriptSha256,
  scriptTimeout,
  setScriptTimeout,
  sendCommand,
}: Props) {
  function confirmCommand(
    command: string,
    message: string,
  ) {
    if (
      window.confirm(
        message,
      )
    ) {
      void sendCommand(
        command,
      )
    }
  }

  return (
    <section className="windows-control-grid">
      <article className="windows-control-card">
        <header>
          <Power size={18} />

          <h2>
            Acciones del endpoint
          </h2>
        </header>

        <div className="windows-control-danger-actions">
          <button
            type="button"
            onClick={() =>
              confirmCommand(
                'LOCK_DEVICE',
                '¿Bloquear la sesión del dispositivo?',
              )
            }
          >
            <Lock size={17} />
            Bloquear
          </button>

          <button
            type="button"
            onClick={() =>
              confirmCommand(
                'RESTART_DEVICE',
                '¿Reiniciar este dispositivo?',
              )
            }
          >
            <RotateCw size={17} />
            Reiniciar
          </button>

          <button
            type="button"
            className="danger"
            onClick={() =>
              confirmCommand(
                'SHUTDOWN_DEVICE',
                '¿Apagar este dispositivo?',
              )
            }
          >
            <Power size={17} />
            Apagar
          </button>
        </div>
      </article>

      <article className="windows-control-card">
        <header>
          <Terminal size={18} />

          <h2>
            Script firmado
          </h2>
        </header>

        <div className="windows-control-form-stack">
          <input
            value={scriptPath}
            onChange={
              event =>
                setScriptPath(
                  event.target.value,
                )
            }
            placeholder="Ruta del script"
          />

          <input
            value={scriptSha256}
            onChange={
              event =>
                setScriptSha256(
                  event.target.value,
                )
            }
            placeholder="SHA-256"
          />

          <input
            type="number"
            min="30"
            max="3600"
            value={scriptTimeout}
            onChange={
              event =>
                setScriptTimeout(
                  event.target.value,
                )
            }
          />

          <button
            type="button"
            disabled={
              !scriptPath.trim()
              ||
              !scriptSha256.trim()
            }
            onClick={() =>
              void sendCommand(
                'SCRIPT_EXECUTE',
                {
                  scriptPath:
                    scriptPath.trim(),

                  expectedSha256:
                    scriptSha256.trim(),

                  timeoutSeconds:
                    Number(
                      scriptTimeout,
                    ),
                },
              )
            }
          >
            Ejecutar
          </button>
        </div>
      </article>
    </section>
  )
}