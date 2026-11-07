import apiClient from '../../api/apiClient'

export type Asset = {
  id: string
  assetTag: string
  name: string
  serialNumber?: string | null
  categoryId?: string | null
  manufacturerId?: string | null
  modelId?: string | null
  branchId?: string | null
  departmentId?: string | null
  locationId?: string | null
  supplierId?: string | null
  status: string
  condition: string
  purchaseDate?: string | null
  purchaseCost?: number | null
  warrantyEnd?: string | null
  depreciationYears?: number | null
  deviceId?: string | null
  macAddress?: string | null
  ipAddress?: string | null
  hostname?: string | null
  specs?: string | null
  notes?: string | null
  photos?: string | null
  documents?: string | null
  assignedTo?: string | null
  assignedToName?: string | null
  deleted: boolean
  deletedAt?: string | null
  createdAt: string
  createdBy?: string | null
  updatedAt?: string | null
  updatedBy?: string | null
  deletedBy?: string | null
  /* Inventario de hardware recuperado de MongoDB. */
  hardwareManufacturer?: string | null
  hardwareModel?: string | null
  osName?: string | null
  processor?: string | null
  ramGb?: number | null
  diskTotalGb?: number | null
  diskFreeGb?: number | null
  lastUser?: string | null
  lastSeen?: string | null
  lastInventoryAt?: string | null
  source?: string | null
}

export type Employee = {
  id: string
  code?: string | null
  name: string
  departmentId?: string | null
  supervisor?: string | null
  email?: string | null
  phone?: string | null
  position?: string | null
  branchId?: string | null
  notes?: string | null
  isActive: boolean
  deleted: boolean
  createdAt: string
}

export type Delivery = {
  id: string
  assetId?: string | null
  assetTag?: string | null
  assetName?: string | null
  serialNumber?: string | null
  assignedToName?: string | null
  employeeCode?: string | null
  departmentId?: string | null
  branchId?: string | null
  expectedReturn?: string | null
  condition?: string | null
  notes?: string | null
  signature?: string | null
  accessories?: string | null
  deliveredBy?: string | null
  deliveredByEmail?: string | null
  deliveryDate: string
  returned: boolean
  returnDate?: string | null
  returnedHandheld: boolean
  handheldReturnDate?: string | null
  returnedRp4d: boolean
  rp4dReturnDate?: string | null
  deleted: boolean
  createdAt: string
  includeRp4d?: boolean
  rp4dId?: string | null
  rp4dSerialNumber?: string | null
}

export type ReturnRecord = {
  id: string
  deliveryId?: string | null
  assetId?: string | null
  assetTag?: string | null
  assetName?: string | null
  returnedBy?: string | null
  employeeCode?: string | null
  receivedBy?: string | null
  condition?: string | null
  notes?: string | null
  signature?: string | null
  returnDate: string
  createdAt: string
}

export type Reception = {
  id: string
  assetId?: string | null
  description?: string | null
  serialNumber?: string | null
  categoryId?: string | null
  deliveredBy?: string | null
  employeeCode?: string | null
  condition?: string | null
  notes?: string | null
  signature?: string | null
  receivedBy?: string | null
  receivedByEmail?: string | null
  receptionDate: string
  createdAt: string
}

export type InventorySession = {
  id: string
  name: string
  branchId?: string | null
  departmentId?: string | null
  notes?: string | null
  status: string
  counted: number
  report?: string | null
  createdBy?: string | null
  deleted: boolean
  createdAt: string
}

export type InventoryCount = {
  id: string
  sessionId?: string | null
  assetTag?: string | null
  assetId?: string | null
  assetName?: string | null
  found: boolean
  systemStatus?: string | null
  condition?: string | null
  locationNote?: string | null
  countedBy?: string | null
  countedAt?: string | null
}

export type AssetHistory = {
  id: string
  assetId?: string | null
  action?: string | null
  detail?: string | null
  beforeJson?: string | null
  afterJson?: string | null
  userEmail?: string | null
  userName?: string | null
  timestamp: string
}

export type CatalogItem = {
  id: string
  name: string
  [key: string]: unknown
}

export type CatalogKind =
  | 'categories'
  | 'branches'
  | 'departments'
  | 'manufacturers'
  | 'suppliers'
  | 'models'
  | 'locations'

