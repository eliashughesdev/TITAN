
import {
  useEffect,
  useState,
} from 'react'

import {
  AnimatePresence,
  motion,
  useReducedMotion,
} from 'motion/react'

import {
  Menu,
  X,
} from 'lucide-react'

import {
  Outlet,
  useLocation,
} from 'react-router-dom'

import apiClient from '../../api/apiClient'

import { useAuth } from '../../auth/AuthContext'

import {
  TitanAssistantProvider,
} from '../../assistant/context/TitanAssistantContext'

import {
  TitanAssistant,
} from '../../assistant/components/TitanAssistant'

import FiorellaController from '../../pages/ponches/fiorella/FiorellaController'

import {
  WorkspaceProvider,
} from '../../workspace/WorkspaceContext'

import { Header } from './Header'
import { ModuleNavigation } from './ModuleNavigation'
import { Sidebar } from './Sidebar'

import './TitanEnterpriseLayout.css'

type WorkspaceName =
  | 'windows'
  | 'android'
  | 'helpdesk'
  | 'ponches'
  | 'administration'

interface MobileMenuState {
  open: boolean
  routeKey: string
}

function resolveWorkspace(
  pathname: string,
  search: string,
): WorkspaceName {
  if (pathname.startsWith('/ponches')) {
    return 'ponches'
  }

  if (
    pathname.startsWith('/helpdesk') ||
    pathname.startsWith('/my-support')
  ) {
    return 'helpdesk'
  }

  if (
    pathname.startsWith('/users') ||
    pathname.startsWith('/roles') ||
    pathname.startsWith('/settings') ||
    pathname.startsWith('/sites') ||
    pathname.startsWith('/audit')
  ) {
    return 'administration'
  }

  if (
    pathname.startsWith('/android') ||
    pathname.startsWith('/kiosk') ||
    pathname.startsWith('/geofencing') ||
    pathname.startsWith('/fleet-map')
  ) {
    return 'android'
  }

  const requestedWorkspace =
    new URLSearchParams(search)
      .get('workspace')

  if (requestedWorkspace === 'android') {
    return 'android'
  }

  return 'windows'
}

