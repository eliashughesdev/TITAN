import {
  AppWindow,
  CheckCircle2,
  Clock3,
  Cpu,
  HardDrive,
  KeyRound,
  MonitorCheck,
  RefreshCcw,
  ScanLine,
  Shield,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  Terminal,
  Usb,
  Wifi,
  Wrench,
} from 'lucide-react'

import type {
  DashboardSummary,
  DashboardWorkspace,
} from '../../api/dashboardApi'

import './WorkspaceInsights.css'

interface WorkspaceInsightsProps {
  workspace:
    DashboardWorkspace

  summary:
    DashboardSummary
}

interface InsightCardProps {
  label: string
  value:
    string | number
  detail: string
  icon:
    React.ReactNode
  tone?:
    'default'
    | 'success'
    | 'warning'
    | 'danger'
}

function InsightCard({
  label,
  value,
  detail,
  icon,
  tone = 'default',
}: InsightCardProps) {
  return (
    <article
      className={
        `workspace-insight-card workspace-insight-card--${tone}`
      }
    >
      <div className="workspace-insight-card__icon">
        {icon}
      </div>

      <div>
        <span>
          {label}
        </span>

        <strong>
          {value}
        </strong>

        <small>
          {detail}
        </small>
      </div>
    </article>
  )
}

