import {
  Bell,
  ChevronDown,
  Grid2X2,
  LogOut,
  Search,
  ShieldCheck,
} from 'lucide-react'

import {
  useEffect,
  useRef,
  useState,
} from 'react'

import {
  useNavigate,
} from 'react-router-dom'

import {
  useAuth,
} from '../../auth/AuthContext'

import {
  useWorkspace,
} from '../../workspace/WorkspaceContext'

export function Header() {
  const {
    user,
    logout,
  } =
    useAuth()

  const {
    activeModule,
    clearWorkspace,
  } =
    useWorkspace()

  const navigate =
    useNavigate()

  const [
    menuOpen,
    setMenuOpen,
  ] =
    useState(false)

  const [
    notificationsOpen,
    setNotificationsOpen,
  ] =
    useState(false)

  const userMenuRef =
    useRef<HTMLDivElement>(
      null,
    )

  const notificationsRef =
    useRef<HTMLDivElement>(
      null,
    )

  const initials =
    `${user?.firstName?.[0] ?? ''}${user?.lastName?.[0] ?? ''}`
      .toUpperCase()
    ||
    'T'

  const fullName =
    `${user?.firstName ?? ''} ${user?.lastName ?? ''}`
      .trim()
    ||
    'Usuario TitanMDM'

  const role =
    user?.roles?.[0]
    ??
    'Usuario'

  /*
   * ============================================================
   * OUTSIDE CLICK
   * ============================================================
   */

  useEffect(
    () => {
      function handleOutsideClick(
        event: MouseEvent,
      ) {
        const target =
          event.target as Node

        if (
          userMenuRef.current
          &&
          !userMenuRef.current.contains(
            target,
          )
        ) {
          setMenuOpen(
            false,
          )
        }

        if (
          notificationsRef.current
          &&
          !notificationsRef.current.contains(
            target,
          )
        ) {
          setNotificationsOpen(
            false,
          )
        }
      }

      document.addEventListener(
        'mousedown',
        handleOutsideClick,
      )

      return () => {
        document.removeEventListener(
          'mousedown',
          handleOutsideClick,
        )
      }
    },
    [],
  )

  /*
   * ============================================================
   * GLOBAL SEARCH SHORTCUT
   * ============================================================
   */

  useEffect(
    () => {
      function handleShortcut(
        event:
          KeyboardEvent,
      ) {
        if (
          (
            event.ctrlKey
            ||
            event.metaKey
          )
          &&
          event.key
            .toLowerCase() ===
            'k'
        ) {
          event.preventDefault()

          document
            .querySelector<HTMLInputElement>(
              '#titan-global-search',
            )
            ?.focus()
        }
      }

      window.addEventListener(
        'keydown',
        handleShortcut,
      )

      return () => {
        window.removeEventListener(
          'keydown',
          handleShortcut,
        )
      }
    },
    [],
  )

  /*
   * ============================================================
   * WORKSPACE CHANGE
   * ============================================================
   */

  const returnToWorkspace =
    () => {
      clearWorkspace()

      navigate(
        '/',
      )
    }

  const ModuleIcon =
    activeModule?.icon
    ??
    Grid2X2

  return (
    <header className="app-header">
      {/* ======================================================
          WORKSPACE
         ====================================================== */}

      <div className="app-header__left">
        <button
          type="button"
          className="header-workspace"
          onClick={
            returnToWorkspace
          }
          title="Cambiar espacio de trabajo"
        >
          <div className="header-workspace__icon">
            <ModuleIcon
              size={17}
            />
          </div>

          <div className="app-header__title">
            <strong>
              {activeModule?.title ??
                'Titan Workspace'}
            </strong>

            <span>
              {activeModule
                ? 'Cambiar módulo'
                : 'Centro de trabajo'}
            </span>
          </div>
        </button>
      </div>

      {/* ======================================================
          SEARCH
         ====================================================== */}

      <div className="app-header__search">
        <Search
          size={18}
        />

        <input
          id="titan-global-search"
          type="search"
          autoComplete="off"
          aria-label="Búsqueda global"
          placeholder={
            activeModule
              ? `Buscar en ${activeModule.shortTitle}...`
              : 'Buscar en TitanMDM...'
          }
        />

        <kbd className="app-header__shortcut">
          Ctrl + K
        </kbd>
      </div>

      {/* ======================================================
          ACTIONS
         ====================================================== */}

      <div className="app-header__actions">
        <div
          className="header-notifications"
          ref={
            notificationsRef
          }
        >
          <button
            type="button"
            className="header-icon-button"
            data-titan-anchor="header.notifications"
            aria-label="Notificaciones"
            aria-expanded={
              notificationsOpen
            }
            onClick={() => {
              setNotificationsOpen(
                current =>
                  !current,
              )

              setMenuOpen(
                false,
              )
            }}
          >
            <Bell
              size={19}
            />

            <span
              className="header-notification-dot"
              aria-hidden="true"
            />
          </button>

          {notificationsOpen && (
            <div className="notification-dropdown">
              <div className="notification-dropdown__header">
                <div>
                  <strong>
                    Notificaciones
                  </strong>

                  <span>
                    Centro de alertas TitanMDM
                  </span>
                </div>

                <span className="notification-count">
                  0
                </span>
              </div>

              <div className="notification-empty">
                <div className="notification-empty__icon">
                  <ShieldCheck
                    size={22}
                  />
                </div>

                <strong>
                  Todo está bajo control
                </strong>

                <span>
                  No existen alertas pendientes.
                </span>
              </div>
            </div>
          )}
        </div>

        <div className="header-divider" />

        {/* ====================================================
            USER
           ==================================================== */}

        <div
          className="user-menu-container"
          ref={
            userMenuRef
          }
        >
          <button
            type="button"
            className="user-menu-trigger"
            data-titan-anchor="header.user"
            aria-expanded={
              menuOpen
            }
            onClick={() => {
              setMenuOpen(
                current =>
                  !current,
              )

              setNotificationsOpen(
                false,
              )
            }}
          >
            <div className="app-header__avatar">
              {initials}
            </div>

            <div className="app-header__user-info">
              <strong>
                {fullName}
              </strong>

              <span>
                {role}
              </span>
            </div>

            <ChevronDown
              size={16}
              className={
                menuOpen
                  ? 'user-menu-chevron user-menu-chevron--open'
                  : 'user-menu-chevron'
              }
            />
          </button>

          {menuOpen && (
            <div className="user-dropdown">
              <div className="user-dropdown__profile">
                <div className="user-dropdown__avatar">
                  {initials}
                </div>

                <div>
                  <strong>
                    {fullName}
                  </strong>

                  <span>
                    {user?.email}
                  </span>
                </div>
              </div>

              <div className="user-dropdown__role">
                <ShieldCheck
                  size={15}
                />

                <span>
                  Sesión activa como
                  <strong>
                    {' '}
                    {role}
                  </strong>
                </span>
              </div>

              <div className="user-dropdown__divider" />

              <button
                type="button"
                className="user-dropdown__logout"
                onClick={() =>
                  void logout()
                }
              >
                <LogOut
                  size={17}
                />

                Cerrar sesión
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}