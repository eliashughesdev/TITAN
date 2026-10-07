import {
  Eye,
  Hand,
  Keyboard,
  MousePointer2,
  ShieldCheck,
  UserRoundCheck,
} from 'lucide-react'

import type {
  RemoteControlState,
  RemoteSession,
} from '../../../api/remoteSupportApi'

interface Props {
  session:
    RemoteSession | null

  control:
    RemoteControlState | null

  remoteHostConnected:
    boolean

  acquiring:
    boolean

  onAcquire:
    () => Promise<void>

  onRelease:
    () => Promise<void>
}

export function RemoteControlToolbar({
  session,
  control,
  remoteHostConnected,
  acquiring,
  onAcquire,
  onRelease,
}: Props) {
  const controlledByOther =
    Boolean(
      control?.hasController
      &&
      !control.ownedByCurrentUser,
    )

  return (
    <section className="remote-control-toolbar">
      <div className="remote-toolbar-state">
        <span>
          <ShieldCheck
            size={16}
          />

          {remoteHostConnected
            ? 'RemoteHost conectado'
            : 'RemoteHost desconectado'}
        </span>

        <span>
          <MousePointer2
            size={16}
          />

          {session?.allowMouse
            ? 'Mouse permitido'
            : 'Mouse bloqueado'}
        </span>

        <span>
          <Keyboard
            size={16}
          />

          {session?.allowKeyboard
            ? 'Teclado permitido'
            : 'Teclado bloqueado'}
        </span>
      </div>

      <div className="remote-toolbar-control">
        {!control?.hasController && (
          <>
            <Eye size={16} />

            <span>
              Modo observador
            </span>

            <button
              type="button"
              disabled={
                !session
                ||
                acquiring
              }
              onClick={() =>
                void onAcquire()
              }
            >
              <Hand size={16} />

              Solicitar control
            </button>
          </>
        )}

        {control?.ownedByCurrentUser && (
          <>
            <UserRoundCheck
              size={16}
            />

            <span>
              Tienes el control
            </span>

            <button
              type="button"
              onClick={() =>
                void onRelease()
              }
            >
              Liberar control
            </button>
          </>
        )}

        {controlledByOther && (
          <>
            <Eye size={16} />

            <span>
              Controlado por{' '}
              <strong>
                {control?.displayName}
              </strong>
            </span>

            <button
              type="button"
              disabled
            >
              Solo lectura
            </button>
          </>
        )}
      </div>
    </section>
  )
}