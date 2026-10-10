import {
  SquareTerminal,
} from 'lucide-react'

import {
  useEffect,
  useState,
} from 'react'

import type {
  DeviceCommand,
} from '../../../api/deviceCommandsApi'

import {
  formatDate,
  prettyResult,
} from '../windowsControl.utils'

import {
  formatCommandElapsed,
  getCommandStatusLabel,
  isTerminalCommand,
} from '../../../utils/deviceCommandPresentation'

interface Props {
  commands:
    DeviceCommand[]
}

export function WindowsCommandHistorySection({
  commands,
}: Props) {
  const [now, setNow] = useState(0)

  useEffect(() => {
    if (commands.every(command =>
      isTerminalCommand(command.status),
    )) {
      return
    }

    const timer = window.setInterval(
      () => setNow(Date.now()),
      1000,
    )

    return () => window.clearInterval(timer)
  }, [commands])

  return (
    <section className="windows-control-single">
      <article className="windows-control-card windows-control-card--wide">
        <header>
          <SquareTerminal size={18} />

          <h2>
            Historial de comandos
          </h2>

          <span>
            {commands.length} registros
          </span>
        </header>

        <div className="windows-command-list">
          {commands.map(
            command => (
              <article
                key={command.id}
                className="windows-command-item"
              >
                <div className="windows-command-item__main">
                  <strong>
                    {command.commandType}
                  </strong>

                  <span
                    className={
                      `windows-command-status windows-command-status--${command.status.toLowerCase()}`
                    }
                  >
                    {getCommandStatusLabel(
                      command.status,
                    )}
                  </span>
                </div>

                <div className="windows-command-item__meta">
                  <span>
                    {formatDate(
                      command.createdAtUtc,
                    )}
                  </span>

                  <span>
                    Intentos:{' '}
                    {command.deliveryAttempts}
                  </span>

                  <span>
                    Transcurrido:{' '}
                    {formatCommandElapsed(
                      command,
                      now,
                    )}
                  </span>

                  {command.errorCode && (
                    <span>
                      {command.errorCode}
                    </span>
                  )}
                </div>

                {(command.resultJson
                  ||
                  command.errorMessage) && (
                  <details className="windows-command-details">
                    <summary>
                      Ver detalle técnico
                    </summary>

                    <pre>
                      {prettyResult(
                        command,
                      )}
                    </pre>
                  </details>
                )}
              </article>
            ),
          )}
        </div>
      </article>
    </section>
  )
}
