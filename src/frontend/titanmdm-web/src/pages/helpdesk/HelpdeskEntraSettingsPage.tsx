import {
  useEffect,
  useState,
} from 'react'

import {
  CheckCircle2,
  CircleAlert,
  CloudCog,
  ExternalLink,
  KeyRound,
  RefreshCw,
  Search,
  ShieldCheck,
  UserPlus,
  Users,
  XCircle,
} from 'lucide-react'

import {
  TitanPageHeader,
} from '../../components/ui/TitanPageHeader'

import {
  helpdeskApi,
  type EntraDirectoryUser,
  type EntraIdSettings,
  type EntraLoginStatus,
} from '../../api/helpdeskApi'

import apiClient
  from '../../api/apiClient'

import {
  rolesApi,
  type RoleListItem,
} from '../../api/rolesApi'

import {
  useAuth,
} from '../../auth/AuthContext'

import './HelpdeskPages.css'
import './HelpdeskEntraSettingsPage.css'

type AccessSelection = {
  directoryUser:
    EntraDirectoryUser

  roleId:
    string
}

function errorMessage(
  error: unknown,
  fallback: string,
) {
  if (
    typeof error ===
      'object'
    &&
    error !== null
    &&
    'response' in error
  ) {
    const response =
      error.response as {
        data?: {
          message?: string
        }
      }

    if (
      response?.data
        ?.message
    ) {
      return response
        .data
        .message
    }
  }

  return fallback
}

function DiagnosticRow({
  label,
  ready,
  value,
}: {
  label: string
  ready: boolean
  value?: string | null
}) {
  return (
    <div
      className="entra-settings__diagnostic-row"
    >
      <span
        className={
          ready
            ? 'entra-settings__diagnostic-icon entra-settings__diagnostic-icon--ready'
            : 'entra-settings__diagnostic-icon entra-settings__diagnostic-icon--error'
        }
      >
        {ready ? (
          <CheckCircle2
            size={17}
          />
        ) : (
          <XCircle
            size={17}
          />
        )}
      </span>

      <div>
        <strong>
          {label}
        </strong>

        {value && (
          <small>
            {value}
          </small>
        )}
      </div>
    </div>
  )
}

