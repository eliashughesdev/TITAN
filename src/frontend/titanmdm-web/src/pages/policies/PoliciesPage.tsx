import {
  Archive,
  CheckCircle2,
  ClipboardCheck,
  Edit3,
  MoreVertical,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Smartphone,
  XCircle,
  Laptop,
  Power,
  PowerOff,
} from 'lucide-react'
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { useNavigate } from 'react-router-dom'

import {
  policiesApi,
  type Policy,
} from '../../api/policiesApi'

import './PoliciesPage.css'

function formatDate(
  value: string | null,
): string {
  if (!value) {
    return 'N/D'
  }

  const date = new Date(value)

  return Number.isNaN(date.getTime())
    ? 'N/D'
    : date.toLocaleString()
}

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

    if (response?.data?.message) {
      return response.data.message
    }
  }

  return 'Ocurrió un error procesando la solicitud.'
}

export function PoliciesPage() {
  const navigate = useNavigate()

  const [policies, setPolicies] =
    useState<Policy[]>([])

  const [loading, setLoading] =
    useState(true)

  const [search, setSearch] =
    useState('')

  const [platform, setPlatform] =
    useState('')

  const [status, setStatus] =
    useState('')

  const [error, setError] =
    useState<string | null>(null)

  const [message, setMessage] =
    useState<string | null>(null)

  const [actionPolicyId, setActionPolicyId] =
    useState<string | null>(null)

  const [openMenuId, setOpenMenuId] =
    useState<string | null>(null)

  const menuRef =
  useRef<HTMLDivElement | null>(null)

const [menuPosition, setMenuPosition] =
  useState({
    top: 0,
    right: 0,
  })

const openPolicyMenu = (
  policyId: string,
  button: HTMLButtonElement,
) => {
  if (openMenuId === policyId) {
    setOpenMenuId(null)
    return
  }

  const rect =
    button.getBoundingClientRect()

  const menuHeight = 190
  const gap = 8

  const availableBelow =
    window.innerHeight -
    rect.bottom

  const shouldOpenUpward =
    availableBelow <
    menuHeight + 20

  const top =
    shouldOpenUpward
      ? Math.max(
          12,
          rect.top -
            menuHeight -
            gap,
        )
      : rect.bottom + gap

  const right =
    Math.max(
      12,
      window.innerWidth -
        rect.right,
    )

  setMenuPosition({
    top,
    right,
  })

  setOpenMenuId(policyId)
}
  const loadPolicies =
    useCallback(async () => {
      try {
        setLoading(true)
        setError(null)

        const result =
          await policiesApi.getAll(
            platform,
            status,
          )

        setPolicies(result)
      } catch (loadError) {
        console.error(loadError)

        setError(
          getErrorMessage(loadError),
        )
      } finally {
        setLoading(false)
      }
    }, [platform, status])

  useEffect(() => {
    void loadPolicies()
  }, [loadPolicies])

  useEffect(() => {
    document.title =
      'Políticas | TitanMDM'
  }, [])

  useEffect(() => {
  if (!openMenuId) {
    return
  }

  const handleMouseDown = (
    event: MouseEvent,
  ) => {
    const target =
      event.target as Node

    if (
      menuRef.current &&
      !menuRef.current.contains(
        target,
      )
    ) {
      const element =
        target instanceof Element
          ? target
          : null

      if (
        !element?.closest(
          '.policy-menu-button',
        )
      ) {
        setOpenMenuId(null)
      }
    }
  }

  const handleKeyDown = (
    event: KeyboardEvent,
  ) => {
    if (event.key === 'Escape') {
      setOpenMenuId(null)
    }
  }

  const closeOnViewportChange =
    () => {
      setOpenMenuId(null)
    }

  document.addEventListener(
    'mousedown',
    handleMouseDown,
  )

  document.addEventListener(
    'keydown',
    handleKeyDown,
  )

  window.addEventListener(
    'resize',
    closeOnViewportChange,
  )

  window.addEventListener(
    'scroll',
    closeOnViewportChange,
    true,
  )

  return () => {
    document.removeEventListener(
      'mousedown',
      handleMouseDown,
    )

    document.removeEventListener(
      'keydown',
      handleKeyDown,
    )

    window.removeEventListener(
      'resize',
      closeOnViewportChange,
    )

    window.removeEventListener(
      'scroll',
      closeOnViewportChange,
      true,
    )
  }
}, [openMenuId])

  const filteredPolicies =
    useMemo(() => {
      const term =
        search.trim().toLowerCase()

      if (!term) {
        return policies
      }

      return policies.filter(
        (policy) =>
          policy.name
            .toLowerCase()
            .includes(term) ||
          policy.description
            ?.toLowerCase()
            .includes(term),
      )
    }, [policies, search])

  const summary =
    useMemo(
      () => ({
        total: policies.length,

        active: policies.filter(
          (policy) =>
            policy.status === 'Active',
        ).length,

        draft: policies.filter(
          (policy) =>
            policy.status === 'Draft',
        ).length,

        assigned: policies.reduce(
          (total, policy) =>
            total +
            policy.assignedDevices,
          0,
        ),
      }),
      [policies],
    )

  const executeStateAction =
    async (
      policy: Policy,
      action:
        | 'activate'
        | 'disable'
        | 'archive',
    ) => {
      try {
        setActionPolicyId(policy.id)
        setOpenMenuId(null)
        setError(null)
        setMessage(null)

        if (action === 'activate') {
          await policiesApi.activate(
            policy.id,
          )
        }

        if (action === 'disable') {
          await policiesApi.disable(
            policy.id,
          )
        }

        if (action === 'archive') {
          await policiesApi.archive(
            policy.id,
          )
        }

        setMessage(
          `La política "${policy.name}" fue actualizada correctamente.`,
        )

        await loadPolicies()
      } catch (actionError) {
        console.error(actionError)

        setError(
          getErrorMessage(actionError),
        )
      } finally {
        setActionPolicyId(null)
      }
    }

  return (
    <div className="policies-page">
      <header className="policies-header">
        <div>
          <div className="policies-title">
            <div className="policies-title__icon">
              <ClipboardCheck size={23} />
            </div>

            <div>
              <h1>Políticas</h1>

              <p>
                Configura, versiona y
                despliega políticas
                corporativas sobre los
                dispositivos administrados.
              </p>
            </div>
          </div>
        </div>

        <div className="policies-header__actions">
          <button
            type="button"
            className="policy-button policy-button--secondary"
            disabled={loading}
            onClick={() => {
              void loadPolicies()
            }}
          >
            <RefreshCw size={16} />
            Actualizar
          </button>

          <button
            type="button"
            className="policy-button policy-button--primary"
            onClick={() =>
              navigate('/policies/new')
            }
          >
            <Plus size={17} />
            Nueva política
          </button>
        </div>
      </header>

      {error && (
        <div className="policy-notice policy-notice--error">
          <XCircle size={17} />
          {error}
        </div>
      )}

      {message && (
        <div className="policy-notice policy-notice--success">
          <CheckCircle2 size={17} />
          {message}
        </div>
      )}

      <section className="policy-kpis">
        <article>
          <ShieldCheck size={20} />
          <div>
            <span>Total</span>
            <strong>
              {summary.total}
            </strong>
          </div>
        </article>

        <article>
          <CheckCircle2 size={20} />
          <div>
            <span>Activas</span>
            <strong>
              {summary.active}
            </strong>
          </div>
        </article>

        <article>
          <Edit3 size={20} />
          <div>
            <span>Borradores</span>
            <strong>
              {summary.draft}
            </strong>
          </div>
        </article>

        <article>
          <Laptop size={20} />
          <div>
            <span>
              Asignaciones
            </span>
            <strong>
              {summary.assigned}
            </strong>
          </div>
        </article>
      </section>

      <section className="policy-toolbar">
        <div className="policy-search">
          <Search size={16} />

          <input
            value={search}
            onChange={(event) =>
              setSearch(
                event.target.value,
              )
            }
            placeholder="Buscar políticas..."
          />
        </div>

        <select
          value={platform}
          onChange={(event) =>
            setPlatform(
              event.target.value,
            )
          }
        >
          <option value="">
            Todas las plataformas
          </option>
          <option value="Windows">
            Windows
          </option>
          <option value="Android">
            Android
          </option>
        </select>
        

        <select
          value={status}
          onChange={(event) =>
            setStatus(
              event.target.value,
            )
          }
        >
          <option value="">
            Todos los estados
          </option>
          <option value="Draft">
            Borrador
          </option>
          <option value="Active">
            Activa
          </option>
          <option value="Disabled">
            Deshabilitada
          </option>
          <option value="Archived">
            Archivada
          </option>
        </select>
      </section>

      <section className="policy-table-card">
        <div className="policy-table-wrapper">
          <table className="policy-table">
            <thead>
              <tr>
                <th>Política</th>
                <th>Plataforma</th>
                <th>Estado</th>
                <th>Versión</th>
                <th>Dispositivos</th>
                <th>Actualizada</th>
                <th />
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td
                    colSpan={7}
                    className="policy-table-empty"
                  >
                    <RefreshCw
                      size={19}
                      className="policy-spin"
                    />
                    Cargando políticas...
                  </td>
                </tr>
              ) : filteredPolicies.length === 0 ? (
                <tr>
                  <td
                    colSpan={7}
                    className="policy-table-empty"
                  >
                    <ClipboardCheck
                      size={28}
                    />

                    <strong>
                      No hay políticas
                    </strong>

                    <span>
                      Crea la primera
                      política de
                      administración.
                    </span>
                  </td>
                </tr>
              ) : (
                filteredPolicies.map(
                  (policy) => (
                    <tr key={policy.id}>
                      <td>
                        <button
                          type="button"
                          className="policy-name"
                          onClick={() =>
                            navigate(
                              `/policies/${policy.id}`,
                            )
                          }
                        >
                          <span className="policy-platform-icon">
                            {policy.platform ===
                            'Windows' ? (
                              <Laptop
                                size={17}
                              />
                            ) : (
                              <Smartphone
                                size={17}
                              />
                            )}
                          </span>

                          <span>
                            <strong>
                              {policy.name}
                            </strong>

                            <small>
                              {policy.description ??
                                'Sin descripción'}
                            </small>
                          </span>
                        </button>
                      </td>

                      <td>
                        {policy.platform}
                      </td>

                      <td>
                        <span
                          className={
                            `policy-status ` +
                            `policy-status--${policy.status.toLowerCase()}`
                          }
                        >
                          {policy.status}
                        </span>
                      </td>

                      <td>
                        v
                        {
                          policy.currentVersion
                        }
                      </td>

                      <td>
                        {
                          policy.assignedDevices
                        }
                      </td>

                      <td>
                        {formatDate(
                          policy.updatedAtUtc,
                        )}
                      </td>

                      <td className="policy-actions-cell">
  <button
    type="button"
    className="policy-menu-button"
    aria-label={
      `Acciones para ${policy.name}`
    }
    aria-haspopup="menu"
    aria-expanded={
      openMenuId === policy.id
    }
    disabled={
      actionPolicyId ===
      policy.id
    }
    onClick={(event) =>
      openPolicyMenu(
        policy.id,
        event.currentTarget,
      )
    }
  >
    <MoreVertical size={17} />
  </button>

  {openMenuId ===
    policy.id && (
    <div
      ref={menuRef}
      role="menu"
      className="policy-menu"
      style={{
        top: menuPosition.top,
        right: menuPosition.right,
      }}
    >
      <div className="policy-menu__header">
        <span>Acciones</span>

        <small>
          {policy.name}
        </small>
      </div>

      <div className="policy-menu__content">
        <button
          type="button"
          role="menuitem"
          onClick={() => {
            setOpenMenuId(null)

            navigate(
              `/policies/${policy.id}`,
            )
          }}
        >
          <span className="policy-menu__icon">
            <Edit3 size={15} />
          </span>

          <span className="policy-menu__label">
            <strong>
              Editar
            </strong>

            <small>
              Modificar configuración
            </small>
          </span>
        </button>

        {policy.status !==
          'Active' &&
          policy.status !==
            'Archived' && (
            <button
              type="button"
              role="menuitem"
              onClick={() => {
                void executeStateAction(
                  policy,
                  'activate',
                )
              }}
            >
              <span
                className={
                  'policy-menu__icon ' +
                  'policy-menu__icon--success'
                }
              >
                <Power size={15} />
              </span>

              <span className="policy-menu__label">
                <strong>
                  Activar
                </strong>

                <small>
                  Habilitar política
                </small>
              </span>
            </button>
          )}

        {policy.status ===
          'Active' && (
          <button
            type="button"
            role="menuitem"
            onClick={() => {
              void executeStateAction(
                policy,
                'disable',
              )
            }}
          >
            <span className="policy-menu__icon">
              <PowerOff size={15} />
            </span>

            <span className="policy-menu__label">
              <strong>
                Deshabilitar
              </strong>

              <small>
                Detener aplicación
              </small>
            </span>
          </button>
        )}

        {policy.status !==
          'Archived' && (
          <>
            <div className="policy-menu__separator" />

            <button
              type="button"
              role="menuitem"
              className="danger"
              onClick={() => {
                void executeStateAction(
                  policy,
                  'archive',
                )
              }}
            >
              <span
                className={
                  'policy-menu__icon ' +
                  'policy-menu__icon--danger'
                }
              >
                <Archive size={15} />
              </span>

              <span className="policy-menu__label">
                <strong>
                  Archivar
                </strong>

                <small>
                  Retirar esta política
                </small>
              </span>
            </button>
          </>
        )}
      </div>
    </div>
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
    </div>
  )
}