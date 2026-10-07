import type {
  WindowsEnrollmentController,
} from '../hooks/useWindowsEnrollment'

import {
  formatDate,
  getEffectiveTokenStatus,
  getStatusLabel,
} from '../utils/enrollmentFormatters'

interface WindowsEnrollmentPanelProps {
  controller:
    WindowsEnrollmentController

  canCreate?: boolean
  canRevoke?: boolean
}

export default function WindowsEnrollmentPanel({
  controller,
  canCreate = true,
  canRevoke = true,
}: WindowsEnrollmentPanelProps) {
  const {
    tokens,
    statistics,

    expirationMinutes,
    setExpirationMinutes,

    maxUses,
    setMaxUses,

    createdToken,

    loading,
    creating,

    downloadingIndividual,
    downloadingGpo,

    copied,

    error,
    success,

    createToken,

    downloadIndividualInstaller,
    downloadGpoInstaller,

    revokeToken,
    copyToken,
  } = controller

  return (
    <div className="enrollment-platform-content">
      {/* =======================================================
          WINDOWS HERO
          ======================================================= */}

      <section className="windows-enrollment-hero">
        <div className="windows-enrollment-title-row">
          <div className="windows-enrollment-logo">
            W
          </div>

          <div>
            <p className="enrollment-eyebrow">
              TitanMDM Windows
            </p>

            <h2>
              Inscripción de dispositivos Windows
            </h2>

            <p>
              Registra equipos Windows mediante
              TitanMDM Windows Agent y administra
              instalaciones individuales o
              despliegues empresariales masivos.
            </p>
          </div>
        </div>

        <span className="windows-enrollment-state">
          Windows Agent
        </span>
      </section>

      {/* =======================================================
          ALERTS
          ======================================================= */}

      {error && (
        <div
          className="
            enrollment-alert
            enrollment-alert-error
          "
          role="alert"
        >
          <strong>
            Windows:
          </strong>{' '}

          {error}
        </div>
      )}

      {success && (
        <div
          className="
            enrollment-alert
            enrollment-alert-success
          "
          role="status"
        >
          {success}
        </div>
      )}

      {/* =======================================================
          STATISTICS
          ======================================================= */}

      <section className="enrollment-stats">
        <article className="enrollment-stat-card">
          <span>
            Inscripciones Windows
          </span>

          <strong>
            {statistics.total}
          </strong>

          <small>
            Credenciales creadas
          </small>
        </article>

        <article className="enrollment-stat-card">
          <span>
            Activas
          </span>

          <strong>
            {statistics.active}
          </strong>

          <small>
            Disponibles
          </small>
        </article>

        <article className="enrollment-stat-card">
          <span>
            Expiradas
          </span>

          <strong>
            {statistics.expired}
          </strong>

          <small>
            Fuera de vigencia
          </small>
        </article>

        <article className="enrollment-stat-card">
          <span>
            Revocadas
          </span>

          <strong>
            {statistics.revoked}
          </strong>

          <small>
            Bloqueadas
          </small>
        </article>
      </section>

      {/* =======================================================
          TOKEN CREATION
          ======================================================= */}

      <div className="enrollment-main-grid">
        <section className="enrollment-panel">
          <div className="enrollment-panel-header">
            <h2>
              Nueva inscripción Windows
            </h2>

            <p>
              Configura la vigencia y cantidad
              máxima de equipos que podrán utilizar
              la credencial.
            </p>
          </div>

          <div className="enrollment-form">
            <div className="enrollment-field">
              <label htmlFor="windowsExpiration">
                Vigencia
              </label>

              <select
                id="windowsExpiration"
                value={
                  expirationMinutes
                }
                disabled={
                  creating
                  ||
                  downloadingIndividual
                  ||
                  downloadingGpo
                  ||
                  !canCreate
                }
                onChange={
                  event =>
                    setExpirationMinutes(
                      Number(
                        event.target.value,
                      ),
                    )
                }
              >
                <option value={15}>
                  15 minutos
                </option>

                <option value={30}>
                  30 minutos
                </option>

                <option value={60}>
                  1 hora
                </option>

                <option value={240}>
                  4 horas
                </option>

                <option value={1440}>
                  24 horas
                </option>

                <option value={10080}>
                  7 días
                </option>
              </select>
            </div>

            <div className="enrollment-field">
              <label htmlFor="windowsMaxUses">
                Usos permitidos
              </label>

              <input
                id="windowsMaxUses"
                type="number"
                min={1}
                max={1000}
                value={
                  maxUses
                }
                disabled={
                  creating
                  ||
                  downloadingIndividual
                  ||
                  downloadingGpo
                  ||
                  !canCreate
                }
                onChange={
                  event =>
                    setMaxUses(
                      Number(
                        event.target.value,
                      ),
                    )
                }
              />

              <small>
                Usa 1 para una inscripción
                individual. Para GPO puedes
                permitir múltiples equipos.
              </small>
            </div>

            <div className="enrollment-platform-info">
              <strong>
                Plataforma: Windows
              </strong>

              <p>
                La credencial es utilizada por
                TitanMDM Windows Agent únicamente
                durante el registro inicial.
              </p>
            </div>

            <button
              className="enrollment-primary-button"
              type="button"
              disabled={
                creating
                ||
                downloadingIndividual
                ||
                downloadingGpo
                ||
                !canCreate
                ||
                maxUses < 1
                ||
                maxUses > 1000
              }
              onClick={
                () =>
                  void createToken()
              }
            >
              {!canCreate
                ? 'Sin permiso para crear'
                : creating
                  ? 'Generando...'
                  : 'Generar token Windows'}
            </button>
          </div>
        </section>

        {/* =====================================================
            CREATED TOKEN
            ===================================================== */}

        <section className="enrollment-panel">
          <div className="enrollment-panel-header">
            <h2>
              Credencial Windows
            </h2>

            <p>
              El secreto solamente se muestra
              durante su creación.
            </p>
          </div>

          {!createdToken ? (
            <div className="enrollment-empty-secret">
              <div className="enrollment-secret-icon">
                🔐
              </div>

              <h3>
                Ningún token nuevo
              </h3>

              <p>
                Genera una credencial manualmente
                o descarga un instalador para crear
                automáticamente una.
              </p>
            </div>
          ) : (
            <div className="enrollment-secret">
              <div className="enrollment-secret-warning">
                Copia esta credencial ahora.
                TitanMDM no volverá a mostrar
                el secreto después de abandonar
                esta vista.
              </div>

              <div className="enrollment-token-value">
                <code>
                  {createdToken.token}
                </code>
              </div>

              <button
                type="button"
                className="enrollment-copy-button"
                onClick={
                  () =>
                    void copyToken()
                }
              >
                {copied
                  ? 'Copiado'
                  : 'Copiar token'}
              </button>

              <dl className="enrollment-secret-details">
                <div>
                  <dt>
                    Plataforma
                  </dt>

                  <dd>
                    Windows
                  </dd>
                </div>

                <div>
                  <dt>
                    Expira
                  </dt>

                  <dd>
                    {formatDate(
                      createdToken.expiresAtUtc,
                    )}
                  </dd>
                </div>

                <div>
                  <dt>
                    Usos
                  </dt>

                  <dd>
                    {createdToken.usedCount}
                    /
                    {createdToken.maxUses}
                  </dd>
                </div>
              </dl>
            </div>
          )}
        </section>
      </div>

      {/* =======================================================
          WINDOWS AGENT DISTRIBUTION
          ======================================================= */}

      <section className="enrollment-panel">
        <div className="enrollment-panel-header">
          <h2>
            Instalar TitanMDM Windows Agent
          </h2>

          <p>
            Descarga un instalador preparado
            automáticamente para conectar el
            equipo con este servidor TitanMDM.
          </p>
        </div>

        <div className="windows-deployment-grid">
          {/* ===================================================
              INDIVIDUAL
              =================================================== */}

          <article className="windows-deployment-card">
            <div className="windows-deployment-card__header">
              <div className="windows-deployment-icon">
                ↓
              </div>

              <div>
                <span className="windows-deployment-eyebrow">
                  Instalación individual
                </span>

                <h3>
                  Windows Agent
                </h3>
              </div>
            </div>

            <p>
              Para instalar TitanMDM manualmente
              en una computadora Windows.
              El instalador incluirá una credencial
              temporal válida para un solo equipo.
            </p>

            <div className="windows-deployment-features">
              <span>
                ✓ Configuración automática del servidor
              </span>

              <span>
                ✓ Credencial de un solo uso
              </span>

              <span>
                ✓ Windows Service automático
              </span>

              <span>
                ✓ RemoteHost incluido
              </span>

              <span>
                ✓ Validación SHA-256
              </span>
            </div>

            <button
              type="button"
              className="enrollment-primary-button"
              disabled={
                !canCreate
                ||
                downloadingIndividual
                ||
                downloadingGpo
              }
              onClick={
                () =>
                  void downloadIndividualInstaller()
              }
            >
              {!canCreate
                ? 'Sin permiso para descargar'
                : downloadingIndividual
                  ? 'Generando instalador...'
                  : 'Descargar instalador Windows'}
            </button>

            <small className="windows-deployment-note">
              Ejecutar el archivo descargado
              desde PowerShell como administrador.
            </small>
          </article>

          {/* ===================================================
              GPO
              =================================================== */}

          <article className="windows-deployment-card">
            <div className="windows-deployment-card__header">
              <div className="windows-deployment-icon">
                G
              </div>

              <div>
                <span className="windows-deployment-eyebrow">
                  Despliegue empresarial
                </span>

                <h3>
                  Active Directory / GPO
                </h3>
              </div>
            </div>

            <p>
              Para desplegar TitanMDM en múltiples
              equipos mediante una política de grupo
              sin intervención de los usuarios.
            </p>

            <div className="windows-deployment-features">
              <span>
                ✓ Computer Startup Script
              </span>

              <span>
                ✓ Instalación silenciosa
              </span>

              <span>
                ✓ Ejecuta como SYSTEM
              </span>

              <span>
                ✓ Evita reinstalar equipos enrolados
              </span>

              <span>
                ✓ Hasta {maxUses} equipos
              </span>
            </div>

            <button
              type="button"
              className="
                enrollment-primary-button
                windows-gpo-button
              "
              disabled={
                !canCreate
                ||
                downloadingIndividual
                ||
                downloadingGpo
                ||
                maxUses < 1
                ||
                maxUses > 1000
              }
              onClick={
                () =>
                  void downloadGpoInstaller()
              }
            >
              {!canCreate
                ? 'Sin permiso para descargar'
                : downloadingGpo
                  ? 'Generando script GPO...'
                  : `Descargar script GPO · ${maxUses} equipos`}
            </button>

            <small className="windows-deployment-note">
              Usa la vigencia y cantidad de
              usos configuradas en la sección
              superior.
            </small>
          </article>
        </div>
      </section>

      {/* =======================================================
          DEPLOYMENT INSTRUCTIONS
          ======================================================= */}

      <section className="enrollment-panel">
        <div className="enrollment-panel-header">
          <h2>
            Métodos de instalación
          </h2>

          <p>
            TitanMDM admite instalación individual
            y despliegue masivo desde Active Directory.
          </p>
        </div>

        <div className="windows-installation-steps">
          <article>
            <span className="windows-installation-step">
              1
            </span>

            <div>
              <strong>
                Instalación individual
              </strong>

              <p>
                Descarga el instalador desde esta
                pantalla en el equipo que deseas
                administrar.
              </p>
            </div>
          </article>

          <article>
            <span className="windows-installation-step">
              2
            </span>

            <div>
              <strong>
                Ejecutar como administrador
              </strong>

              <p>
                Inicia PowerShell con privilegios
                administrativos y ejecuta el
                archivo descargado.
              </p>
            </div>
          </article>

          <article>
            <span className="windows-installation-step">
              3
            </span>

            <div>
              <strong>
                Enrolamiento automático
              </strong>

              <p>
                El agente descargará los componentes,
                instalará el servicio y aparecerá
                automáticamente en TitanMDM.
              </p>
            </div>
          </article>

          <article>
            <span className="windows-installation-step">
              GPO
            </span>

            <div>
              <strong>
                Instalación masiva
              </strong>

              <p>
                Descarga el script GPO y configúralo
                como Computer Startup Script para
                desplegar TitanMDM en múltiples PCs.
              </p>
            </div>
          </article>
        </div>
      </section>

      {/* =======================================================
          HISTORY
          ======================================================= */}

      <section className="enrollment-panel">
        <div className="enrollment-panel-header">
          <h2>
            Historial Windows
          </h2>

          <p>
            Credenciales Windows asociadas a
            esta organización.
          </p>
        </div>

        {loading ? (
          <div className="enrollment-loading">
            Cargando credenciales...
          </div>
        ) : tokens.length === 0 ? (
          <div className="enrollment-empty-table">
            Todavía no existen credenciales
            Windows.
          </div>
        ) : (
          <div className="enrollment-table-wrapper">
            <table className="enrollment-table">
              <thead>
                <tr>
                  <th>
                    Estado
                  </th>

                  <th>
                    Uso
                  </th>

                  <th>
                    Creado
                  </th>

                  <th>
                    Expira
                  </th>

                  <th>
                    Último uso
                  </th>

                  <th>
                    Acciones
                  </th>
                </tr>
              </thead>

              <tbody>
                {tokens.map(
                  token => {
                    const effectiveStatus =
                      getEffectiveTokenStatus(
                        token.status,
                        token.expiresAtUtc,
                      )

                    return (
                      <tr
                        key={
                          token.id
                        }
                      >
                        <td>
                          <span
                            className={
                              `enrollment-status ` +
                              `enrollment-status-${effectiveStatus.toLowerCase()}`
                            }
                          >
                            {getStatusLabel(
                              effectiveStatus,
                            )}
                          </span>
                        </td>

                        <td>
                          {token.usedCount}
                          /
                          {token.maxUses}
                        </td>

                        <td>
                          {formatDate(
                            token.createdAtUtc,
                          )}
                        </td>

                        <td>
                          {formatDate(
                            token.expiresAtUtc,
                          )}
                        </td>

                        <td>
                          {token.lastUsedAtUtc
                            ? formatDate(
                                token.lastUsedAtUtc,
                              )
                            : 'Sin uso'}
                        </td>

                        <td>
                          <button
                            type="button"
                            className="enrollment-action-button"
                            disabled={
                              effectiveStatus !==
                                'Active'
                              ||
                              !canRevoke
                            }
                            onClick={
                              () =>
                                void revokeToken(
                                  token,
                                )
                            }
                          >
                            {!canRevoke
                              ? 'Sin permiso'
                              : 'Revocar'}
                          </button>
                        </td>
                      </tr>
                    )
                  },
                )}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}