import {
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import apiClient from './apiClient'
import {
  androidEnterpriseApi,
} from './androidEnterpriseApi'

vi.mock('./apiClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}))

describe('androidEnterpriseApi.executeDeviceAction', () => {
  it('posts a typed action to the real device endpoint', async () => {
    const result = {
      deviceId: 'device-1',
      action: 'ResetPassword',
      status: 'Issued' as const,
      googleCommandName: 'enterprises/e/devices/d/operations/o',
      correlationId: 'correlation-1',
      issuedAtUtc: '2026-10-14T12:00:00Z',
    }

    vi.mocked(apiClient.post).mockResolvedValue({
      data: result,
    })

    await expect(
      androidEnterpriseApi.executeDeviceAction(
        'device-1',
        {
          action: 'reset-password',
          newPassword: 'temporary-secret',
        },
      ),
    ).resolves.toEqual(result)

    expect(apiClient.post).toHaveBeenCalledWith(
      '/android-enterprise/devices/device-1/actions',
      {
        action: 'reset-password',
        newPassword: 'temporary-secret',
      },
    )
  })
})

describe('androidEnterpriseApi.getInventory', () => {
  it('queries the paged inventory endpoint with server-side filters', async () => {
    const result = {
      items: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
      totalPages: 0,
    }

    vi.mocked(apiClient.get).mockResolvedValue({
      data: result,
    })

    await expect(
      androidEnterpriseApi.getInventory({
        page: 2,
        pageSize: 25,
        search: 'caja',
      }),
    ).resolves.toEqual(result)

    expect(apiClient.get).toHaveBeenCalledWith(
      '/android-enterprise/devices',
      {
        params: {
          page: 2,
          pageSize: 25,
          search: 'caja',
        },
      },
    )
  })
})

describe('androidEnterpriseApi.getAudit', () => {
  it('queries the paged android audit endpoint', async () => {
    const result = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
      totalPages: 0,
    }

    vi.mocked(apiClient.get).mockResolvedValue({
      data: result,
    })

    await expect(
      androidEnterpriseApi.getAudit({
        search: '  Lock  ',
      }),
    ).resolves.toEqual(result)

    expect(apiClient.get).toHaveBeenCalledWith(
      '/android-enterprise/audit',
      {
        params: {
          page: 1,
          pageSize: 50,
          search: 'Lock',
        },
      },
    )
  })
})