export type CatalogItemInput = Partial<Record<string, string | number | null>> & { name: string }

export type Catalogs = {
  categories: CatalogItem[]
  branches: CatalogItem[]
  departments: CatalogItem[]
  manufacturers: CatalogItem[]
  suppliers: CatalogItem[]
  models: CatalogItem[]
  locations: CatalogItem[]
}

export type NameCount = { name: string; count: number }

export type Dashboard = {
  totalAssets: number
  assignedAssets: number
  availableAssets: number
  handHeldAssets: number
  damagedAssets: number
  retiredAssets: number
  totalEmployees: number
  activeDeliveries: number
  returnedDeliveries: number
  pendingReceptions: number
  warrantiesExpiringSoon: number
  totalPurchaseCost: number
  accumulatedDepreciation: number
  bookValue: number
  atRiskAssets: number
  totalBranches: number
  rotationRate: number
  byStatus: NameCount[]
  byCondition: NameCount[]
  byCategory: NameCount[]
  byBranch: NameCount[]
  byManufacturer: NameCount[]
  byDepartment: NameCount[]
  monthlyAssignments: NameCount[]
}

export type ReconcileResult = {
  sessionId: string
  systemCount: number
  countedCount: number
  found: Asset[]
  missing: Asset[]
  extra: InventoryCount[]
}

export type AssetDepreciation = {
  assetId: string
  assetTag: string
  name: string
  purchaseCost: number
  depreciationYears?: number | null
  yearsElapsed: number
  annualDepreciation: number
  accumulatedDepreciation: number
  bookValue: number
}

export type AssetQuery = {
  q?: string
  status?: string
  categoryId?: string
  branchId?: string
  departmentId?: string
  warranty?: string
  includeDeleted?: boolean
}

export type AssetExportFormat = 'excel' | 'pdf'

/** Filtros visibles en la pantalla de Activos: el archivo exportado los respeta todos. */
export type AssetExportQuery = Omit<AssetQuery, 'includeDeleted'>

const fileNameFrom = (disposition: unknown, fallback: string) => {
  if (typeof disposition !== 'string') return fallback
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
  if (utf8) return decodeURIComponent(utf8[1])
  const plain = /filename="?([^";]+)"?/i.exec(disposition)
  return plain ? plain[1] : fallback
}

