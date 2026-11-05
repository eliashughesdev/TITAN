import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'

import { InventoryDataTable } from './InventoryDataTable'

const rows = Array.from({ length: 60 }, (_, i) => ({ id: `a${i}`, assetTag: `TI-${i}`, name: `Equipo ${i}` }))
const columns: Array<[string, string]> = [['assetTag', 'Tag'], ['name', 'Nombre']]

describe('InventoryDataTable', () => {
  beforeEach(() => localStorage.clear())

  it('pagina de 25 en 25 y navega', () => {
    render(<InventoryDataTable rows={rows} columns={columns} tableKey="t1" />)

    expect(screen.getByText('1–25 de 60')).toBeInTheDocument()
    expect(screen.queryByText('TI-30')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Página siguiente' }))
    expect(screen.getByText('26–50 de 60')).toBeInTheDocument()
    expect(screen.getByText('TI-30')).toBeInTheDocument()
  })

  it('permite volver a mostrar una columna oculta', () => {
    render(<InventoryDataTable rows={rows} columns={columns} tableKey="t2" />)

    fireEvent.click(screen.getByRole('button', { name: 'Seleccionar columnas visibles' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Nombre' }))
    expect(screen.queryByRole('columnheader', { name: 'Nombre' })).not.toBeInTheDocument()

    // Antes del fix la columna oculta desaparecia del panel.
    fireEvent.click(screen.getByRole('checkbox', { name: 'Nombre' }))
    expect(screen.getByRole('columnheader', { name: 'Nombre' })).toBeInTheDocument()
  })

  it('no permite ocultar la ultima columna y no persiste el estado del panel', () => {
    render(<InventoryDataTable rows={rows} columns={columns} tableKey="t3" />)

    fireEvent.click(screen.getByRole('button', { name: 'Seleccionar columnas visibles' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Nombre' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Tag' }))

    expect(screen.getByRole('columnheader', { name: 'Tag' })).toBeInTheDocument()
    expect(localStorage.getItem('t3.hiddenColumns')).toBe('["name"]')
  })

  it('el buscador reinicia la pagina', () => {
    render(<InventoryDataTable rows={rows} columns={columns} tableKey="t4" />)

    fireEvent.click(screen.getByRole('button', { name: 'Última página' }))
    fireEvent.change(screen.getByLabelText('Buscar en la tabla'), { target: { value: 'TI-5' } })

    expect(screen.getByText('TI-5')).toBeInTheDocument()
    expect(screen.getByText(/^1–\d+ de \d+$/)).toBeInTheDocument()
  })
})