export function WorkspaceInsights({
  workspace,
  summary,
}: WorkspaceInsightsProps) {
  /*
   * ==============================================================
   * WINDOWS
   * ==============================================================
   */

  if (
    workspace ===
      'windows'
    &&
    summary.windows
  ) {
    const windows =
      summary.windows

    return (
      <section className="workspace-insights">
        <header className="workspace-insights__header">
          <div>
            <span>
              WINDOWS OPERATIONS
            </span>

            <h2>
              Estado avanzado de Windows
            </h2>

            <p>
              Seguridad, Windows Update, telemetría y soporte remoto.
            </p>
          </div>

          <MonitorCheck
            size={22}
          />
        </header>

        <div className="workspace-insights-grid">
          <InsightCard
            label="Check-ins 24h"
            value={
              windows.checkInsLast24Hours
            }
            detail="Equipos con actividad reciente"
            icon={
              <Clock3
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="Reinicio pendiente"
            value={
              windows.pendingReboot
            }
            detail="Reportado por telemetría Windows"
            icon={
              <RefreshCcw
                size={19}
              />
            }
            tone={
              windows.pendingReboot >
              0
                ? 'warning'
                : 'success'
            }
          />

          <InsightCard
            label="Windows Update"
            value={
              windows.updateTelemetryDevices
            }
            detail="Equipos con status disponible"
            icon={
              <Wrench
                size={19}
              />
            }
          />

          <InsightCard
            label="Update Service"
            value={
              windows.updateServiceRunning
            }
            detail="Servicio reportado Running"
            icon={
              <Cpu
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="Defender"
            value={
              windows.defenderAvailable
            }
            detail="Equipos con telemetría disponible"
            icon={
              <ShieldCheck
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="Firewall"
            value={
              windows.firewallAvailable
            }
            detail="Equipos con consulta disponible"
            icon={
              <Shield
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="BitLocker"
            value={
              windows.bitLockerAvailable
            }
            detail="Equipos con información BitLocker"
            icon={
              <HardDrive
                size={19}
              />
            }
          />

          <InsightCard
            label="TPM"
            value={
              windows.tpmAvailable
            }
            detail="Equipos con TPM consultable"
            icon={
              <KeyRound
                size={19}
              />
            }
          />

          <InsightCard
            label="Secure Boot"
            value={
              windows.secureBootEnabled
            }
            detail="Equipos que reportan Secure Boot"
            icon={
              <ShieldCheck
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="Sesiones remotas"
            value={
              windows.remoteSessionsTotal
            }
            detail={`${windows.remoteSessionsLast24Hours} en las últimas 24h`}
            icon={
              <Terminal
                size={19}
              />
            }
          />

          <InsightCard
            label="Remotas activas"
            value={
              windows.remoteSessionsActive
            }
            detail="Solicitadas, conectando o conectadas"
            icon={
              <Wifi
                size={19}
              />
            }
            tone={
              windows.remoteSessionsActive >
              0
                ? 'success'
                : 'default'
            }
          />

          <InsightCard
            label="Remotas fallidas"
            value={
              windows.remoteSessionsFailed
            }
            detail={`${windows.remoteSessionsCompleted} completadas`}
            icon={
              <ShieldAlert
                size={19}
              />
            }
            tone={
              windows.remoteSessionsFailed >
              0
                ? 'danger'
                : 'success'
            }
          />
        </div>
      </section>
    )
  }

  /*
   * ==============================================================
   * ANDROID
   * ==============================================================
   */

  if (
    workspace ===
      'android'
    &&
    summary.android
  ) {
    const android =
      summary.android

    const lastSync =
      android.lastSynchronizationUtc
        ? new Date(
            android.lastSynchronizationUtc,
          ).toLocaleString()
        : 'Nunca'

    return (
      <section className="workspace-insights">
        <header className="workspace-insights__header">
          <div>
            <span>
              ANDROID ENTERPRISE
            </span>

            <h2>
              Estado avanzado de Android
            </h2>

            <p>
              Management modes, políticas, aplicaciones y seguridad.
            </p>
          </div>

          <Smartphone
            size={22}
          />
        </header>

        <div className="workspace-insights-grid">
          <InsightCard
            label="Administrados"
            value={
              android.managed
            }
            detail={`${android.missingInGoogle} ausentes en Google`}
            icon={
              <Smartphone
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="Fully Managed"
            value={
              android.fullyManaged
            }
            detail="Dispositivos corporativos"
            icon={
              <MonitorCheck
                size={19}
              />
            }
          />

          <InsightCard
            label="Dedicated / Kiosk"
            value={
              android.dedicated
            }
            detail="Dispositivos dedicados"
            icon={
              <ScanLine
                size={19}
              />
            }
          />

          <InsightCard
            label="Work Profile"
            value={
              android.workProfile
            }
            detail="Perfil de trabajo"
            icon={
              <AppWindow
                size={19}
              />
            }
          />

          <InsightCard
            label="Enrollments activos"
            value={
              android.activeEnrollments
            }
            detail={`${android.expiredEnrollments} expirados · ${android.revokedEnrollments} revocados`}
            icon={
              <CheckCircle2
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="Políticas aplicadas"
            value={
              android.policyApplied
            }
            detail={`${android.policyPendingOrUnknown} pendientes o desconocidas`}
            icon={
              <ShieldCheck
                size={19}
              />
            }
            tone={
              android.policyPendingOrUnknown >
              0
                ? 'warning'
                : 'success'
            }
          />

          <InsightCard
            label="Aplicaciones"
            value={
              android.applicationsPresent
            }
            detail="Aplicaciones presentes inventariadas"
            icon={
              <AppWindow
                size={19}
              />
            }
          />

          <InsightCard
            label="Security telemetry"
            value={
              android.securityTelemetryDevices
            }
            detail="Dispositivos con postura recibida"
            icon={
              <Shield
                size={19}
              />
            }
          />

          <InsightCard
            label="Device Secure"
            value={
              android.deviceSecure
            }
            detail={`${android.encrypted} cifrados`}
            icon={
              <ShieldCheck
                size={19}
              />
            }
            tone="success"
          />

          <InsightCard
            label="Root detectado"
            value={
              android.rootDetected
            }
            detail="Dispositivos con señal de root"
            icon={
              <ShieldAlert
                size={19}
              />
            }
            tone={
              android.rootDetected >
              0
                ? 'danger'
                : 'success'
            }
          />

          <InsightCard
            label="ADB habilitado"
            value={
              android.adbEnabled
            }
            detail="Dispositivos con depuración activa"
            icon={
              <Usb
                size={19}
              />
            }
            tone={
              android.adbEnabled >
              0
                ? 'warning'
                : 'success'
            }
          />

          <InsightCard
            label="Última sincronización"
            value={
              android.securityPostureReported
            }
            detail={lastSync}
            icon={
              <Clock3
                size={19}
              />
            }
          />
        </div>
      </section>
    )
  }

  return null
}