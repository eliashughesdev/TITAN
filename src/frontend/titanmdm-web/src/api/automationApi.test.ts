import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import apiClient from './apiClient'
import { automationApi } from './automationApi'

vi.mock('./apiClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}))

describe('automationApi enterprise contracts', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads real and prepared integrations', async () => {
    const integrations = [
      {
        module: 'general',
        label: 'Devices',
        status: 'connected' as const,
        connected: true,
        eventCodes: ['DeviceOnline'],
        owner: 'Devices',
        connectionPoint: 'DeviceAgentService',
        notes: 'Connected',
      },
    ]

    vi.mocked(apiClient.get).mockResolvedValue({
      data: integrations,
    })

    await expect(
      automationApi.getIntegrations(),
    ).resolves.toEqual(integrations)

    expect(apiClient.get).toHaveBeenCalledWith(
      '/automation/platform/integrations',
    )
  })

  it('loads disabled AUT-2 tool contracts', async () => {
    const contracts = [
      {
        toolCode: 'RunAssistantTool',
        owner: 'Titan Assistant / Fiorella',
        requiredPermission: 'automation.admin',
        writeEnabled: false,
        requiresApproval: true,
        notes: 'Disabled until AUT-2.',
      },
    ]

    vi.mocked(apiClient.get).mockResolvedValue({
      data: contracts,
    })

    await expect(
      automationApi.getToolContracts(),
    ).resolves.toEqual(contracts)

    expect(apiClient.get).toHaveBeenCalledWith(
      '/automation/platform/tool-contracts',
    )
  })

  it('posts simulations without using an execution endpoint', async () => {
    const result = {
      ruleId: 'rule-1',
      matched: true,
      simulated: true,
      conditionTraces: [],
      plannedActions: [],
      warnings: [],
    }

    vi.mocked(apiClient.post).mockResolvedValue({
      data: result,
    })

    await expect(
      automationApi.simulate('rule-1', {
        triggerPayloadJson: '{"status":"offline"}',
      }),
    ).resolves.toEqual(result)

    expect(apiClient.post).toHaveBeenCalledWith(
      '/automation/platform/rules/rule-1/simulate',
      {
        triggerPayloadJson: '{"status":"offline"}',
      },
    )
  })
})
