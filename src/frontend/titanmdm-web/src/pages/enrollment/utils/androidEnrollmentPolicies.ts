import type {
  AndroidPolicyPublication,
  Policy,
} from '../../../api/policiesApi'

export interface PublishedAndroidPolicy {
  policy: Policy
  publication: AndroidPolicyPublication
}

export function isCurrentPublishedAndroidPolicy(
  policy: Policy,
  publication: AndroidPolicyPublication | null,
): publication is AndroidPolicyPublication {
  return (
    publication?.status === 'Published'
    && publication.policyVersion === policy.currentVersion
  )
}
