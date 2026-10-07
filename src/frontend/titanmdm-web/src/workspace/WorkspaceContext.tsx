import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { useLocation } from 'react-router-dom'
import {
  getTitanModule,
  type TitanModuleDefinition,
  type TitanModuleId,
} from '../config/moduleRegistry'

const STORAGE_KEY = 'titanmdm:active-workspace'

interface WorkspaceContextValue {
  activeWorkspaceId: TitanModuleId | null
  activeModule: TitanModuleDefinition | null
  selectWorkspace: (workspaceId: TitanModuleId) => void
  clearWorkspace: () => void
  isWorkspace: (workspaceId: TitanModuleId) => boolean
}

const WorkspaceContext = createContext<WorkspaceContextValue | undefined>(
  undefined,
)

function isValidWorkspace(value: string | null): value is TitanModuleId {
  return (
    value === 'windows' ||
    value === 'android' ||
    value === 'helpdesk' ||
    value === 'administration' ||
    value === 'ponches'
  )
}

function readStoredWorkspace(): TitanModuleId | null {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY)
    return isValidWorkspace(value) ? value : null
  } catch {
    return null
  }
}

function storeWorkspace(value: TitanModuleId): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, value)
  } catch {
    // El almacenamiento del navegador es opcional.
  }
}

function removeStoredWorkspace(): void {
  try {
    window.localStorage.removeItem(STORAGE_KEY)
  } catch {
    // El almacenamiento del navegador es opcional.
  }
}

function resolveWorkspace(
  pathname: string,
  search: string,
): TitanModuleId | null {
  if (pathname === '/') return null

  // La ruta del módulo prevalece sobre cualquier selección anterior.
  if (pathname === '/ponches' || pathname.startsWith('/ponches/')) {
    return 'ponches'
  }

  if (
    pathname === '/helpdesk/entra' ||
    pathname.startsWith('/helpdesk/entra/')
  ) {
    return 'administration'
  }

  if (
    pathname === '/helpdesk' ||
    pathname.startsWith('/helpdesk/') ||
    pathname === '/my-support' ||
    pathname.startsWith('/my-support/')
  ) {
    return 'helpdesk'
  }

  const params = new URLSearchParams(search)
  const selected = params.get('workspace')
  if (isValidWorkspace(selected)) return selected

  if (
    pathname.startsWith('/users') ||
    pathname.startsWith('/roles') ||
    pathname.startsWith('/settings') ||
    pathname.startsWith('/audit')
  ) {
    return 'administration'
  }

  if (pathname.startsWith('/remote')) return 'windows'

  if (
    pathname.startsWith('/kiosk') ||
    pathname.startsWith('/geofencing')
  ) {
    return 'android'
  }

  if (pathname.startsWith('/devices')) {
    const platform = params.get('platform')?.toLowerCase()
    if (platform === 'windows' || platform === 'android') {
      return platform
    }
  }

  return null
}

function applyWorkspaceTheme(module: TitanModuleDefinition | null): void {
  const root = document.documentElement

  root.style.setProperty(
    '--workspace-primary',
    module?.theme.primary ?? '#4169e1',
  )
  root.style.setProperty(
    '--workspace-primary-dark',
    module?.theme.primaryDark ?? '#315edb',
  )
  root.style.setProperty(
    '--workspace-soft',
    module?.theme.soft ?? '#edf2ff',
  )
  root.style.setProperty(
    '--workspace-border',
    module?.theme.border ?? '#dbe4ff',
  )
  root.dataset.workspace = module?.id ?? 'home'
}

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const location = useLocation()
  const [selectedWorkspaceId, setSelectedWorkspaceId] =
    useState<TitanModuleId | null>(readStoredWorkspace)

  const activeWorkspaceId =
    location.pathname === '/'
      ? null
      : resolveWorkspace(location.pathname, location.search) ??
        selectedWorkspaceId

  const activeModule = useMemo(
    () => (activeWorkspaceId ? getTitanModule(activeWorkspaceId) ?? null : null),
    [activeWorkspaceId],
  )

  useEffect(() => {
    applyWorkspaceTheme(activeModule)
  }, [activeModule])

  const selectWorkspace = useCallback((workspaceId: TitanModuleId) => {
    setSelectedWorkspaceId(workspaceId)
    storeWorkspace(workspaceId)
  }, [])

  const clearWorkspace = useCallback(() => {
    setSelectedWorkspaceId(null)
    removeStoredWorkspace()
    applyWorkspaceTheme(null)
  }, [])

  const isWorkspace = useCallback(
    (workspaceId: TitanModuleId) => activeWorkspaceId === workspaceId,
    [activeWorkspaceId],
  )

  const value = useMemo(
    () => ({
      activeWorkspaceId,
      activeModule,
      selectWorkspace,
      clearWorkspace,
      isWorkspace,
    }),
    [activeWorkspaceId, activeModule, selectWorkspace, clearWorkspace, isWorkspace],
  )

  return (
    <WorkspaceContext.Provider value={value}>
      {children}
    </WorkspaceContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useWorkspace(): WorkspaceContextValue {
  const context = useContext(WorkspaceContext)
  if (!context) {
    throw new Error('useWorkspace debe utilizarse dentro de WorkspaceProvider.')
  }
  return context
}