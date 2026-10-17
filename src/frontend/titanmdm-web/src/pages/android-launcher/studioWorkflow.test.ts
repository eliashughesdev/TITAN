import { describe, expect, it } from 'vitest'
import { launcherWorkflow } from './studioWorkflow'

const base = {
  dirty: false,
  saved: false,
  active: false,
  published: false,
  assigned: false,
  applied: false,
  failed: false,
}

describe('launcher workflow', () => {
  it('does not treat a Google publication as applied', () => {
    expect(launcherWorkflow({ ...base, saved: true, active: true, published: true })).toBe('published')
  })

  it('keeps unsaved edits ahead of a previous publication', () => {
    expect(launcherWorkflow({ ...base, dirty: true, published: true, applied: true })).toBe('dirty')
  })

  it('shows applying only after assignment and before launcher confirmation', () => {
    expect(launcherWorkflow({ ...base, published: true, assigned: true })).toBe('applying')
    expect(launcherWorkflow({ ...base, published: true, assigned: true, applied: true })).toBe('applied')
  })
})
