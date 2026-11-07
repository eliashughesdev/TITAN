import type { Asset, CatalogItem } from './inventoryApi'

/**
 * RP4D Honeywell. Misma regla que el API
 * (InventoryService.IsRp4dPrinter): categoría, nombre o modelo contiene "RP4D".
 */
export const isRp4dPrinter = (asset: Asset, categoryName?: string | null): boolean =>
  [categoryName, asset.name, asset.hardwareModel].some(value => (value ?? '').toUpperCase().includes('RP4D'))

/** RP4D libres para entregar junto con la Handheld seleccionada. */
export const availableRp4dPrinters = (
  assets: readonly Asset[],
  categories: readonly CatalogItem[],
  handheldId?: string | null,
): Asset[] => {
  const categoryNames = new Map(categories.map(category => [category.id, category.name]))

  return assets
    .filter(asset => !asset.deleted)
    .filter(asset => asset.id !== handheldId)
    .filter(asset => asset.status.toLowerCase() !== 'asignado')
    .filter(asset => isRp4dPrinter(asset, asset.categoryId ? categoryNames.get(asset.categoryId) : null))
    .sort((a, b) => a.assetTag.localeCompare(b.assetTag))
}

export const rp4dLabel = (asset: Asset): string =>
  [asset.assetTag, asset.name, asset.serialNumber ? `S/N ${asset.serialNumber}` : null].filter(Boolean).join(' · ')
