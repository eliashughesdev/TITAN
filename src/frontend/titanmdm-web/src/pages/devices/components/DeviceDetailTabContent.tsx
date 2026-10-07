import {
  CheckCircle2,
  Database,
  HardDrive,
  Info,
  Network,
  RefreshCw,
  ShieldCheck,
  Smartphone,
  User,
} from 'lucide-react'

import type {
  AndroidDeviceDetails,
  DeviceDetails,
} from '../../../types/device'

import {
  formatDeviceDate,
  getBatteryText,
  getYesNo,
} from '../utils/deviceDetail.utils'

import type {
  DeviceDetailTab,
} from './DeviceDetailTabs'

interface DetailFieldProps {
  label: string

  value:
    | string
    | number
    | null
    | undefined
}

function DetailField({
  label,
  value,
}: DetailFieldProps) {
  const displayValue =
    value === null
    ||
    value === undefined
    ||
    value === ''
      ? 'N/D'
      : String(value)

  return (
    <div>
      <span>
        {label}
      </span>

      <strong>
        {displayValue}
      </strong>
    </div>
  )
}

interface Props {
  tab:
    DeviceDetailTab

  device:
    DeviceDetails

  androidDetails:
    AndroidDeviceDetails | null
}

export function DeviceDetailTabContent({
  tab,
  device,
  androidDetails,
}: Props) {
  const isAndroid =
    device.platform ===
    'Android'

  if (
    tab ===
    'overview'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card">
          <header>
            <Info
              size={18}
            />

            <h2>
              Información general
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Nombre"
              value={
                device.deviceName
              }
            />

            <DetailField
              label="Plataforma"
              value={
                device.platform
              }
            />

            <DetailField
              label="Sistema operativo"
              value={
                device.operatingSystem
              }
            />

            <DetailField
              label="Versión del SO"
              value={
                device
                  .operatingSystemVersion
              }
            />

            <DetailField
              label="Versión del agente"
              value={
                device.agentVersion
              }
            />

            <DetailField
              label="Administrado"
              value={
                getYesNo(
                  device.isManaged,
                )
              }
            />
          </div>
        </article>

        <article className="device-detail-card">
          <header>
            <User
              size={18}
            />

            <h2>
              Asignación
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Usuario"
              value={
                device.assignedUser
                ??
                'Sin asignar'
              }
            />

            <DetailField
              label="Departamento"
              value={
                device.department
                ??
                'Sin departamento'
              }
            />

            <DetailField
              label="Inscrito"
              value={
                formatDeviceDate(
                  device
                    .enrolledAtUtc,
                )
              }
            />

            <DetailField
              label="Actualizado"
              value={
                formatDeviceDate(
                  device
                    .updatedAtUtc,
                )
              }
            />

            {isAndroid
              &&
              androidDetails && (
              <>
                <DetailField
                  label="Modo de administración"
                  value={
                    androidDetails
                      .managementMode
                  }
                />

                <DetailField
                  label="Propiedad"
                  value={
                    androidDetails
                      .ownership
                  }
                />
              </>
            )}
          </div>
        </article>
      </section>
    )
  }

  if (
    tab ===
    'hardware'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card device-detail-card--wide">
          <header>
            <HardDrive
              size={18}
            />

            <h2>
              Identidad del hardware
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Fabricante"
              value={
                device.manufacturer
              }
            />

            <DetailField
              label="Modelo"
              value={
                device.model
              }
            />

            <DetailField
              label="Número de serie"
              value={
                device.serialNumber
              }
            />

            <DetailField
              label="IMEI"
              value={
                device.imei
              }
            />

            <DetailField
              label="Batería"
              value={
                getBatteryText(
                  device.batteryLevel,
                )
              }
            />

            {isAndroid
              &&
              androidDetails && (
              <>
                <DetailField
                  label="Marca"
                  value={
                    androidDetails
                      .brand
                  }
                />

                <DetailField
                  label="Hardware"
                  value={
                    androidDetails
                      .hardware
                  }
                />

                <DetailField
                  label="Baseband"
                  value={
                    androidDetails
                      .deviceBasebandVersion
                  }
                />

                <DetailField
                  label="Bootloader"
                  value={
                    androidDetails
                      .bootloaderVersion
                  }
                />
              </>
            )}
          </div>
        </article>
      </section>
    )
  }

  if (
    tab ===
    'network'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card device-detail-card--wide">
          <header>
            <Network
              size={18}
            />

            <h2>
              Información de red
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Dirección IP"
              value={
                device.ipAddress
              }
            />

            <DetailField
              label="Dirección MAC"
              value={
                device.macAddress
              }
            />
          </div>
        </article>
      </section>
    )
  }

  if (
    !isAndroid
    ||
    !androidDetails
  ) {
    return null
  }

  if (
    tab ===
    'enterprise'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card device-detail-card--wide">
          <header>
            <Smartphone
              size={18}
            />

            <h2>
              Android Enterprise
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Google Device ID"
              value={
                androidDetails
                  .googleDeviceId
              }
            />

            <DetailField
              label="Recurso de Google"
              value={
                androidDetails
                  .googleDeviceName
              }
            />

            <DetailField
              label="Modo de administración"
              value={
                androidDetails
                  .managementMode
              }
            />

            <DetailField
              label="Propiedad"
              value={
                androidDetails
                  .ownership
              }
            />

            <DetailField
              label="Estado AMAPI"
              value={
                androidDetails.state
              }
            />

            <DetailField
              label="Usuario"
              value={
                androidDetails
                  .userName
              }
            />

            <DetailField
              label="Enrollment Token"
              value={
                androidDetails
                  .enrollmentTokenName
              }
            />

            <DetailField
              label="Fecha de inscripción"
              value={
                formatDeviceDate(
                  androidDetails
                    .enrollmentTimeUtc,
                )
              }
            />
          </div>
        </article>
      </section>
    )
  }

  if (
    tab ===
    'system'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card device-detail-card--wide">
          <header>
            <Database
              size={18}
            />

            <h2>
              Sistema Android
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Versión Android"
              value={
                device
                  .operatingSystemVersion
              }
            />

            <DetailField
              label="API Level"
              value={
                androidDetails
                  .apiLevel
              }
            />

            <DetailField
              label="Build"
              value={
                androidDetails
                  .buildNumber
              }
            />

            <DetailField
              label="Kernel"
              value={
                androidDetails
                  .kernelVersion
              }
            />

            <DetailField
              label="Security Patch"
              value={
                androidDetails
                  .securityPatchLevel
              }
            />

            <DetailField
              label="Android Device Policy"
              value={
                androidDetails
                  .androidDevicePolicyVersion
              }
            />

            <DetailField
              label="ADP Version Code"
              value={
                androidDetails
                  .androidDevicePolicyVersionCode
              }
            />
          </div>
        </article>
      </section>
    )
  }

  if (
    tab ===
    'security'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card device-detail-card--wide">
          <header>
            <ShieldCheck
              size={18}
            />

            <h2>
              Seguridad Android
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Security Posture"
              value={
                androidDetails
                  .securityPosture
              }
            />

            <DetailField
              label="Cifrado"
              value={
                androidDetails
                  .encryptionStatus
              }
            />

            <DetailField
              label="Cumplimiento"
              value={
                device
                  .complianceStatus
              }
            />

            <DetailField
              label="Administrado"
              value={
                getYesNo(
                  device.isManaged,
                )
              }
            />

            <DetailField
              label="Security Patch"
              value={
                androidDetails
                  .securityPatchLevel
              }
            />

            <DetailField
              label="Estado AMAPI"
              value={
                androidDetails.state
              }
            />
          </div>
        </article>
      </section>
    )
  }

  if (
    tab ===
    'policy'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card device-detail-card--wide">
          <header>
            <CheckCircle2
              size={18}
            />

            <h2>
              Política Android
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Política aplicada"
              value={
                androidDetails
                  .appliedPolicyName
              }
            />

            <DetailField
              label="Versión"
              value={
                androidDetails
                  .appliedPolicyVersion
              }
            />

            <DetailField
              label="Estado de aplicación"
              value={
                androidDetails
                  .appliedPolicyState
              }
            />

            <DetailField
              label="Última sincronización de política"
              value={
                formatDeviceDate(
                  androidDetails
                    .lastPolicySyncTimeUtc,
                )
              }
            />
          </div>
        </article>
      </section>
    )
  }

  if (
    tab ===
    'sync'
  ) {
    return (
      <section className="device-detail-grid">
        <article className="device-detail-card device-detail-card--wide">
          <header>
            <RefreshCw
              size={18}
            />

            <h2>
              Sincronización
            </h2>
          </header>

          <div className="device-detail-fields">
            <DetailField
              label="Último reporte de estado"
              value={
                formatDeviceDate(
                  androidDetails
                    .lastStatusReportTimeUtc,
                )
              }
            />

            <DetailField
              label="Última sincronización TitanMDM"
              value={
                formatDeviceDate(
                  androidDetails
                    .lastSynchronizedAtUtc,
                )
              }
            />

            <DetailField
              label="Última sincronización de política"
              value={
                formatDeviceDate(
                  androidDetails
                    .lastPolicySyncTimeUtc,
                )
              }
            />

            <DetailField
              label="Eliminado en Google"
              value={
                getYesNo(
                  androidDetails
                    .isDeletedInGoogle,
                )
              }
            />

            <DetailField
              label="Fecha eliminación Google"
              value={
                formatDeviceDate(
                  androidDetails
                    .deletedInGoogleAtUtc,
                )
              }
            />
          </div>
        </article>
      </section>
    )
  }

  return null
}