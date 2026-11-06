import type { Asset, CatalogItem } from './inventoryApi'

/**
 * Impresora portátil Honeywell RP4 (RP4, RP4D...). Misma regla que el API
 * (InventoryService.IsRp4Printer): categoría, nombre o modelo contiene "RP4".
 */
export const isRp4Printer = (asset: Asset, categoryName?: string | null): boolean =>
  [categoryName, asset.name, asset.hardwareModel].some(value => (value ?? '').toUpperCase().includes('RP4'))

/** Impresoras RP4 libres para entregar junto con la Handheld seleccionada. */
export const availableRp4Printers = (
  assets: readonly Asset[],
  categories: readonly CatalogItem[],
  handheldId?: string | null,
): Asset[] => {
  const categoryNames = new Map(categories.map(category => [category.id, category.name]))

  return assets
    .filter(asset => !asset.deleted)
    .filter(asset => asset.id !== handheldId)
    .filter(asset => asset.status.toLowerCase() !== 'asignado')
    .filter(asset => isRp4Printer(asset, asset.categoryId ? categoryNames.get(asset.categoryId) : null))
    .sort((a, b) => a.assetTag.localeCompare(b.assetTag))
}

export const printerLabel = (asset: Asset): string =>
  [asset.assetTag, asset.name, asset.serialNumber ? `S/N ${asset.serialNumber}` : null].filter(Boolean).join(' · ')
