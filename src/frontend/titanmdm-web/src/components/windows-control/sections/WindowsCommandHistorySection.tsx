import {
  SquareTerminal,
} from 'lucide-react'

import type {
  DeviceCommand,
} from '../../../api/deviceCommandsApi'

import {
  formatDate,
  prettyResult,
} from '../windowsControl.utils'

interface Props {
  commands:
    DeviceCommand[]
}

export function WindowsCommandHistorySection({
  commands,
}: Props) {
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
                    {command.status}
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