export interface DeviceGroupDynamicRule {
  operator: 'AND'
  conditions: Array<{
    field: string
    operator: 'equals'
    value: string
  }>
}

export interface ParsedDynamicRule {
  platform: string
  status: string
}

export function buildDynamicRule(
  platform: string,
  status: string,
): string {
  const rule: DeviceGroupDynamicRule = {
    operator: 'AND',
    conditions: [
      {
        field: 'platform',
        operator: 'equals',
        value: platform,
      },
      {
        field: 'status',
        operator: 'equals',
        value: status,
      },
    ],
  }

  return JSON.stringify(rule)
}

export function parseDynamicRule(
  ruleJson: string | null,
): ParsedDynamicRule {
  const fallback: ParsedDynamicRule = {
    platform: 'Windows',
    status: 'Online',
  }

  if (!ruleJson) {
    return fallback
  }

  try {
    const parsed =
      JSON.parse(
        ruleJson,
      ) as Partial<DeviceGroupDynamicRule>

    if (
      !Array.isArray(
        parsed.conditions,
      )
    ) {
      return fallback
    }

    const platformCondition =
      parsed.conditions.find(
        condition =>
          condition.field ===
          'platform',
      )

    const statusCondition =
      parsed.conditions.find(
        condition =>
          condition.field ===
          'status',
      )

    return {
      platform:
        platformCondition
          ?.value
        ?? fallback.platform,

      status:
        statusCondition
          ?.value
        ?? fallback.status,
    }
  } catch {
    return fallback
  }
}