/** Descarga el archivo binario generado por /api/inventory/assets/export/{excel|pdf}. */
export const downloadAssetExport = async (format: AssetExportFormat, query: AssetExportQuery) => {
  const params = Object.fromEntries(
    Object.entries(query).filter(([, value]) => typeof value === 'string' && value.trim() !== ''),
  )
  const response = await apiClient.get<Blob>(`/inventory/assets/export/${format}`, {
    params,
    responseType: 'blob',
  })
  const extension = format === 'excel' ? 'xlsx' : 'pdf'
  const fileName = fileNameFrom(response.headers['content-disposition'], `activos-inventario.${extension}`)

  const url = URL.createObjectURL(response.data)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export const itamApi = {
  dashboard: () =>
    apiClient.get<Dashboard>('/itam/dashboard').then(r => r.data),

  catalogs: () =>
    apiClient.get<Catalogs>('/itam/catalogs').then(r => r.data),

  createCatalogItem: (kind: CatalogKind, input: CatalogItemInput) =>
    apiClient.post<CatalogItem>(`/itam/catalogs/${kind}`, input).then(r => r.data),

  updateCatalogItem: (kind: CatalogKind, id: string, input: CatalogItemInput) =>
    apiClient.put<CatalogItem>(`/itam/catalogs/${kind}/${encodeURIComponent(id)}`, input).then(r => r.data),

  deleteCatalogItem: (kind: CatalogKind, id: string) =>
    apiClient.delete(`/itam/catalogs/${kind}/${encodeURIComponent(id)}`).then(() => undefined),

  assets: (params?: AssetQuery) =>
    apiClient.get<Asset[]>('/itam/assets', { params }).then(r => r.data),

  asset: (id: string) =>
    apiClient.get<Asset>(`/itam/assets/${id}`).then(r => r.data),

  assetByTag: (assetTag: string) =>
    apiClient.get<Asset>(`/itam/assets/by-tag/${encodeURIComponent(assetTag)}`).then(r => r.data),

  searchAssets: (q: string, limit = 15) =>
    apiClient.get<Asset[]>('/itam/assets/search', { params: { q, limit } }).then(r => r.data),

  depreciation: () =>
    apiClient.get<AssetDepreciation[]>('/itam/assets/depreciation').then(r => r.data),

  assetQr: (id: string) =>
    apiClient.get<{ assetId: string; assetTag: string; payload: string }>(`/itam/assets/${id}/qr`).then(r => r.data),

  createAsset: (payload: Partial<Asset>) =>
    apiClient.post<Asset>('/itam/assets', payload).then(r => r.data),

  updateAsset: (id: string, payload: Partial<Asset>) =>
    apiClient.put<Asset>(`/itam/assets/${id}`, payload).then(r => r.data),

  deleteAsset: (id: string) =>
    apiClient.delete(`/itam/assets/${id}`),

  linkDevice: (id: string, deviceId: string | null) =>
    apiClient.post<Asset>(`/itam/assets/${id}/link-device`, { deviceId }).then(r => r.data),

  employees: (params?: { q?: string; departmentId?: string; page?: number; pageSize?: number; includeInactive?: boolean }) =>
    apiClient.get<Employee[]>('/itam/employees', { params }).then(r => r.data),

  createEmployee: (payload: Partial<Employee>) =>
    apiClient.post<Employee>('/itam/employees', payload).then(r => r.data),

  updateEmployee: (id: string, payload: Partial<Employee>) =>
    apiClient.put<Employee>(`/itam/employees/${id}`, payload).then(r => r.data),

  deleteEmployee: (id: string) =>
    apiClient.delete(`/itam/employees/${id}`),

  deliveries: (params?: { q?: string; returned?: boolean; period?: string }) =>
    apiClient.get<Delivery[]>('/itam/deliveries', { params }).then(r => r.data),

  createDelivery: (payload: Partial<Delivery>) =>
    apiClient.post<Delivery>('/itam/deliveries', payload).then(r => r.data),

  returnHandheld: (deliveryId: string, payload: Partial<ReturnRecord>) =>
    apiClient.post<Delivery>(`/itam/deliveries/${deliveryId}/return-handheld`, payload).then(r => r.data),

  returnRp4d: (deliveryId: string, payload: Partial<ReturnRecord>) =>
    apiClient.post<Delivery>(`/itam/deliveries/${deliveryId}/return-rp4d`, payload).then(r => r.data),

  returns: (params?: { q?: string }) =>
    apiClient.get<ReturnRecord[]>('/itam/returns', { params }).then(r => r.data),

  createReturn: (payload: Partial<ReturnRecord>) =>
    apiClient.post<ReturnRecord>('/itam/returns', payload).then(r => r.data),

  receptions: (params?: { q?: string }) =>
    apiClient.get<Reception[]>('/itam/receptions', { params }).then(r => r.data),

  createReception: (payload: Partial<Reception>) =>
    apiClient.post<Reception>('/itam/receptions', payload).then(r => r.data),

  sessions: () =>
    apiClient.get<InventorySession[]>('/itam/sessions').then(r => r.data),

  createSession: (payload: Partial<InventorySession>) =>
    apiClient.post<InventorySession>('/itam/sessions', payload).then(r => r.data),

  closeSession: (id: string) =>
    apiClient.post<InventorySession>(`/itam/sessions/${id}/close`).then(r => r.data),

  counts: (sessionId: string) =>
    apiClient.get<InventoryCount[]>(`/itam/sessions/${sessionId}/counts`).then(r => r.data),

  saveCount: (sessionId: string, payload: Partial<InventoryCount>) =>
    apiClient.post<InventoryCount>(`/itam/sessions/${sessionId}/counts`, payload).then(r => r.data),

  reconcile: (sessionId: string) =>
    apiClient.get<ReconcileResult>(`/itam/sessions/${sessionId}/reconcile`).then(r => r.data),

  history: (params?: { assetId?: string; q?: string; limit?: number }) =>
    apiClient.get<AssetHistory[]>('/itam/history', { params }).then(r => r.data),
}
