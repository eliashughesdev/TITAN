import './RouteFallback.css'

/** Indicador mientras se descarga el chunk de una pagina (React.lazy). */
export function RouteFallback() {
  return (
    <div className="titan-route-fallback" role="status" aria-live="polite">
      <span className="titan-route-fallback__spinner" aria-hidden="true" />
      Cargando módulo…
    </div>
  )
}
