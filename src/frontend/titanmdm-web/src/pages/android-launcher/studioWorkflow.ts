export type StudioWorkflow =
  | 'error'
  | 'dirty'
  | 'applying'
  | 'applied'
  | 'assigned'
  | 'published'
  | 'active'
  | 'saved'
  | 'draft'

export function launcherWorkflow(input: {
  dirty: boolean
  saved: boolean
  active: boolean
  published: boolean
  assigned: boolean
  applied: boolean
  failed: boolean
}): StudioWorkflow {
  if (input.failed) return 'error'
  if (input.dirty) return 'dirty'
  if (input.applied) return 'applied'
  if (input.assigned && input.published) return 'applying'
  if (input.assigned) return 'assigned'
  if (input.published) return 'published'
  if (input.active) return 'active'
  if (input.saved) return 'saved'
  return 'draft'
}

export const workflowLabel: Record<StudioWorkflow, string> = {
  error: 'Error',
  dirty: 'Sin guardar',
  applying: 'Aplicando',
  applied: 'Aplicado',
  assigned: 'Asignado',
  published: 'Publicado',
  active: 'Activo',
  saved: 'Guardado',
  draft: 'Borrador',
}
