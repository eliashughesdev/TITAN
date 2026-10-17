import {
  AxiosError,
  AxiosHeaders,
} from 'axios'
import { describe, expect, it } from 'vitest'

import {
  describeAutomationError,
  formatDate,
  formatDuration,
  isPlatformUnavailable,
} from './automationUi'

function httpError(status: number): AxiosError {
  return new AxiosError(
    'Request failed',
    undefined,
    undefined,
    undefined,
    {
      data: {},
      status,
      statusText: 'Error',
      headers: new AxiosHeaders(),
      config: {
        headers: new AxiosHeaders(),
      },
    },
  )
}

describe('automationUi', () => {
  it('formats empty, millisecond, and second durations', () => {
    expect(formatDuration(null)).toBe('—')
    expect(formatDuration(999)).toBe('999 ms')
    expect(formatDuration(1250)).toBe('1.25 s')
  })

  it('formats missing and invalid dates safely', () => {
    expect(formatDate(null)).toBe('Nunca')
    expect(formatDate('not-a-date')).toBe('N/D')
  })

  it('identifies an unavailable enterprise platform', () => {
    const error = httpError(503)

    expect(isPlatformUnavailable(error)).toBe(true)
    expect(
      describeAutomationError(error, 'fallback'),
    ).toContain('HTTP 503')
  })

  it('maps authorization failures without exposing internals', () => {
    expect(
      describeAutomationError(
        httpError(403),
        'fallback',
      ),
    ).toBe('No tienes permisos para esta operación.')
  })
})
