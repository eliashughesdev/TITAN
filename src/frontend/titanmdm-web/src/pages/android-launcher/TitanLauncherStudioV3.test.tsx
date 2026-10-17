import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { createLauncherDraft } from './launcherModel'
import { TitanLauncherStudioV3 } from './TitanLauncherStudioV3'

describe('Titan Launcher Studio V3', () => {
  it('switches the inspector without showing every field at once', async () => {
    const onTool = vi.fn()
    render(
      <TitanLauncherStudioV3
        name="Tiendas"
        description=""
        draft={createLauncherDraft()}
        dirty
        canManage
        working={false}
        saved={false}
        active={false}
        published={false}
        assigned={false}
        applied={false}
        version="nueva"
        tool="identity"
        onTool={onTool}
        onName={vi.fn()}
        onDescription={vi.fn()}
        onDraft={vi.fn()}
        onApplications={vi.fn()}
        onSave={vi.fn()}
        onPublish={vi.fn()}
        onDeployments={vi.fn()}
        appsPanel={null}
      />,
    )

    expect(screen.getByLabelText('Compania')).toBeTruthy()
    expect(screen.queryByLabelText('Columnas')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Distribucion' }))
    expect(onTool).toHaveBeenCalledWith('layout')
  })

  it('keeps save disabled until there are pending changes', () => {
    render(
      <TitanLauncherStudioV3
        name="Tiendas"
        description=""
        draft={createLauncherDraft()}
        dirty={false}
        canManage
        working={false}
        saved
        active
        published={false}
        assigned={false}
        applied={false}
        version="3"
        tool="publish"
        onTool={vi.fn()}
        onName={vi.fn()}
        onDescription={vi.fn()}
        onDraft={vi.fn()}
        onApplications={vi.fn()}
        onSave={vi.fn()}
        onPublish={vi.fn()}
        onDeployments={vi.fn()}
        appsPanel={null}
      />,
    )
    expect(screen.getByRole('button', { name: /^Guardar$/ })).toBeDisabled()
  })
})
