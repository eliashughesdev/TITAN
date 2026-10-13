import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react'

import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import {
  sitesApi,
} from '../../api/sitesApi'

import {
  SiteUserAssignmentsPanel,
} from './SiteUserAssignmentsPanel'

vi.mock('../../auth/AuthContext', () => ({
  useAuth: () => ({
    hasPermission: () => true,
  }),
}))

vi.mock('../../api/helpdeskRoutingApi', () => ({
  retryOpenRouting: vi.fn(),
}))

vi.mock('../../api/sitesApi', () => ({
  sitesApi: {
    getAssignmentUsers: vi.fn(),
    assignUsers: vi.fn(),
  },
}))

const site = {
  id: 'site-1',
  code: 'HQ',
  name: 'Headquarters',
  description: null,
  address: null,
  city: null,
  province: null,
  country: null,
  timeZoneId: null,
  isActive: true,
  createdAtUtc: '2026-10-13T10:00:00Z',
  updatedAtUtc: '2026-10-13T10:00:00Z',
}

describe('SiteUserAssignmentsPanel', () => {
  beforeEach(() => {
    vi.mocked(sitesApi.getAssignmentUsers).mockResolvedValue([
      {
        id: 'user-1',
        firstName: 'Ana',
        lastName: 'Tech',
        fullName: 'Ana Tech',
        email: 'ana@example.test',
        jobTitle: null,
        isActive: true,
        siteId: null,
        siteName: null,
        siteLocationId: null,
        siteLocationName: null,
        isTechnician: true,
        groups: ['Network'],
      },
    ])
    vi.mocked(sitesApi.assignUsers).mockResolvedValue()
  })

  it('searches by group and bulk assigns the selected technician', async () => {
    render(
      <SiteUserAssignmentsPanel
        site={site}
        locations={[]}
      />,
    )

    expect(await screen.findByText('Ana Tech')).toBeInTheDocument()
    fireEvent.change(screen.getByPlaceholderText('Nombre, correo o grupo'), {
      target: { value: 'network' },
    })
    expect(screen.getByText('Ana Tech')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('checkbox', {
      name: /Ana Tech/,
    }))
    fireEvent.click(screen.getByRole('button', {
      name: 'Asignar 1',
    }))

    await waitFor(() =>
      expect(sitesApi.assignUsers).toHaveBeenCalledWith(
        ['user-1'],
        'site-1',
        null,
      ),
    )
  })
})
