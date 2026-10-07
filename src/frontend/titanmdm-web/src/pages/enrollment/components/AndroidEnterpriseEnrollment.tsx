import type {
  AndroidEnrollmentMode,
} from '../../../types/androidEnterprise'

import type {
  AndroidEnterpriseEnrollmentController,
} from '../hooks/useAndroidEnterpriseEnrollment'

import {
  formatDate,
  getAndroidModeDescription,
  getAndroidModeLabel,
  getStatusLabel,
} from '../utils/enrollmentFormatters'

interface AndroidEnterpriseEnrollmentProps {
  controller:
    AndroidEnterpriseEnrollmentController

  canCreate?: boolean
  canRevoke?: boolean
  canConnect?: boolean
}

const modes: AndroidEnrollmentMode[] = [
  'FullyManaged',
  'Dedicated',
  'WorkProfile',
]

export default function AndroidEnterpriseEnrollment({
  controller,
  canCreate = true,
  canRevoke = true,
  canConnect = true,
}: AndroidEnterpriseEnrollmentProps) {
  const {
    status,
    enrollments,

    mode,
    setMode,

    expirationMinutes,
    setExpirationMinutes,

    createdEnrollment,
    qrDataUrl,

    loading,
    creating,
    connecting,
    copied,

    error,
    success,

    active,
    readyForSignup,
    statistics,

    connect,
    createEnrollment,
    revokeEnrollment,
    copyToken,
  } = controller

  return (
    <div className="android-enterprise-section">
      <section className="android-enterprise-hero">
        <div>
          <div className="android-enterprise-title-row">
            <div className="android-enterprise-logo">
              A
            </div>

            <div>
              <p className="enrollment-eyebrow">
                Google Android Enterprise
              </p>

              <h2>
                Administración Android
              </h2>

              <p>
                Inscribe y administra teléfonos,
                tabletas y dispositivos dedicados
                mediante Android Management API.
              </p>
            </div>
          </div>
        </div>

        <span
          className={
            active
              ? 'android-enterprise-state android-enterprise-state-active'
              : 'android-enterprise-state android-enterprise-state-inactive'
          }
        >
          {loading
            ? 'Consultando...'
            : active
              ? 'Enterprise conectado'
              : getStatusLabel(
                  status?.status ??
                    'NotConfigured',
                )}
        </span>
      </section>

      {error && (
        <div
          className="enrollment-alert enrollment-alert-error"
          role="alert"
        >
          <strong>
            Android Enterprise:
          </strong>{' '}
          {error}
        </div>
      )}

      {success && (
        <div
          className="enrollment-alert enrollment-alert-success"
          role="status"
        >
          {success}
        </div>
      )}

      <section className="android-readiness-grid">
        <article className="android-readiness-card">
          <span
            className={
              status?.isConfigured
                ? 'android-indicator android-indicator-ok'
                : 'android-indicator android-indicator-error'
            }
          />

          <div>
            <strong>
              Proyecto Google
            </strong>

            <small>
              {status?.googleProjectId ||
                'No configurado'}
            </small>
          </div>
        </article>

        <article className="android-readiness-card">
          <span
            className={
              status?.canAuthenticate
                ? 'android-indicator android-indicator-ok'
                : 'android-indicator android-indicator-error'
            }
          />

          <div>
            <strong>
              Autenticación ADC
            </strong>

            <small>
              {status?.canAuthenticate
                ? 'Autenticación disponible'
                : 'Sin autenticación'}
            </small>
          </div>
        </article>

        <article className="android-readiness-card">
          <span
            className={
              status?.hasPublicCallback
                ? 'android-indicator android-indicator-ok'
                : 'android-indicator android-indicator-warning'
            }
          />

          <div>
            <strong>
              Callback público
            </strong>

            <small>
              {status?.hasPublicCallback
                ? 'Disponible'
                : 'Pendiente'}
            </small>
          </div>
        </article>

        <article className="android-readiness-card">
          <span
            className={
              active
                ? 'android-indicator android-indicator-ok'
                : 'android-indicator android-indicator-warning'
            }
          />

          <div>
            <strong>
              Enterprise
            </strong>

            <small>
              {status?.enterpriseDisplayName ||
                status?.enterpriseName ||
                'No conectado'}
            </small>
          </div>
        </article>
      </section>

      {!active && (
        <section className="enrollment-panel android-connect-panel">
          <div className="android-connect-content">
            <div>
              <h2>
                Conectar Android Enterprise
              </h2>

              <p>
                Vincula TitanMDM con Android
                Enterprise para habilitar
                aprovisionamiento, políticas,
                aplicaciones administradas y
                dispositivos dedicados.
              </p>

              {!status?.hasPublicCallback && (
                <div className="android-callback-warning">
                  El callback público todavía
                  no está disponible. La
                  vinculación con Google puede
                  requerir una URL pública
                  accesible.
                </div>
              )}
            </div>

            <button
              type="button"
              className="android-connect-button"
              disabled={
                connecting ||
                !readyForSignup ||
                !canConnect
              }
              onClick={() =>
                void connect()
              }
            >
              {!canConnect
                ? 'Sin permiso'
                : connecting
                  ? 'Conectando...'
                  : 'Conectar Android Enterprise'}
            </button>
          </div>
        </section>
      )}

      {active && (
        <>
          <section className="enrollment-stats">
            <article className="enrollment-stat-card">
              <span>
                Inscripciones Android
              </span>

              <strong>
                {statistics.total}
              </strong>

              <small>
                Credenciales Enterprise
              </small>
            </article>

            <article className="enrollment-stat-card">
              <span>Activas</span>

              <strong>
                {statistics.active}
              </strong>

              <small>
                Disponibles
              </small>
            </article>

            <article className="enrollment-stat-card">
              <span>Expiradas</span>

              <strong>
                {statistics.expired}
              </strong>

              <small>
                Fuera de vigencia
              </small>
            </article>

            <article className="enrollment-stat-card">
              <span>Revocadas</span>

              <strong>
                {statistics.revoked}
              </strong>

              <small>
                Bloqueadas
              </small>
            </article>
          </section>

          <div className="enrollment-main-grid">
            <section className="enrollment-panel">
              <div className="enrollment-panel-header">
                <h2>
                  Nueva inscripción Android
                </h2>

                <p>
                  Selecciona el modo de
                  administración y genera el QR
                  oficial de Android Enterprise.
                </p>
              </div>

              <div className="enrollment-form">
                <div className="enrollment-field">
                  <label>
                    Modo de administración
                  </label>

                  <div className="android-mode-grid">
                    {modes.map(
                      (availableMode) => (
                        <button
                          key={availableMode}
                          type="button"
                          className={
                            mode ===
                            availableMode
                              ? 'android-mode-card android-mode-card-selected'
                              : 'android-mode-card'
                          }
                          disabled={
                            creating ||
                            !canCreate
                          }
                          onClick={() =>
                            setMode(
                              availableMode,
                            )
                          }
                        >
                          <strong>
                            {getAndroidModeLabel(
                              availableMode,
                            )}
                          </strong>

                          <span>
                            {mode ===
                            availableMode
                              ? 'Seleccionado'
                              : 'Disponible'}
                          </span>
                        </button>
                      ),
                    )}
                  </div>

                  <small>
                    {getAndroidModeDescription(
                      mode,
                    )}
                  </small>
                </div>

                <div className="enrollment-field">
                  <label htmlFor="androidEnterpriseExpiration">
                    Vigencia del QR
                  </label>

                  <select
                    id="androidEnterpriseExpiration"
                    value={
                      expirationMinutes
                    }
                    disabled={
                      creating ||
                      !canCreate
                    }
                    onChange={(event) =>
                      setExpirationMinutes(
                        Number(
                          event.target
                            .value,
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
                  </select>
                </div>

                <button
                  className="enrollment-primary-button"
                  type="button"
                  disabled={
                    creating ||
                    !canCreate
                  }
                  onClick={() =>
                    void createEnrollment()
                  }
                >
                  {!canCreate
                    ? 'Sin permiso para crear'
                    : creating
                      ? 'Generando...'
                      : 'Generar QR de inscripción'}
                </button>
              </div>
            </section>

            <section className="enrollment-panel">
              <div className="enrollment-panel-header">
                <h2>
                  QR de aprovisionamiento
                </h2>

                <p>
                  Utiliza este QR únicamente
                  durante el aprovisionamiento
                  Android Enterprise.
                </p>
              </div>

              {!createdEnrollment ? (
                <div className="enrollment-empty-secret">
                  <div className="enrollment-secret-icon">
                    ▦
                  </div>

                  <h3>
                    Ningún QR nuevo
                  </h3>

                  <p>
                    Genera una inscripción
                    Android Enterprise para
                    visualizar aquí el QR.
                  </p>
                </div>
              ) : (
                <div className="android-qr-result">
                  <div className="enrollment-secret-warning">
                    Esta credencial contiene
                    información sensible de
                    aprovisionamiento. No la
                    publiques ni la incluyas en
                    el repositorio.
                  </div>

                  {qrDataUrl && (
                    <div className="android-qr-container">
                      <img
                        src={qrDataUrl}
                        alt="QR de inscripción Android Enterprise"
                      />
                    </div>
                  )}

                  <div className="android-token-summary">
                    <span>
                      Token Android Enterprise
                    </span>

                    <code>
                      {
                        createdEnrollment
                          .enrollmentToken
                      }
                    </code>
                  </div>

                  <button
                    type="button"
                    className="enrollment-copy-button"
                    onClick={() =>
                      void copyToken()
                    }
                  >
                    {copied
                      ? 'Copiado'
                      : 'Copiar token'}
                  </button>

                  <dl className="enrollment-secret-details">
                    <div>
                      <dt>Modo</dt>
                      <dd>
                        {getAndroidModeLabel(
                          createdEnrollment.mode,
                        )}
                      </dd>
                    </div>

                    <div>
                      <dt>Creado</dt>
                      <dd>
                        {formatDate(
                          createdEnrollment
                            .createdAtUtc,
                        )}
                      </dd>
                    </div>

                    <div>
                      <dt>Expira</dt>
                      <dd>
                        {formatDate(
                          createdEnrollment
                            .expiresAtUtc,
                        )}
                      </dd>
                    </div>
                  </dl>
                </div>
              )}
            </section>
          </div>

          <section className="enrollment-panel">
            <div className="enrollment-panel-header">
              <h2>
                Historial Android Enterprise
              </h2>

              <p>
                Credenciales de
                aprovisionamiento creadas en
                Google Android Management API.
              </p>
            </div>

            {loading ? (
              <div className="enrollment-loading">
                Cargando inscripciones...
              </div>
            ) : enrollments.length === 0 ? (
              <div className="enrollment-empty-table">
                Todavía no existen
                inscripciones Android
                Enterprise.
              </div>
            ) : (
              <div className="enrollment-table-wrapper">
                <table className="enrollment-table">
                  <thead>
                    <tr>
                      <th>Modo</th>
                      <th>Estado</th>
                      <th>Creado</th>
                      <th>Expira</th>
                      <th>Acciones</th>
                    </tr>
                  </thead>

                  <tbody>
                    {enrollments.map(
                      (enrollment) => {
                        const enrollmentStatus =
                          enrollment.isRevoked
                            ? 'Revoked'
                            : enrollment.isExpired
                              ? 'Expired'
                              : 'Active'

                        return (
                          <tr
                            key={
                              enrollment.id
                            }
                          >
                            <td>
                              <strong>
                                {getAndroidModeLabel(
                                  enrollment.mode,
                                )}
                              </strong>
                            </td>

                            <td>
                              <span
                                className={
                                  `enrollment-status ` +
                                  `enrollment-status-${enrollmentStatus.toLowerCase()}`
                                }
                              >
                                {getStatusLabel(
                                  enrollmentStatus,
                                )}
                              </span>
                            </td>

                            <td>
                              {formatDate(
                                enrollment
                                  .createdAtUtc,
                              )}
                            </td>

                            <td>
                              {formatDate(
                                enrollment
                                  .expiresAtUtc,
                              )}
                            </td>

                            <td>
                              <button
                                type="button"
                                className="enrollment-action-button"
                                disabled={
                                  enrollment.isRevoked ||
                                  enrollment.isExpired ||
                                  !canRevoke
                                }
                                onClick={() =>
                                  void revokeEnrollment(
                                    enrollment,
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
        </>
      )}
    </div>
  )
}