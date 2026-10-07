import {
  Eye,
  MousePointer2,
  Users,
} from 'lucide-react'

import type {
  RemoteSessionParticipant,
} from '../../../api/remoteSupportApi'

interface Props {
  participants:
    RemoteSessionParticipant[]
}

export function RemoteParticipantsPanel({
  participants,
}: Props) {
  const connected =
    participants.filter(
      participant =>
        participant.isConnected,
    )

  return (
    <section className="remote-participants">
      <header>
        <div>
          <Users size={17} />

          <strong>
            Técnicos conectados
          </strong>
        </div>

        <span>
          {connected.length}
        </span>
      </header>

      {connected.length === 0 ? (
        <div className="remote-empty">
          Ningún técnico conectado.
        </div>
      ) : (
        connected.map(
          participant => (
            <div
              className="remote-participant"
              key={participant.id}
            >
              <div>
                <strong>
                  {participant.displayName}
                </strong>

                <span>
                  {participant.canControl
                    ? 'Controller'
                    : 'Viewer'}
                </span>
              </div>

              {participant.canControl
                ? (
                  <MousePointer2
                    size={15}
                  />
                )
                : (
                  <Eye
                    size={15}
                  />
                )}
            </div>
          ),
        )
      )}
    </section>
  )
}