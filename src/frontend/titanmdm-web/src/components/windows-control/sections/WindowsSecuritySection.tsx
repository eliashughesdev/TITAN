import {
  HardDrive,
  KeyRound,
  RadioTower,
  RefreshCw,
  Shield,
  ShieldCheck,
} from 'lucide-react'

import type {
  SecurityView,
  SendWindowsCommand,
} from '../windowsControl.types'

import {
  WindowsTelemetryCard,
} from './WindowsTelemetryCard'

interface Props {
  security:
    SecurityView

  sendCommand:
    SendWindowsCommand
}

export function WindowsSecuritySection({
  security,
  sendCommand,
}: Props) {
  return (
    <section className="windows-control-single">
      <article className="windows-control-card windows-control-card--wide">
        <header>
          <ShieldCheck size={18} />

          <h2>
            Windows Security Posture
          </h2>

          <button
            type="button"
            onClick={() =>
              void sendCommand(
                'SECURITY_STATUS',
              )
            }
          >
            Consultar ahora
          </button>
        </header>

        {!security.available ? (
          <div className="windows-telemetry-empty">
            Ejecuta SECURITY_STATUS para obtener la postura de seguridad.
          </div>
        ) : (
          <div className="windows-telemetry-grid">
            <WindowsTelemetryCard
              title="Microsoft Defender"
              value={
                security.defenderRealTime === true
                  ? 'Protegido'
                  : security.defenderAvailable
                    ? 'Revisar'
                    : 'No disponible'
              }
              detail={
                security.defenderVersion
                  ? `Firmas ${security.defenderVersion}`
                  : 'Protección en tiempo real'
              }
              icon={
                <ShieldCheck size={18} />
              }
              tone={
                security.defenderRealTime === true
                  ? 'success'
                  : 'warning'
              }
            />

            <WindowsTelemetryCard
              title="Firewall"
              value={
                security.firewallEnabledProfiles !== null
                  ? `${security.firewallEnabledProfiles} perfiles`
                  : 'N/D'
              }
              detail="Perfiles de Windows Firewall"
              icon={<Shield size={18} />}
              tone={
                (
                  security.firewallEnabledProfiles ??
                  0
                ) > 0
                  ? 'success'
                  : 'warning'
              }
            />

            <WindowsTelemetryCard
              title="BitLocker"
              value={
                security.bitLockerProtected === true
                  ? 'Protegido'
                  : security.bitLockerAvailable
                    ? 'Revisar'
                    : 'N/D'
              }
              detail="Protección de volúmenes"
              icon={<HardDrive size={18} />}
              tone={
                security.bitLockerProtected === true
                  ? 'success'
                  : 'warning'
              }
            />

            <WindowsTelemetryCard
              title="TPM"
              value={
                security.tpmPresent === true
                  ? security.tpmReady === true
                    ? 'Listo'
                    : 'Presente'
                  : 'No disponible'
              }
              detail="Trusted Platform Module"
              icon={<KeyRound size={18} />}
              tone={
                security.tpmReady === true
                  ? 'success'
                  : 'warning'
              }
            />

            <WindowsTelemetryCard
              title="Secure Boot"
              value={
                security.secureBoot === true
                  ? 'Activo'
                  : security.secureBoot === false
                    ? 'Inactivo'
                    : 'N/D'
              }
              detail="Arranque seguro UEFI"
              icon={<ShieldCheck size={18} />}
              tone={
                security.secureBoot === true
                  ? 'success'
                  : 'warning'
              }
            />

            <WindowsTelemetryCard
              title="UAC"
              value={
                security.uacEnabled === true
                  ? 'Activo'
                  : 'Inactivo'
              }
              detail="User Account Control"
              icon={<Shield size={18} />}
              tone={
                security.uacEnabled === true
                  ? 'success'
                  : 'warning'
              }
            />

            <WindowsTelemetryCard
              title="Reinicio pendiente"
              value={
                security.pendingReboot === true
                  ? 'Sí'
                  : 'No'
              }
              detail="Windows requiere reinicio"
              icon={<RefreshCw size={18} />}
              tone={
                security.pendingReboot === true
                  ? 'warning'
                  : 'success'
              }
            />

            <WindowsTelemetryCard
              title="Remote Desktop"
              value={
                security.remoteDesktopEnabled === true
                  ? 'Habilitado'
                  : 'Deshabilitado'
              }
              detail="Configuración RDP"
              icon={<RadioTower size={18} />}
              tone={
                security.remoteDesktopEnabled === true
                  ? 'warning'
                  : 'success'
              }
            />
          </div>
        )}
      </article>
    </section>
  )
}