export function HelpdeskEntraSettingsPage() {
  const {
    hasPermission,
  } =
    useAuth()

  const canAssignAccess =
    hasPermission(
      'users.manage',
    )

  const [
    settings,
    setSettings,
  ] =
    useState<
      EntraIdSettings | null
    >(
      null,
    )

  const [
    loginStatus,
    setLoginStatus,
  ] =
    useState<
      EntraLoginStatus | null
    >(
      null,
    )

  const [
    tenantId,
    setTenantId,
  ] =
    useState(
      '',
    )

  const [
    clientId,
    setClientId,
  ] =
    useState(
      '',
    )

  const [
    clientSecret,
    setClientSecret,
  ] =
    useState(
      '',
    )

  const [
    allowedGroupIds,
    setAllowedGroupIds,
  ] =
    useState(
      '',
    )

  const [
    isEnabled,
    setIsEnabled,
  ] =
    useState(
      false,
    )

  const [
    syncRequestersOnly,
    setSyncRequestersOnly,
  ] =
    useState(
      true,
    )

  const [
    users,
    setUsers,
  ] =
    useState<
      EntraDirectoryUser[]
    >(
      [],
    )

  const [
    roles,
    setRoles,
  ] =
    useState<
      RoleListItem[]
    >(
      [],
    )

  const [
    search,
    setSearch,
  ] =
    useState(
      '',
    )

  const [
    selected,
    setSelected,
  ] =
    useState<
      AccessSelection | null
    >(
      null,
    )

  const [
    busy,
    setBusy,
  ] =
    useState(
      false,
    )

  const [
    message,
    setMessage,
  ] =
    useState<
      string | null
    >(
      null,
    )

  const [
    error,
    setError,
  ] =
    useState<
      string | null
    >(
      null,
    )

  async function loadDirectory(
    searchTerm = '',
  ) {
    const result =
      await helpdeskApi
        .searchEntraUsers(
          searchTerm,
        )

    setUsers(
      result,
    )
  }

  async function loadLoginStatus() {
    try {
      const status =
        await helpdeskApi
          .getEntraLoginStatus()

      setLoginStatus(
        status,
      )
    }
    catch {
      setLoginStatus(
        null,
      )
    }
  }

  async function load() {
    setError(
      null,
    )

    try {
      const [
        current,
      ] =
        await Promise.all(
          [
            helpdeskApi
              .getEntraSettings(),

            loadLoginStatus(),
          ],
        )

      setSettings(
        current,
      )

      setTenantId(
        current.tenantId
        ??
        '',
      )

      setClientId(
        current.clientId
        ??
        '',
      )

      setAllowedGroupIds(
        current.allowedGroupIds
        ??
        '',
      )

      setIsEnabled(
        current.isEnabled,
      )

      setSyncRequestersOnly(
        current
          .syncRequestersOnly,
      )

      await loadDirectory()

      if (
        canAssignAccess
      ) {
        const availableRoles =
          await rolesApi
            .getRoles(
              true,
            )

        setRoles(
          availableRoles
            .filter(
              role =>
                role.isActive
                &&
                !role.isSystemRole,
            ),
        )
      }
    }
    catch (
      cause
    ) {
      setError(
        errorMessage(
          cause,
          'No se pudo cargar la configuración de Entra ID.',
        ),
      )
    }
  }

  useEffect(
    () => {
      void load()

      // Carga inicial con permisos de sesión.
      // eslint-disable-next-line react-hooks/exhaustive-deps
    },
    [],
  )

  async function saveSettings() {
    if (busy) {
      return
    }

    setBusy(
      true,
    )

    setError(
      null,
    )

    setMessage(
      null,
    )

    try {
      const saved =
        await helpdeskApi
          .saveEntraSettings(
            {
              isEnabled,

              tenantId:
                tenantId.trim(),

              clientId:
                clientId.trim(),

              clientSecret:
                clientSecret
                ||
                undefined,

              allowedGroupIds:
                allowedGroupIds
                  .trim(),

              syncRequestersOnly,
            },
          )

      setSettings(
        saved,
      )

      setClientSecret(
        '',
      )

      await loadLoginStatus()

      setMessage(
        'Configuración de Microsoft Entra ID guardada.',
      )
    }
    catch (
      cause
    ) {
      setError(
        errorMessage(
          cause,
          'No se pudo guardar la configuración.',
        ),
      )
    }
    finally {
      setBusy(
        false,
      )
    }
  }

  async function syncDirectory() {
    if (busy) {
      return
    }

    setBusy(
      true,
    )

    setError(
      null,
    )

    setMessage(
      null,
    )

    try {
      const result =
        await helpdeskApi
          .syncEntra()

      await loadDirectory(
        search.trim(),
      )

      setSettings(
        await helpdeskApi
          .getEntraSettings(),
      )

      await loadLoginStatus()

      setMessage(
        `Sincronización completada: ${result.imported} importados, ` +
        `${result.updated} actualizados y ` +
        `${result.linkedToExistingUsers} vinculados.`,
      )
    }
    catch (
      cause
    ) {
      setError(
        errorMessage(
          cause,
          'No se pudo sincronizar el directorio.',
        ),
      )
    }
    finally {
      setBusy(
        false,
      )
    }
  }

  async function searchUsers() {
    if (busy) {
      return
    }

    setBusy(
      true,
    )

    setError(
      null,
    )

    try {
      await loadDirectory(
        search.trim(),
      )
    }
    catch (
      cause
    ) {
      setError(
        errorMessage(
          cause,
          'No se pudo buscar en el directorio.',
        ),
      )
    }
    finally {
      setBusy(
        false,
      )
    }
  }

  function selectUser(
    directoryUser:
      EntraDirectoryUser,
  ) {
    if (
      !canAssignAccess
      ||
      directoryUser
        .linkedTitanUserId
    ) {
      return
    }

    setSelected({
      directoryUser,
      roleId:
        '',
    })

    setError(
      null,
    )

    setMessage(
      null,
    )
  }

  async function assignAccess() {
    if (
      !selected
      ||
      busy
      ||
      !canAssignAccess
    ) {
      return
    }

    if (
      !roles.some(
        role =>
          role.id ===
          selected.roleId,
      )
    ) {
      setError(
        'Selecciona un rol válido.',
      )

      return
    }

    setBusy(
      true,
    )

    setError(
      null,
    )

    setMessage(
      null,
    )

    try {
      const response =
        await apiClient
          .post<{
            userId: string
            email: string
            roleName: string
          }>(
            `/helpdesk/entra/access/${selected.directoryUser.id}`,
            {
              roleId:
                selected.roleId,
            },
          )

      setSelected(
        null,
      )

      await loadDirectory(
        search.trim(),
      )

      setMessage(
        `Rol «${response.data.roleName}» asignado a ` +
        `${response.data.email}. Esta cuenta ya puede ser utilizada ` +
        'por el flujo de inicio de sesión Microsoft cuando la configuración esté lista.',
      )
    }
    catch (
      cause
    ) {
      setError(
        errorMessage(
          cause,
          'No se pudo asignar el acceso.',
        ),
      )
    }
    finally {
      setBusy(
        false,
      )
    }
  }

  function testMicrosoftLogin() {
    window.location.href =
      '/api/auth/entra/start'
  }

  const linkedUsers =
    users.filter(
      user =>
        Boolean(
          user.linkedTitanUserId,
        ),
    ).length

  const loginReady =
    Boolean(
      loginStatus
        ?.enabled
      &&
      loginStatus
        ?.tenantConfigured
      &&
      loginStatus
        ?.clientConfigured
      &&
      loginStatus
        ?.secretConfigured
      &&
      loginStatus
        ?.callbackUrl,
    )

  return (
    <main
      className="titan-page helpdesk-page entra-settings"
    >
      <TitanPageHeader
        eyebrow="Directorio empresarial"
        title="Microsoft Entra ID"
        description={
          'Configura Microsoft Graph, sincroniza usuarios y valida ' +
          'el inicio de sesión corporativo de TitanMDM.'
        }
        icon={
          <CloudCog
            size={16}
          />
        }
      />

      {message && (
        <div
          className="entra-settings__success"
          role="status"
        >
          <ShieldCheck
            size={18}
          />

          <span>
            {message}
          </span>
        </div>
      )}

      {error && (
        <div
          className="entra-settings__error"
          role="alert"
        >
          {error}
        </div>
      )}

      {/* ======================================================
          DIAGNOSTIC
         ====================================================== */}

      <section
        className="entra-settings__card entra-settings__diagnostic"
      >
        <div
          className="entra-settings__card-header"
        >
          <div
            className="entra-settings__icon"
          >
            <ShieldCheck
              size={22}
            />
          </div>

          <div>
            <h2>
              Estado de integración
            </h2>

            <p>
              Comprueba qué componentes de
              Microsoft Entra están listos
              antes de probar el acceso.
            </p>
          </div>
        </div>

        <div
          className="entra-settings__diagnostic-grid"
        >
          <DiagnosticRow
            label="Entra habilitado"
            ready={
              Boolean(
                loginStatus
                  ?.enabled,
              )
            }
          />

          <DiagnosticRow
            label="Tenant ID"
            ready={
              Boolean(
                loginStatus
                  ?.tenantConfigured,
              )
            }
            value={
              settings
                ?.tenantId
            }
          />

          <DiagnosticRow
            label="Application ID"
            ready={
              Boolean(
                loginStatus
                  ?.clientConfigured,
              )
            }
            value={
              settings
                ?.clientId
            }
          />

          <DiagnosticRow
            label="Client Secret"
            ready={
              Boolean(
                loginStatus
                  ?.secretConfigured,
              )
            }
            value={
              loginStatus
                ?.secretConfigured
                ? 'Configurado y protegido'
                : 'No configurado'
            }
          />

          <DiagnosticRow
            label="Directorio sincronizado"
            ready={
              Boolean(
                settings
                  ?.lastSyncAtUtc,
              )
            }
            value={
              settings
                ?.lastSyncStatus
            }
          />

          <DiagnosticRow
            label="Usuarios vinculados"
            ready={
              linkedUsers >
              0
            }
            value={
              `${linkedUsers} visibles en esta consulta`
            }
          />
        </div>

        <div
          className="entra-settings__callback"
        >
          <div>
            <strong>
              Redirect URI para Microsoft
            </strong>

            <code>
              {
                loginStatus
                  ?.callbackUrl
                ??
                'Todavía no disponible'
              }
            </code>
          </div>

          <p>
            Esta URL debe estar registrada
            como Redirect URI tipo Web en la
            App Registration de TitanMDM.
          </p>
        </div>

        <div
          className="entra-settings__actions"
        >
          <button
            type="button"
            className="entra-settings__button entra-settings__button--light"
            disabled={
              busy
            }
            onClick={
              () =>
                void loadLoginStatus()
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar diagnóstico
          </button>

          <button
            type="button"
            className="entra-settings__button"
            disabled={
              !loginReady
            }
            onClick={
              testMicrosoftLogin
            }
          >
            <ExternalLink
              size={16}
            />

            Probar inicio Microsoft
          </button>
        </div>

        {!loginReady && (
          <div
            className="entra-settings__warning"
          >
            <CircleAlert
              size={17}
            />

            El inicio de sesión todavía
            no está listo. Corrige los
            elementos marcados antes de
            probarlo.
          </div>
        )}
      </section>

      {/* ======================================================
          GRAPH SETTINGS
         ====================================================== */}

      <section
        className="entra-settings__card"
      >
        <div
          className="entra-settings__card-header"
        >
          <div
            className="entra-settings__icon"
          >
            <CloudCog
              size={22}
            />
          </div>

          <div>
            <h2>
              Aplicación Microsoft Entra
            </h2>

            <p>
              Esta misma configuración se
              utiliza para Directorio,
              Graph y Login Microsoft.
            </p>
          </div>
        </div>

        <div
          className="entra-settings__fields"
        >
          <label>
            Tenant ID

            <input
              value={
                tenantId
              }
              onChange={
                event =>
                  setTenantId(
                    event.target
                      .value,
                  )
              }
              placeholder="Directory (tenant) ID"
            />
          </label>

          <label>
            Application (client) ID

            <input
              value={
                clientId
              }
              onChange={
                event =>
                  setClientId(
                    event.target
                      .value,
                  )
              }
              placeholder="Application (client) ID"
            />
          </label>

          <label
            className="entra-settings__full"
          >
            Client Secret

            <input
              type="password"
              autoComplete="new-password"
              value={
                clientSecret
              }
              onChange={
                event =>
                  setClientSecret(
                    event.target
                      .value,
                  )
              }
              placeholder={
                settings
                  ?.hasClientSecret
                  ? 'Ya configurado; escribe uno nuevo únicamente para reemplazarlo'
                  : 'Introduce el valor del secreto'
              }
            />
          </label>

          <label
            className="entra-settings__full"
          >
            Object IDs de grupos permitidos

            <input
              value={
                allowedGroupIds
              }
              onChange={
                event =>
                  setAllowedGroupIds(
                    event.target
                      .value,
                  )
              }
              placeholder="Opcional; separados por coma"
            />
          </label>
        </div>

        <div
          className="entra-settings__options"
        >
          <label
            className="entra-settings__check"
          >
            <input
              type="checkbox"
              checked={
                isEnabled
              }
              onChange={
                event =>
                  setIsEnabled(
                    event.target
                      .checked,
                  )
              }
            />

            <span>
              Habilitar Microsoft Entra ID
            </span>
          </label>

          <label
            className="entra-settings__check"
          >
            <input
              type="checkbox"
              checked={
                syncRequestersOnly
              }
              onChange={
                event =>
                  setSyncRequestersOnly(
                    event.target
                      .checked,
                  )
              }
            />

            <span>
              Guardar preferencia
              «solo solicitantes»
            </span>
          </label>
        </div>

        <p
          className="entra-settings__hint"
        >
          Tenant ID, Client ID y Client Secret
          son utilizados por TitanMDM para
          Microsoft Graph y el inicio de sesión
          corporativo. El secreto permanece
          protegido en el servidor.
        </p>

        <div
          className="entra-settings__actions"
        >
          <button
            type="button"
            className="entra-settings__button"
            disabled={
              busy
            }
            onClick={
              () =>
                void saveSettings()
            }
          >
            <KeyRound
              size={16}
            />

            Guardar configuración
          </button>

          <button
            type="button"
            className="entra-settings__button entra-settings__button--light"
            disabled={
              busy
              ||
              !settings
                ?.isEnabled
            }
            onClick={
              () =>
                void syncDirectory()
            }
          >
            <RefreshCw
              size={16}
            />

            Sincronizar directorio
          </button>
        </div>

        {settings
          ?.lastSyncStatus && (
          <p
            className="entra-settings__last-sync"
          >
            Última sincronización:
            {' '}
            {
              settings
                .lastSyncStatus
            }
          </p>
        )}
      </section>

      {/* ======================================================
          DIRECTORY
         ====================================================== */}

      <section
        className="entra-settings__card"
      >
        <div
          className="entra-settings__card-header"
        >
          <div
            className="entra-settings__icon"
          >
            <Users
              size={22}
            />
          </div>

          <div>
            <h2>
              Personas sincronizadas
            </h2>

            <p>
              Busca una persona de Entra ID,
              vincúlala con TitanMDM y asígnale
              un rol de acceso.
            </p>
          </div>
        </div>

        <div
          className="entra-settings__search"
        >
          <input
            aria-label="Buscar persona sincronizada"
            value={
              search
            }
            onChange={
              event =>
                setSearch(
                  event.target
                    .value,
                )
            }
            onKeyDown={
              event => {
                if (
                  event.key ===
                  'Enter'
                ) {
                  void searchUsers()
                }
              }
            }
            placeholder="Nombre, correo o UPN"
          />

          <button
            type="button"
            className="entra-settings__button entra-settings__button--light"
            disabled={
              busy
            }
            onClick={
              () =>
                void searchUsers()
            }
          >
            <Search
              size={16}
            />

            Buscar
          </button>
        </div>

        <div
          className="entra-settings__table-wrap"
        >
          <table
            className="entra-settings__table"
          >
            <thead>
              <tr>
                <th>
                  Nombre
                </th>

                <th>
                  UPN
                </th>

                <th>
                  Correo
                </th>

                <th>
                  Departamento
                </th>

                <th>
                  Acceso
                </th>

                {canAssignAccess && (
                  <th>
                    Acción
                  </th>
                )}
              </tr>
            </thead>

            <tbody>
              {users.map(
                item => (
                  <tr
                    key={
                      item.id
                    }
                  >
                    <td>
                      <strong>
                        {
                          item
                            .displayName
                        }
                      </strong>
                    </td>

                    <td>
                      {
                        item
                          .userPrincipalName
                      }
                    </td>

                    <td>
                      {
                        item.mail
                        ??
                        '—'
                      }
                    </td>

                    <td>
                      {
                        item.department
                        ??
                        '—'
                      }
                    </td>

                    <td>
                      <span
                        className={
                          item
                            .linkedTitanUserId
                            ? 'entra-settings__status entra-settings__status--ready'
                            : 'entra-settings__status'
                        }
                      >
                        {
                          item
                            .linkedTitanUserId
                            ? 'Vinculada'
                            : 'Sin acceso'
                        }
                      </span>
                    </td>

                    {canAssignAccess && (
                      <td>
                        {!item
                          .linkedTitanUserId && (
                          <button
                            type="button"
                            className="entra-settings__button entra-settings__button--small"
                            disabled={
                              busy
                            }
                            onClick={
                              () =>
                                selectUser(
                                  item,
                                )
                            }
                          >
                            Asignar rol
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                ),
              )}

              {users.length ===
                0 && (
                <tr>
                  <td
                    colSpan={
                      canAssignAccess
                        ? 6
                        : 5
                    }
                  >
                    No hay personas para mostrar.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* ======================================================
          ACCESS
         ====================================================== */}

      {selected &&
        canAssignAccess && (
        <section
          className="entra-settings__card"
        >
          <div
            className="entra-settings__card-header"
          >
            <div
              className="entra-settings__icon"
            >
              <UserPlus
                size={22}
              />
            </div>

            <div>
              <h2>
                Asignar acceso individual
              </h2>

              <p>
                {
                  selected
                    .directoryUser
                    .displayName
                }
                {' · '}
                {
                  selected
                    .directoryUser
                    .userPrincipalName
                }
              </p>
            </div>
          </div>

          <label
            className="entra-settings__role"
          >
            Rol en TitanMDM

            <select
              value={
                selected.roleId
              }
              onChange={
                event =>
                  setSelected({
                    ...selected,

                    roleId:
                      event.target
                        .value,
                  })
              }
            >
              <option value="">
                Selecciona un rol
              </option>

              {roles.map(
                role => (
                  <option
                    key={
                      role.id
                    }
                    value={
                      role.id
                    }
                  >
                    {
                      role.name
                    }
                  </option>
                ),
              )}
            </select>
          </label>

          {roles.length ===
            0 && (
            <p
              className="entra-settings__hint"
            >
              Crea primero un rol
              no sistémico en
              Administración → Roles.
            </p>
          )}

          <p
            className="entra-settings__hint"
          >
            TitanMDM no solicita ni almacena
            la contraseña corporativa.
            Microsoft realiza la autenticación
            y TitanMDM aplica posteriormente
            sus propios roles y permisos.
          </p>

          <div
            className="entra-settings__actions"
          >
            <button
              type="button"
              className="entra-settings__button"
              disabled={
                busy
                ||
                !selected.roleId
              }
              onClick={
                () =>
                  void assignAccess()
              }
            >
              <UserPlus
                size={16}
              />

              Asignar este rol
            </button>

            <button
              type="button"
              className="entra-settings__button entra-settings__button--light"
              disabled={
                busy
              }
              onClick={
                () =>
                  setSelected(
                    null,
                  )
              }
            >
              Cancelar
            </button>
          </div>
        </section>
      )}
    </main>
  )
}