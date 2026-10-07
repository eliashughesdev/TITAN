import {
  KeyRound,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  UserCheck,
  UserRound,
  UserX,
  X,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  usersApi,
  type CreateUserRequest,
  type UserDetails,
  type UserListItem,
} from '../../api/usersApi'

import {
  rolesApi,
  type RoleListItem,
} from '../../api/rolesApi'

import { useAuth } from '../../auth/AuthContext'

import './AdministrationPage.css'

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

function formatDate(
  value: string | null,
): string {
  if (!value) {
    return 'Nunca'
  }

  const date =
    new Date(value)

  return Number.isNaN(
    date.getTime(),
  )
    ? 'Sin información'
    : date.toLocaleString()
}

const initialCreateForm:
  CreateUserRequest = {
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    jobTitle: '',
    departmentId: null,
    mfaEnabled: false,
    roleIds: [],
  }

export function UsersPage() {
  const {
    hasPermission,
  } =
    useAuth()

  const canManage =
    hasPermission(
      'users.manage',
    )

  const [users, setUsers] =
    useState<UserListItem[]>(
      [],
    )

  const [roles, setRoles] =
    useState<RoleListItem[]>(
      [],
    )

  const [
    selectedUser,
    setSelectedUser,
  ] =
    useState<UserDetails | null>(
      null,
    )

  const [search, setSearch] =
    useState('')

  const [status, setStatus] =
    useState<
      'all' |
      'active' |
      'inactive'
    >('all')

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
    createForm,
    setCreateForm,
  ] =
    useState<CreateUserRequest>(
      initialCreateForm,
    )

  const [
    isSaving,
    setIsSaving,
  ] =
    useState(false)

  const loadUsers =
    useCallback(async () => {
      try {
        setIsLoading(true)
        setError(null)

        const data =
          await usersApi.getUsers({
            search,
            active:
              status === 'all'
                ? undefined
                : status ===
                    'active',
          })

        setUsers(data)
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
    }, [
      search,
      status,
    ])

  const loadRoles =
    useCallback(async () => {
      try {
        const data =
          await rolesApi.getRoles(
            true,
          )

        setRoles(data)
      } catch {
        setRoles([])
      }
    }, [])

  useEffect(() => {
    const timeout =
      window.setTimeout(
        () => {
          void loadUsers()
        },
        250,
      )

    return () =>
      window.clearTimeout(
        timeout,
      )
  }, [loadUsers])

  useEffect(() => {
    void loadRoles()

    document.title =
      'Usuarios | TitanMDM'
  }, [loadRoles])

  const activeUsers =
    useMemo(
      () =>
        users.filter(
          user =>
            user.isActive,
        ).length,
      [users],
    )

  const inactiveUsers =
    users.length -
    activeUsers

  const openUser =
    async (
      userId: string,
    ) => {
      try {
        setError(null)

        const details =
          await usersApi.getUser(
            userId,
          )

        setSelectedUser(
          details,
        )
      } catch (
        userError
      ) {
        setError(
          getErrorMessage(
            userError,
          ),
        )
      }
    }

  const createUser =
    async () => {
      try {
        setIsSaving(true)
        setError(null)

        await usersApi.createUser(
          createForm,
        )

        setCreateForm(
          initialCreateForm,
        )

        setShowCreate(false)

        await loadUsers()
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

  const toggleStatus =
    async (
      user: UserDetails,
    ) => {
      try {
        if (
          user.isActive
        ) {
          await usersApi
            .deactivateUser(
              user.id,
            )
        } else {
          await usersApi
            .activateUser(
              user.id,
            )
        }

        const refreshed =
          await usersApi.getUser(
            user.id,
          )

        setSelectedUser(
          refreshed,
        )

        await loadUsers()
      } catch (
        toggleError
      ) {
        setError(
          getErrorMessage(
            toggleError,
          ),
        )
      }
    }

  const changePassword =
    async (
      userId: string,
    ) => {
      const password =
        window.prompt(
          'Nueva contraseña. Debe tener al menos 12 caracteres.',
        )

      if (!password) {
        return
      }

      try {
        await usersApi
          .changePassword(
            userId,
            password,
          )

        window.alert(
          'Contraseña actualizada correctamente.',
        )
      } catch (
        passwordError
      ) {
        setError(
          getErrorMessage(
            passwordError,
          ),
        )
      }
    }

  const updateRoles =
    async (
      userId: string,
      roleId: string,
      checked: boolean,
    ) => {
      if (
        !selectedUser
      ) {
        return
      }

      const current =
        selectedUser.roles.map(
          role =>
            role.id,
        )

      const next =
        checked
          ? [
              ...current,
              roleId,
            ]
          : current.filter(
              id =>
                id !==
                roleId,
            )

      try {
        await usersApi
          .assignRoles(
            userId,
            next,
          )

        const refreshed =
          await usersApi.getUser(
            userId,
          )

        setSelectedUser(
          refreshed,
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

  return (
    <div className="admin-page">
      <header className="admin-page__header">
        <div>
          <span className="admin-eyebrow">
            IDENTIDAD Y ACCESO
          </span>

          <h1>Usuarios</h1>

          <p>
            Administra las cuentas con acceso
            a TitanMDM y sus roles.
          </p>
        </div>

        <div className="admin-page__actions">
          <button
            type="button"
            className="admin-button"
            onClick={() =>
              void loadUsers()
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

              Nuevo usuario
            </button>
          )}
        </div>
      </header>

      <section className="admin-summary">
        <article>
          <UserRound
            size={21}
          />

          <div>
            <span>Total usuarios</span>
            <strong>
              {users.length}
            </strong>
          </div>
        </article>

        <article>
          <UserCheck
            size={21}
          />

          <div>
            <span>Activos</span>
            <strong>
              {activeUsers}
            </strong>
          </div>
        </article>

        <article>
          <UserX
            size={21}
          />

          <div>
            <span>Inactivos</span>
            <strong>
              {inactiveUsers}
            </strong>
          </div>
        </article>

        <article>
          <ShieldCheck
            size={21}
          />

          <div>
            <span>Roles disponibles</span>
            <strong>
              {roles.length}
            </strong>
          </div>
        </article>
      </section>

      {error && (
        <div className="admin-alert admin-alert--error">
          {error}
        </div>
      )}

      <section className="admin-panel">
        <div className="admin-toolbar">
          <div className="admin-search">
            <Search
              size={16}
            />

            <input
              type="search"
              placeholder="Buscar usuario, correo o cargo..."
              value={search}
              onChange={event =>
                setSearch(
                  event.target.value,
                )
              }
            />
          </div>

          <select
            value={status}
            onChange={event =>
              setStatus(
                event.target.value as
                  | 'all'
                  | 'active'
                  | 'inactive',
              )
            }
          >
            <option value="all">
              Todos
            </option>

            <option value="active">
              Activos
            </option>

            <option value="inactive">
              Inactivos
            </option>
          </select>
        </div>

        <div className="admin-table-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Usuario</th>
                <th>Cargo</th>
                <th>Estado</th>
                <th>MFA</th>
                <th>Último acceso</th>
              </tr>
            </thead>

            <tbody>
              {isLoading ? (
                <tr>
                  <td
                    colSpan={5}
                    className="admin-empty"
                  >
                    Cargando usuarios...
                  </td>
                </tr>
              ) : users.length ===
                0 ? (
                <tr>
                  <td
                    colSpan={5}
                    className="admin-empty"
                  >
                    No hay usuarios.
                  </td>
                </tr>
              ) : (
                users.map(
                  user => (
                    <tr
                      key={
                        user.id
                      }
                      onClick={() =>
                        void openUser(
                          user.id,
                        )
                      }
                    >
                      <td>
                        <div className="admin-user-cell">
                          <div className="admin-avatar">
                            {user.firstName
                              .charAt(0)
                              .toUpperCase()}
                            {user.lastName
                              .charAt(0)
                              .toUpperCase()}
                          </div>

                          <div>
                            <strong>
                              {user.fullName}
                            </strong>

                            <span>
                              {user.email}
                            </span>
                          </div>
                        </div>
                      </td>

                      <td>
                        {user.jobTitle ??
                          'Sin cargo'}
                      </td>

                      <td>
                        <span
                          className={
                            user.isActive
                              ? 'admin-status admin-status--active'
                              : 'admin-status admin-status--inactive'
                          }
                        >
                          {user.isActive
                            ? 'Activo'
                            : 'Inactivo'}
                        </span>
                      </td>

                      <td>
                        {user.mfaEnabled
                          ? 'Habilitado'
                          : 'No'}
                      </td>

                      <td>
                        {formatDate(
                          user.lastLoginAtUtc,
                        )}
                      </td>
                    </tr>
                  ),
                )
              )}
            </tbody>
          </table>
        </div>
      </section>

      {selectedUser && (
        <div className="admin-drawer-backdrop">
          <aside className="admin-drawer">
            <header className="admin-drawer__header">
              <div>
                <span>
                  Usuario
                </span>

                <h2>
                  {selectedUser.fullName}
                </h2>
              </div>

              <button
                type="button"
                onClick={() =>
                  setSelectedUser(
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
                  <span>Correo</span>
                  <strong>
                    {selectedUser.email}
                  </strong>
                </div>

                <div>
                  <span>Cargo</span>
                  <strong>
                    {selectedUser.jobTitle ??
                      'Sin cargo'}
                  </strong>
                </div>

                <div>
                  <span>Estado</span>
                  <strong>
                    {selectedUser.isActive
                      ? 'Activo'
                      : 'Inactivo'}
                  </strong>
                </div>

                <div>
                  <span>MFA</span>
                  <strong>
                    {selectedUser.mfaEnabled
                      ? 'Habilitado'
                      : 'Deshabilitado'}
                  </strong>
                </div>
              </div>

              <section className="admin-section">
                <h3>Roles asignados</h3>

                <div className="admin-permission-list">
                  {roles.map(
                    role => {
                      const checked =
                        selectedUser.roles.some(
                          current =>
                            current.id ===
                            role.id,
                        )

                      return (
                        <label
                          key={
                            role.id
                          }
                        >
                          <input
                            type="checkbox"
                            checked={
                              checked
                            }
                            disabled={
                              !canManage
                            }
                            onChange={event =>
                              void updateRoles(
                                selectedUser.id,
                                role.id,
                                event.target.checked,
                              )
                            }
                          />

                          <div>
                            <strong>
                              {role.name}
                            </strong>

                            <span>
                              {role.description ??
                                'Sin descripción'}
                            </span>
                          </div>
                        </label>
                      )
                    },
                  )}
                </div>
              </section>

              {canManage && (
                <div className="admin-drawer__actions">
                  <button
                    type="button"
                    className="admin-button"
                    onClick={() =>
                      void changePassword(
                        selectedUser.id,
                      )
                    }
                  >
                    <KeyRound
                      size={16}
                    />

                    Cambiar contraseña
                  </button>

                  <button
                    type="button"
                    className={
                      selectedUser.isActive
                        ? 'admin-button admin-button--danger'
                        : 'admin-button admin-button--success'
                    }
                    onClick={() =>
                      void toggleStatus(
                        selectedUser,
                      )
                    }
                  >
                    {selectedUser.isActive
                      ? 'Desactivar'
                      : 'Activar'}
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
                  NUEVA CUENTA
                </span>

                <h2>
                  Crear usuario
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
              <label>
                Nombre
                <input
                  value={
                    createForm.firstName
                  }
                  onChange={event =>
                    setCreateForm(
                      current => ({
                        ...current,
                        firstName:
                          event.target.value,
                      }),
                    )
                  }
                />
              </label>

              <label>
                Apellido
                <input
                  value={
                    createForm.lastName
                  }
                  onChange={event =>
                    setCreateForm(
                      current => ({
                        ...current,
                        lastName:
                          event.target.value,
                      }),
                    )
                  }
                />
              </label>

              <label className="admin-field-full">
                Correo
                <input
                  type="email"
                  value={
                    createForm.email
                  }
                  onChange={event =>
                    setCreateForm(
                      current => ({
                        ...current,
                        email:
                          event.target.value,
                      }),
                    )
                  }
                />
              </label>

              <label className="admin-field-full">
                Cargo
                <input
                  value={
                    createForm.jobTitle ??
                    ''
                  }
                  onChange={event =>
                    setCreateForm(
                      current => ({
                        ...current,
                        jobTitle:
                          event.target.value,
                      }),
                    )
                  }
                />
              </label>

              <label className="admin-field-full">
                Contraseña inicial
                <input
                  type="password"
                  value={
                    createForm.password
                  }
                  onChange={event =>
                    setCreateForm(
                      current => ({
                        ...current,
                        password:
                          event.target.value,
                      }),
                    )
                  }
                />
              </label>
            </div>

            <section className="admin-section">
              <h3>Roles</h3>

              <div className="admin-permission-list">
                {roles.map(
                  role => {
                    const checked =
                      createForm.roleIds?.includes(
                        role.id,
                      ) ??
                      false

                    return (
                      <label
                        key={
                          role.id
                        }
                      >
                        <input
                          type="checkbox"
                          checked={
                            checked
                          }
                          onChange={event =>
                            setCreateForm(
                              current => {
                                const currentRoles =
                                  current.roleIds ??
                                  []

                                return {
                                  ...current,

                                  roleIds:
                                    event.target.checked
                                      ? [
                                          ...currentRoles,
                                          role.id,
                                        ]
                                      : currentRoles.filter(
                                          id =>
                                            id !==
                                            role.id,
                                        ),
                                }
                              },
                            )
                          }
                        />

                        <div>
                          <strong>
                            {role.name}
                          </strong>

                          <span>
                            {role.description ??
                              'Sin descripción'}
                          </span>
                        </div>
                      </label>
                    )
                  },
                )}
              </div>
            </section>

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
                  void createUser()
                }
              >
                {isSaving
                  ? 'Creando...'
                  : 'Crear usuario'}
              </button>
            </footer>
          </div>
        </div>
      )}
    </div>
  )
}