import type { ReconcileResult } from './inventoryApi'

const shortDate = (value?: string | null) => (value ? value.slice(0, 10) : '—')

export function PhysicalReconciliation({
  result,
  onRefresh,
}: {
  result: ReconcileResult
  onRefresh?: () => void
}) {
  const totalSystem = result.systemCount
  const totalCounted = result.countedCount
  const coinciden = result.found.length
  const faltantes = result.missing.length
  const sobrantes = result.extra.length
  const percentage = totalSystem > 0 ? (coinciden / totalSystem) * 100 : 0

  return (
    <div className="physical-reconciliation">
      <div className="reconciliation-summary">
        <div><strong>Sistema:</strong> {totalSystem}</div>
        <div><strong>Contado:</strong> {totalCounted}</div>
        <div><strong>Coinciden:</strong> {coinciden} ({percentage.toFixed(1)}%)</div>
        <div><strong>Faltantes:</strong> {faltantes}</div>
        <div><strong>Sobrantes:</strong> {sobrantes}</div>
      </div>

      <p className="inventory-muted">
        Coinciden = Contados ∩ Sistema · Faltantes = Sistema − Contados · Sobrantes = Contados − Sistema
      </p>

      {onRefresh && (
        <div className="inventory-actions">
          <button type="button" className="inventory-btn inventory-btn--primary" onClick={onRefresh}>
            Recalcular conciliación
          </button>
        </div>
      )}

      <div className="reconciliation-table">
        <table className="inventory-table">
          <thead>
            <tr>
              <th>Activo</th>
              <th>Etiqueta</th>
              <th>Estado</th>
              <th>Última actualización</th>
              <th>Resultado</th>
            </tr>
          </thead>
          <tbody>
            {result.found.map(asset => (
              <tr key={`found-${asset.id}`}>
                <td>{asset.name}</td>
                <td>{asset.assetTag}</td>
                <td>{asset.status}</td>
                <td>{shortDate(asset.updatedAt ?? asset.createdAt)}</td>
                <td><span className="inventory-badge inventory-badge--ok">Coincide</span></td>
              </tr>
            ))}
            {result.missing.map(asset => (
              <tr key={`missing-${asset.id}`}>
                <td>{asset.name}</td>
                <td>{asset.assetTag}</td>
                <td>{asset.status}</td>
                <td>{shortDate(asset.updatedAt ?? asset.createdAt)}</td>
                <td><span className="inventory-badge inventory-badge--danger">Faltante</span></td>
              </tr>
            ))}
            {result.extra.map(count => (
              <tr key={`extra-${count.id}`}>
                <td>{count.assetName ?? 'Activo no registrado'}</td>
                <td>{count.assetTag ?? '—'}</td>
                <td>{count.systemStatus ?? '—'}</td>
                <td>{shortDate(count.countedAt)}</td>
                <td><span className="inventory-badge inventory-badge--warn">Sobrante</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
