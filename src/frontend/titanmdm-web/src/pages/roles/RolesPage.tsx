import {
  KeyRound,
  Plus,
  RefreshCw,
  ShieldCheck,
  Users,
  X,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  rolesApi,
  type PermissionModule,
  type RoleDetails,
  type RoleListItem,
} from '../../api/rolesApi'

import { useAuth } from '../../auth/AuthContext'

import '../users/AdministrationPage.css'

function getErrorMessage(
  error: unknown,
): string {
  if (
    typeof error === 'object' &&
    error !== null &&
    'response' in error
  ) {
    const response =
      (
        error as {
          response?: {
            data?: {
              message?: string
            }
          }
        }
      ).response

    if (
      response?.data?.message
    ) {
      return response.data.message
    }
  }

  if (
    error instanceof Error
  ) {
    return error.message
  }

  return 'Ocurrió un error inesperado.'
}

export function RolesPage() {
  const {
    hasPermission,
  } =
    useAuth()

  const canManage =
    hasPermission(
      'roles.manage',
    )

  const [roles, setRoles] =
    useState<RoleListItem[]>(
      [],
    )

  const [
    permissionModules,
    setPermissionModules,
  ] =
    useState<PermissionModule[]>(
      [],
    )

  const [
    selectedRole,
    setSelectedRole,
  ] =
    useState<RoleDetails | null>(
      null,
    )

  const [
    selectedPermissions,
    setSelectedPermissions,
  ] =
    useState<string[]>(
      [],
    )

  const [isLoading, setIsLoading] =
    useState(true)

  const [error, setError] =
    useState<string | null>(
      null,
    )

  const [
    showCreate,
    setShowCreate,
  ] =
    useState(false)

  const [
    roleName,
    setRoleName,
  ] =
    useState('')

  const [
    roleDescription,
    setRoleDescription,
  ] =
    useState('')

  const [
    isSaving,
    setIsSaving,
  ] =
    useState(false)

  const loadRoles =
    useCallback(async () => {
      try {
        setIsLoading(true)

        const data =
          await rolesApi.getRoles()

        setRoles(data)
        setError(null)
      } catch (
        loadError
      ) {
        setError(
          getErrorMessage(
            loadError,
          ),
        )
      } finally {
        setIsLoading(false)
      }
    }, [])

  const loadPermissions =
    useCallback(async () => {
      try {
        const response =
          await rolesApi
            .getPermissions()

        setPermissionModules(
          response.modules,
        )
      } catch (
        permissionError
      ) {
        setError(
          getErrorMessage(
            permissionError,
          ),
        )
      }
    }, [])

  useEffect(() => {
    void Promise.all([
      loadRoles(),
      loadPermissions(),
    ])

    document.title =
      'Roles | TitanMDM'
  }, [
    loadRoles,
    loadPermissions,
  ])

  const totalUsers =
    useMemo(
      () =>
        roles.reduce(
          (
            total,
            role,
          ) =>
            total +
            role.userCount,
          0,
        ),
      [roles],
    )

  const openRole =
    async (
      roleId: string,
    ) => {
      try {
        const details =
          await rolesApi.getRole(
            roleId,
          )

        setSelectedRole(
          details,
        )

        setSelectedPermissions(
          details.permissions.map(
            permission =>
              permission.id,
          ),
        )
      } catch (
        roleError
      ) {
        setError(
          getErrorMessage(
            roleError,
          ),
        )
      }
    }

  const createRole =
    async () => {
      try {
        setIsSaving(true)

        await rolesApi.createRole({
          name: roleName,
          description:
            roleDescription,
          permissionIds: [],
        })

        setRoleName('')
        setRoleDescription('')
        setShowCreate(false)

        await loadRoles()
      } catch (
        createError
      ) {
        setError(
          getErrorMessage(
            createError,
          ),
        )
      } finally {
        setIsSaving(false)
      }
    }

  const savePermissions =
    async () => {
      if (
        !selectedRole
      ) {
        return
      }

      try {
        setIsSaving(true)

        await rolesApi
          .replacePermissions(
            selectedRole.id,
            selectedPermissions,
          )

        const refreshed =
          await rolesApi.getRole(
            selectedRole.id,
          )

        setSelectedRole(
          refreshed,
        )

        await loadRoles()
      } catch (
        saveError
      ) {
        setError(
          getErrorMessage(
            saveError,
          ),
        )
      } finally {
        setIsSaving(false)
      }
    }

  const togglePermission =
    (
      permissionId: string,
    ) => {
      setSelectedPermissions(
        current =>
          current.includes(
            permissionId,
          )
            ? current.filter(
                id =>
                  id !==
                  permissionId,
              )
            : [
                ...current,
                permissionId,
              ],
      )
    }

  return (
    <div className="admin-page">
      <header className="admin-page__header">
        <div>
          <span className="admin-eyebrow">
            CONTROL DE ACCESO
          </span>

          <h1>
            Roles y permisos
          </h1>

          <p>
            Define qué áreas y acciones puede
            utilizar cada grupo de usuarios.
          </p>
        </div>

        <div className="admin-page__actions">
          <button
            type="button"
            className="admin-button"
            onClick={() =>
              void loadRoles()
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar
          </button>

          {canManage && (
            <button
              type="button"
              className="admin-button admin-button--primary"
              onClick={() =>
                setShowCreate(
                  true,
                )
              }
            >
              <Plus
                size={16}
              />

              Nuevo rol
            </button>
          )}
        </div>
      </header>

      <section className="admin-summary">
        <article>
          <ShieldCheck
            size={21}
          />

          <div>
            <span>Roles</span>
            <strong>
              {roles.length}
            </strong>
          </div>
        </article>

        <article>
          <KeyRound
            size={21}
          />

          <div>
            <span>Módulos de permisos</span>
            <strong>
              {permissionModules.length}
            </strong>
          </div>
        </article>

        <article>
          <Users
            size={21}
          />

          <div>
            <span>Asignaciones</span>
            <strong>
              {totalUsers}
            </strong>
          </div>
        </article>
      </section>

      {error && (
        <div className="admin-alert admin-alert--error">
          {error}
        </div>
      )}

      <section className="admin-role-grid">
        {isLoading ? (
          <div className="admin-empty">
            Cargando roles...
          </div>
        ) : (
          roles.map(
            role => (
              <button
                key={
                  role.id
                }
                type="button"
                className="admin-role-card"
                onClick={() =>
                  void openRole(
                    role.id,
                  )
                }
              >
                <div className="admin-role-card__icon">
                  <ShieldCheck
                    size={20}
                  />
                </div>

                <div className="admin-role-card__content">
                  <div className="admin-role-card__title">
                    <strong>
                      {role.name}
                    </strong>

                    {role.isSystemRole && (
                      <span>
                        Sistema
                      </span>
                    )}
                  </div>

                  <p>
                    {role.description ??
                      'Sin descripción'}
                  </p>

                  <div className="admin-role-card__metrics">
                    <span>
                      {role.userCount}{' '}
                      usuario(s)
                    </span>

                    <span>
                      {role.permissionCount}{' '}
                      permisos
                    </span>
                  </div>
                </div>
              </button>
            ),
          )
        )}
      </section>

      {selectedRole && (
        <div className="admin-drawer-backdrop">
          <aside className="admin-drawer admin-drawer--wide">
            <header className="admin-drawer__header">
              <div>
                <span>
                  ROL
                </span>

                <h2>
                  {selectedRole.name}
                </h2>
              </div>

              <button
                type="button"
                onClick={() =>
                  setSelectedRole(
                    null,
                  )
                }
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div className="admin-drawer__body">
              <div className="admin-detail-grid">
                <div>
                  <span>Tipo</span>

                  <strong>
                    {selectedRole.isSystemRole
                      ? 'Rol del sistema'
                      : 'Rol personalizado'}
                  </strong>
                </div>

                <div>
                  <span>Usuarios</span>

                  <strong>
                    {selectedRole.users.length}
                  </strong>
                </div>

                <div>
                  <span>Permisos</span>

                  <strong>
                    {selectedPermissions.length}
                  </strong>
                </div>

                <div>
                  <span>Estado</span>

                  <strong>
                    {selectedRole.isActive
                      ? 'Activo'
                      : 'Inactivo'}
                  </strong>
                </div>
              </div>

              <section className="admin-section">
                <h3>
                  Matriz de permisos
                </h3>

                <div className="admin-permission-modules">
                  {permissionModules.map(
                    module => (
                      <div
                        key={
                          module.module
                        }
                        className="admin-permission-module"
                      >
                        <header>
                          <strong>
                            {module.module}
                          </strong>

                          <span>
                            {module.permissions.length}{' '}
                            permisos
                          </span>
                        </header>

                        <div className="admin-permission-list">
                          {module.permissions.map(
                            permission => (
                              <label
                                key={
                                  permission.id
                                }
                              >
                                <input
                                  type="checkbox"
                                  checked={
                                    selectedPermissions.includes(
                                      permission.id,
                                    )
                                  }
                                  disabled={
                                    !canManage ||
                                    (
                                      selectedRole.isSystemRole &&
                                      selectedRole.name
                                        .toLowerCase() ===
                                        'superadmin'
                                    )
                                  }
                                  onChange={() =>
                                    togglePermission(
                                      permission.id,
                                    )
                                  }
                                />

                                <div>
                                  <strong>
                                    {permission.name}
                                  </strong>

                                  <span>
                                    {permission.code}
                                  </span>
                                </div>
                              </label>
                            ),
                          )}
                        </div>
                      </div>
                    ),
                  )}
                </div>
              </section>

              {canManage &&
                !(
                  selectedRole.isSystemRole &&
                  selectedRole.name
                    .toLowerCase() ===
                    'superadmin'
                ) && (
                  <div className="admin-drawer__actions">
                    <button
                      type="button"
                      className="admin-button admin-button--primary"
                      disabled={
                        isSaving
                      }
                      onClick={() =>
                        void savePermissions()
                      }
                    >
                      {isSaving
                        ? 'Guardando...'
                        : 'Guardar permisos'}
                    </button>
                  </div>
                )}
            </div>
          </aside>
        </div>
      )}

      {showCreate && (
        <div className="admin-modal-backdrop">
          <div className="admin-modal">
            <header>
              <div>
                <span>
                  RBAC
                </span>

                <h2>
                  Nuevo rol
                </h2>
              </div>

              <button
                type="button"
                onClick={() =>
                  setShowCreate(
                    false,
                  )
                }
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div className="admin-form-grid">
              <label className="admin-field-full">
                Nombre
                <input
                  value={
                    roleName
                  }
                  onChange={event =>
                    setRoleName(
                      event.target.value,
                    )
                  }
                />
              </label>

              <label className="admin-field-full">
                Descripción
                <textarea
                  value={
                    roleDescription
                  }
                  onChange={event =>
                    setRoleDescription(
                      event.target.value,
                    )
                  }
                />
              </label>
            </div>

            <footer className="admin-modal__footer">
              <button
                type="button"
                className="admin-button"
                onClick={() =>
                  setShowCreate(
                    false,
                  )
                }
              >
                Cancelar
              </button>

              <button
                type="button"
                className="admin-button admin-button--primary"
                disabled={
                  isSaving
                }
                onClick={() =>
                  void createRole()
                }
              >
                Crear rol
              </button>
            </footer>
          </div>
        </div>
      )}
    </div>
  )
}