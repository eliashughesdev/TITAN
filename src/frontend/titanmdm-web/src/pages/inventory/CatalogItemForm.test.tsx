import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Catalogs } from './inventoryApi'
import { ModalCloseContext } from './itamModalContext'

const api = vi.hoisted(() => ({
  createCatalogItem: vi.fn(),
  updateCatalogItem: vi.fn(),
}))

vi.mock('./inventoryApi', () => ({ itamApi: api }))

const { CatalogItemForm } = await import('./InventoryScreens')

const catalogs: Catalogs = {
  categories: [{ id: 'c1', name: 'Handheld' }],
  branches: [],
  departments: [],
  manufacturers: [{ id: 'm1', name: 'Honeywell' }],
  suppliers: [],
  models: [],
  locations: [],
}

function renderForm(onSaved = vi.fn(), initial: Parameters<typeof CatalogItemForm>[0]['initial'] = null) {
  const close = vi.fn((afterClose?: () => void) => afterClose?.())
  render(
    <ModalCloseContext.Provider value={close}>
      <CatalogItemForm kind="models" initial={initial} catalogs={catalogs} onSaved={onSaved} />
    </ModalCloseContext.Provider>,
  )
  return { close, onSaved }
}

describe('CatalogItemForm', () => {
  beforeEach(() => {
    api.createCatalogItem.mockReset()
    api.updateCatalogItem.mockReset()
  })

  it('crea un modelo nuevo con fabricante y categoria del catalogo', async () => {
    api.createCatalogItem.mockResolvedValue({ id: 'x1', name: 'CT40' })
    const { onSaved, close } = renderForm()

    fireEvent.change(screen.getByLabelText(/Modelo \*/), { target: { value: ' CT40 ' } })
    fireEvent.change(screen.getByLabelText('Fabricante'), { target: { value: 'm1' } })
    fireEvent.change(screen.getByLabelText('Categoría'), { target: { value: 'c1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear' }))

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith({ id: 'x1', name: 'CT40' }))
    expect(api.createCatalogItem).toHaveBeenCalledWith('models', {
      name: 'CT40',
      modelNumber: null,
      manufacturerId: 'm1',
      categoryId: 'c1',
    })
    expect(close).toHaveBeenCalled()
  })

  it('exige nombre y muestra el error del API (duplicado)', async () => {
    api.updateCatalogItem.mockRejectedValue({ response: { data: { message: 'Ya existe un registro llamado "CT40".' } } })
    const { onSaved } = renderForm(vi.fn(), { id: 'x1', name: '' })

    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('El nombre es obligatorio.')

    fireEvent.change(screen.getByLabelText(/Modelo \*/), { target: { value: 'CT40' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByText('Ya existe un registro llamado "CT40".')).toBeInTheDocument()
    expect(onSaved).not.toHaveBeenCalled()
  })
})
