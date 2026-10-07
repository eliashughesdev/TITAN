import { useEffect, useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import apiClient from '../../api/apiClient'
import {
  TitanAssistantProvider,
} from '../../assistant/context/TitanAssistantContext'
import {
  TitanAssistant,
} from '../../assistant/components/TitanAssistant'
import {
  HelpdeskAttachmentMount,
} from '../../pages/helpdesk/HelpdeskAttachmentMount'
import {
  WorkspaceProvider,
} from '../../workspace/WorkspaceContext'
import { Header } from './Header'
import { ModuleNavigation } from './ModuleNavigation'
import { Sidebar } from './Sidebar'

export function AppLayout() {
  const location = useLocation()
  const isPonches =
    location.pathname === '/ponches' ||
    location.pathname.startsWith('/ponches/')

  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [assistantAllowed, setAssistantAllowed] = useState(false)

  useEffect(() => {
    let active = true

    async function checkAssistantAccess() {
      try {
        const response = await apiClient.get<{ enabled: boolean }>(
          '/helpdesk/operations/assistant/me',
        )

        if (active) {
          setAssistantAllowed(response.data.enabled === true)
        }
      } catch {
        if (active) setAssistantAllowed(false)
      }
    }

    void checkAssistantAccess()
    return () => {
      active = false
    }
  }, [])

  return (
    <WorkspaceProvider>
      <TitanAssistantProvider>
        <div className="app-layout">
          <Sidebar
            collapsed={sidebarCollapsed}
            onToggle={() => setSidebarCollapsed((value) => !value)}
          />

          <div className="app-layout__main">
            <Header />

            {/* Ponches utiliza navegación dentro de su propio módulo. */}
            {!isPonches && <ModuleNavigation />}

            <main className="app-layout__content">
              <Outlet />
              <HelpdeskAttachmentMount />
            </main>
          </div>

          {/* Fiorella ocupará esta pantalla al integrar su chat original. */}
          {assistantAllowed && !isPonches && <TitanAssistant />}
        </div>
      </TitanAssistantProvider>
    </WorkspaceProvider>
  )
}