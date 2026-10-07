interface Props {
  total: number

  page: number

  pageSize: number

  totalPages: number

  isLoading:
    boolean

  workspaceLabel:
    string

  onPageSizeChange:
    (value: number) => void

  onPrevious:
    () => void

  onNext:
    () => void
}

export function DevicesPagination({
  total,

  page,

  pageSize,

  totalPages,

  isLoading,

  workspaceLabel,

  onPageSizeChange,
  onPrevious,
  onNext,
}: Props) {
  return (
    <footer className="devices-panel-footer">
      <div>
        <span>
          {total}{' '}
          dispositivo
          {total === 1
            ? ''
            : 's'}
        </span>

        <span>
          Página{' '}
          {page}{' '}
          de{' '}
          {Math.max(
            totalPages,
            1,
          )}
        </span>

        <span>
          {workspaceLabel}
        </span>
      </div>

      <div className="devices-pagination">
        <select
          value={
            pageSize
          }
          aria-label="Dispositivos por página"
          onChange={
            event =>
              onPageSizeChange(
                Number(
                  event
                    .target
                    .value,
                ),
              )
          }
        >
          <option value={10}>
            10
          </option>

          <option value={25}>
            25
          </option>

          <option value={50}>
            50
          </option>

          <option value={100}>
            100
          </option>
        </select>

        <button
          type="button"
          disabled={
            page <= 1
            ||
            isLoading
          }
          onClick={
            onPrevious
          }
        >
          Anterior
        </button>

        <button
          type="button"
          disabled={
            totalPages === 0
            ||
            page >=
              totalPages
            ||
            isLoading
          }
          onClick={
            onNext
          }
        >
          Siguiente
        </button>
      </div>
    </footer>
  )
}