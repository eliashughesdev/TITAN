import {
  describe,
  expect,
  it,
} from 'vitest'

import type {
  AndroidPolicyPublication,
  Policy,
} from '../../../api/policiesApi'

import {
  isCurrentPublishedAndroidPolicy,
} from './androidEnrollmentPolicies'

const policy: Policy = {
  id: 'policy-1',
  organizationId: 'organization-1',
  name: 'Corporate Android',
  description: null,
  platform: 'Android',
  status: 'Active',
  currentVersion: 3,
  assignedDevices: 0,
  createdByUserId: 'user-1',
  createdAtUtc: '2026-10-14T10:00:00Z',
  updatedAtUtc: '2026-10-14T10:00:00Z',
  activatedAtUtc: '2026-10-14T10:00:00Z',
  archivedAtUtc: null,
}

const publication: AndroidPolicyPublication = {
  id: 'publication-1',
  policyId: policy.id,
  policyVersionId: 'version-3',
  policyVersion: 3,
  googlePolicyId: 'policy-1-v3',
  googlePolicyName: 'enterprises/e/policies/policy-1-v3',
  status: 'Published',
  compiledPolicyJson: '{}',
  googleResponseJson: '{}',
  errorCode: null,
  errorMessage: null,
  createdAtUtc: '2026-10-14T10:00:00Z',
  updatedAtUtc: '2026-10-14T10:00:00Z',
  publishedAtUtc: '2026-10-14T10:00:00Z',
  lastAttemptAtUtc: '2026-10-14T10:00:00Z',
  deletedAtUtc: null,
}

describe('isCurrentPublishedAndroidPolicy', () => {
  it('accepts only the published current policy version', () => {
    expect(
      isCurrentPublishedAndroidPolicy(
        policy,
        publication,
      ),
    ).toBe(true)

    expect(
      isCurrentPublishedAndroidPolicy(
        policy,
        {
          ...publication,
          policyVersion: 2,
        },
      ),
    ).toBe(false)

    expect(
      isCurrentPublishedAndroidPolicy(
        policy,
        {
          ...publication,
          status: 'Publishing',
        },
      ),
    ).toBe(false)
  })
})
