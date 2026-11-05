export function resolveRequiredWorkspacePermission(
  pathname: string,
  search: string,
): string | null {
  const requestedWorkspace = new URLSearchParams(search)
    .get('workspace')
    ?.trim()
    .toLowerCase()

  if (requestedWorkspace === 'windows') {
    return 'workspace.windows.view'
  }

  if (requestedWorkspace === 'android') {
    return 'workspace.android.view'
  }

  if (requestedWorkspace === 'helpdesk') {
    return 'workspace.helpdesk.view'
  }

  if (requestedWorkspace === 'administration') {
    return 'workspace.administration.view'
  }

  if (requestedWorkspace === 'ponches') {
    return 'workspace.ponches.view'
  }

  if (requestedWorkspace === 'inventory') {
    return null
  }

  if (
    pathname === '/remote' ||
    pathname.startsWith('/remote/') ||
    (pathname.startsWith('/devices/') &&
      pathname.endsWith('/control-center'))
  ) {
    return 'workspace.windows.view'
  }

  if (
    pathname === '/android' ||
    pathname.startsWith('/android/') ||
    pathname === '/kiosk' ||
    pathname.startsWith('/kiosk/') ||
    pathname === '/geofencing' ||
    pathname.startsWith('/geofencing/') ||
    pathname === '/fleet-map' ||
    pathname.startsWith('/fleet-map/')
  ) {
    return 'workspace.android.view'
  }

  if (
    pathname === '/settings' ||
    pathname.startsWith('/settings/') ||
    pathname === '/users' ||
    pathname.startsWith('/users/') ||
    pathname === '/roles' ||
    pathname.startsWith('/roles/') ||
    pathname === '/sites' ||
    pathname.startsWith('/sites/') ||
    pathname === '/audit' ||
    pathname.startsWith('/audit/')
  ) {
    return 'workspace.administration.view'
  }

  if (
    pathname === '/helpdesk' ||
    pathname.startsWith('/helpdesk/') ||
    pathname === '/my-support' ||
    pathname.startsWith('/my-support/')
  ) {
    return 'workspace.helpdesk.view'
  }

  if (
    pathname === '/ponches' ||
    pathname.startsWith('/ponches/')
  ) {
    return 'workspace.ponches.view'
  }

  return null
}