export function AppLayout() {
  const location = useLocation()
  const reducedMotion = useReducedMotion()

  const { hasPermission } = useAuth()

  const routeKey =
    `${location.pathname}${location.search}`

  const workspace = resolveWorkspace(
    location.pathname,
    location.search,
  )

  const isPonches =
    location.pathname === '/ponches' ||
    location.pathname.startsWith('/ponches/')

  const [
    sidebarCollapsed,
    setSidebarCollapsed,
  ] = useState(false)

  const [
    mobileMenuState,
    setMobileMenuState,
  ] = useState<MobileMenuState>({
    open: false,
    routeKey: '',
  })

  const [
    assistantAccess,
    setAssistantAccess,
  ] = useState<
    'checking' | 'allowed' | 'denied' | 'error'
  >('checking')

  /*
   * El menú queda asociado a la ruta donde
   * se abrió. Al navegar a otra ruta, se
   * cierra automáticamente sin setState
   * dentro de useEffect.
   */
  const mobileMenuOpen =
    mobileMenuState.open &&
    mobileMenuState.routeKey === routeKey

  function openMobileMenu() {
    setMobileMenuState({
      open: true,
      routeKey,
    })
  }

  function closeMobileMenu() {
    setMobileMenuState({
      open: false,
      routeKey,
    })
  }

  function toggleSidebar() {
    if (
      window.matchMedia(
        '(max-width: 960px)',
      ).matches
    ) {
      if (mobileMenuOpen) {
        closeMobileMenu()
      } else {
        openMobileMenu()
      }

      return
    }

    setSidebarCollapsed(
      current => !current,
    )
  }

  /*
   * Conservamos la autorización original
   * del asistente corporativo.
   *
   * - allowed: acceso confirmado.
   * - denied: el backend respondió explícitamente
   *   que el asistente está deshabilitado.
   * - error: no pudimos verificar (red/estado).
   *   No ocultamos el asistente por un fallo
   *   transitorio; el backend sigue validando.
   */
  useEffect(() => {
    let active = true

    async function checkAssistantAccess() {
      try {
        const response = await apiClient.get<{
          enabled: boolean
        }>(
          '/helpdesk/operations/assistant/me',
        )

        if (active) {
          setAssistantAccess(
            response.data.enabled === true
              ? 'allowed'
              : 'denied',
          )
        }
      } catch {
        if (active) {
          setAssistantAccess('error')
        }
      }
    }

    void checkAssistantAccess()

    return () => {
      active = false
    }
  }, [])

  /*
   * Escape cierra el menú móvil.
   */
  useEffect(() => {
    if (!mobileMenuOpen) {
      return
    }

    function handleEscape(
      event: KeyboardEvent,
    ) {
      if (event.key === 'Escape') {
        closeMobileMenu()
      }
    }

    window.addEventListener(
      'keydown',
      handleEscape,
    )

    return () => {
      window.removeEventListener(
        'keydown',
        handleEscape,
      )
    }
  }, [mobileMenuOpen, routeKey])

  return (
    <WorkspaceProvider>
      <TitanAssistantProvider>
        <div
          className="app-layout titan-enterprise-layout"
          data-titan-workspace={workspace}
        >
          <AnimatePresence>
            {mobileMenuOpen && (
              <motion.button
                key="mobile-backdrop"
                type="button"
                className="titan-mobile-backdrop"
                aria-label="Cerrar navegación"
                initial={{
                  opacity: 0,
                }}
                animate={{
                  opacity: 1,
                }}
                exit={{
                  opacity: 0,
                }}
                transition={{
                  duration: reducedMotion
                    ? 0
                    : 0.18,
                }}
                onClick={closeMobileMenu}
              />
            )}
          </AnimatePresence>

          <div
            className={
              mobileMenuOpen
                ? 'titan-sidebar-container titan-sidebar-container--open'
                : 'titan-sidebar-container'
            }
          >
            <div className="titan-mobile-sidebar-header">
              <strong>
                TitanMDM
              </strong>

              <button
                type="button"
                className="titan-mobile-close"
                aria-label="Cerrar menú"
                onClick={closeMobileMenu}
              >
                <X size={19} />
              </button>
            </div>

            <Sidebar
              collapsed={
                mobileMenuOpen
                  ? false
                  : sidebarCollapsed
              }
              onToggle={toggleSidebar}
            />
          </div>

          <div className="app-layout__main titan-enterprise-main">
            <div className="titan-enterprise-topbar">
              <button
                type="button"
                className="titan-mobile-menu"
                aria-label="Abrir navegación"
                aria-expanded={mobileMenuOpen}
                onClick={openMobileMenu}
              >
                <Menu size={21} />
              </button>

              <div className="titan-enterprise-header">
                <Header />
              </div>
            </div>

            {!isPonches && (
              <div className="titan-enterprise-module-nav">
                <ModuleNavigation />
              </div>
            )}

            <main
              id="titan-main-content"
              className="app-layout__content titan-enterprise-content"
            >
              <motion.div
                key={location.pathname}
                className="titan-enterprise-page"
                initial={
                  reducedMotion
                    ? false
                    : {
                        opacity: 0,
                        y: 8,
                      }
                }
                animate={{
                  opacity: 1,
                  y: 0,
                }}
                transition={{
                  duration: reducedMotion
                    ? 0
                    : 0.2,
                  ease: 'easeOut',
                }}
              >
                <Outlet />
              </motion.div>
            </main>
          </div>

          {(assistantAccess === 'allowed' ||
            assistantAccess === 'error') &&
            !isPonches && (
              <TitanAssistant />
            )}

          {isPonches &&
            hasPermission('ponches.fiorella.use') && (
              <FiorellaController />
            )}
        </div>
      </TitanAssistantProvider>
    </WorkspaceProvider>
  )
}
