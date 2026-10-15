import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import QRCode from 'qrcode'

import {
  androidEnterpriseApi,
} from '../../../api/androidEnterpriseApi'

import {
  policiesApi,
} from '../../../api/policiesApi'

import {
  useAuth,
} from '../../../auth/AuthContext'

import {
  Permissions,
} from '../../../auth/permissions'

import type {
  AndroidEnterpriseStatus,
  AndroidEnrollment,
  AndroidEnrollmentMode,
  CreatedAndroidEnrollment,
} from '../../../types/androidEnterprise'

import {
  extractRequestError,
  isAndroidEnterpriseActive,
} from '../utils/enrollmentFormatters'

import {
  isCurrentPublishedAndroidPolicy,
  type PublishedAndroidPolicy,
} from '../utils/androidEnrollmentPolicies'

export function useAndroidEnterpriseEnrollment() {
  const {
    hasPermission,
  } = useAuth()

  const canViewPolicies =
    hasPermission(
      Permissions.policies.view,
    )

  const [
    status,
    setStatus,
  ] =
    useState<AndroidEnterpriseStatus | null>(
      null,
    )

  const [
    enrollments,
    setEnrollments,
  ] = useState<AndroidEnrollment[]>([])

  const [
    mode,
    setMode,
  ] =
    useState<AndroidEnrollmentMode>(
      'FullyManaged',
    )

  const [
    expirationMinutes,
    setExpirationMinutes,
  ] = useState(60)

  const [
    publishedPolicies,
    setPublishedPolicies,
  ] = useState<PublishedAndroidPolicy[]>([])

  const [
    selectedPolicyId,
    setSelectedPolicyId,
  ] = useState('')

  const [
    policiesLoading,
    setPoliciesLoading,
  ] = useState(false)

  const [
    policiesError,
    setPoliciesError,
  ] = useState<string | null>(null)

  const [
    createdEnrollment,
    setCreatedEnrollment,
  ] =
    useState<CreatedAndroidEnrollment | null>(
      null,
    )

  const [
    qrDataUrl,
    setQrDataUrl,
  ] = useState<string | null>(null)

  const [
    loading,
    setLoading,
  ] = useState(false)

  const [
    creating,
    setCreating,
  ] = useState(false)

  const [
    connecting,
    setConnecting,
  ] = useState(false)

  const [
    copied,
    setCopied,
  ] = useState(false)

  const [
    error,
    setError,
  ] = useState<string | null>(null)

  const [
    success,
    setSuccess,
  ] = useState<string | null>(null)

  const active =
    isAndroidEnterpriseActive(status)

  const readyForSignup =
    Boolean(
      status?.isConfigured &&
      status?.canAuthenticate &&
      status?.hasPublicCallback,
    )

  const policyRequired =
    mode === 'FullyManaged'
    || mode === 'Dedicated'

  const statistics =
    useMemo(
      () => ({
        total: enrollments.length,

        active:
          enrollments.filter(
            (item) =>
              !item.isRevoked &&
              !item.isExpired,
          ).length,

        expired:
          enrollments.filter(
            (item) =>
              item.isExpired &&
              !item.isRevoked,
          ).length,

        revoked:
          enrollments.filter(
            (item) =>
              item.isRevoked,
          ).length,
      }),
      [enrollments],
    )

  const load =
    useCallback(async () => {
      try {
        setLoading(true)
        setError(null)

        const response =
          await androidEnterpriseApi
            .getStatus()

        setStatus(response)

        if (
          response.status === 'Active' &&
          response.enterpriseName
        ) {
          if (canViewPolicies) {
            setPoliciesLoading(true)
            setPoliciesError(null)
          }

          const enrollmentResponse =
            await androidEnterpriseApi
              .getEnrollments()

          if (canViewPolicies) {
            try {
              const policies =
                await policiesApi.getAll(
                  'Android',
                )

              const publications =
                await Promise.all(
                  policies.map(
                    policy =>
                      policiesApi
                        .getAndroidPublication(
                          policy.id,
                        ),
                  ),
                )

              const available =
                policies.flatMap(
                  (policy, index) => {
                    const publication =
                      publications[index]

                    return isCurrentPublishedAndroidPolicy(
                      policy,
                      publication,
                    )
                      ? [{
                          policy,
                          publication,
                        }]
                      : []
                  },
                )

              setPublishedPolicies(
                available,
              )

              setSelectedPolicyId(
                current =>
                  available.some(
                    item =>
                      item.policy.id === current,
                  )
                    ? current
                    : '',
              )
            } catch (policyError) {
              console.error(policyError)
              setPublishedPolicies([])
              setSelectedPolicyId('')
              setPoliciesError(
                extractRequestError(
                  policyError,
                  'No fue posible consultar las políticas Android publicadas.',
                ),
              )
            } finally {
              setPoliciesLoading(false)
            }
          } else {
            setPublishedPolicies([])
            setSelectedPolicyId('')
            setPoliciesError(null)
          }

          setEnrollments(
            enrollmentResponse,
          )
        } else {
          setEnrollments([])
          setPublishedPolicies([])
          setSelectedPolicyId('')
          setPoliciesError(null)
        }
      } catch (requestError) {
        console.error(requestError)

        setError(
          extractRequestError(
            requestError,
            'No fue posible consultar Android Enterprise.',
          ),
        )
      } finally {
        setLoading(false)
        setPoliciesLoading(false)
      }
    }, [canViewPolicies])

  useEffect(() => {
    let cancelled = false

    async function generateQr() {
      if (!createdEnrollment?.qrCode) {
        setQrDataUrl(null)
        return
      }

      try {
        const dataUrl =
          await QRCode.toDataURL(
            createdEnrollment.qrCode,
            {
              width: 360,
              margin: 2,
              errorCorrectionLevel: 'M',
            },
          )

        if (!cancelled) {
          setQrDataUrl(dataUrl)
        }
      } catch (qrError) {
        console.error(qrError)

        if (!cancelled) {
          setQrDataUrl(null)

          setError(
            'Google generó la inscripción, pero TitanMDM no pudo representar el código QR.',
          )
        }
      }
    }

    void generateQr()

    return () => {
      cancelled = true
    }
  }, [createdEnrollment])

  const connect =
    useCallback(async () => {
      try {
        setConnecting(true)
        setError(null)
        setSuccess(null)

        const response =
          await androidEnterpriseApi
            .createSignup()

        window.location.assign(
          response.signupUrl,
        )
      } catch (requestError) {
        console.error(requestError)

        setError(
          extractRequestError(
            requestError,
            'No fue posible iniciar la conexión con Android Enterprise.',
          ),
        )

        setConnecting(false)
      }
    }, [])

  const createEnrollment =
    useCallback(async () => {
      if (
        policyRequired
        && !selectedPolicyId
      ) {
        setError(
          'Selecciona una política Android publicada para este modo de administración.',
        )
        return
      }

      try {
        setCreating(true)
        setError(null)
        setSuccess(null)
        setCopied(false)
        setCreatedEnrollment(null)
        setQrDataUrl(null)

        const response =
          await androidEnterpriseApi
            .createEnrollment({
              mode,
              expirationMinutes,
              policyId:
                selectedPolicyId || null,
            })

        setCreatedEnrollment(response)

        setSuccess(
          'La credencial de aprovisionamiento fue emitida. La inscripción solo se completa cuando el dispositivo se sincroniza y aparece en TitanMDM.',
        )

        const enrollmentResponse =
          await androidEnterpriseApi
            .getEnrollments()

        setEnrollments(
          enrollmentResponse,
        )
      } catch (requestError) {
        console.error(requestError)

        setError(
          extractRequestError(
            requestError,
            'No fue posible generar la inscripción Android Enterprise.',
          ),
        )
      } finally {
        setCreating(false)
      }
    }, [
      mode,
      expirationMinutes,
      policyRequired,
      selectedPolicyId,
    ])

  const revokeEnrollment =
    useCallback(
      async (
        enrollment: AndroidEnrollment,
      ) => {
        if (
          enrollment.isRevoked ||
          enrollment.isExpired
        ) {
          return
        }

        const confirmed =
          window.confirm(
            '¿Deseas revocar esta inscripción Android Enterprise?',
          )

        if (!confirmed) {
          return
        }

        try {
          setError(null)
          setSuccess(null)

          await androidEnterpriseApi
            .revokeEnrollment(
              enrollment.id,
            )

          setSuccess(
            'La inscripción Android Enterprise fue revocada.',
          )

          const response =
            await androidEnterpriseApi
              .getEnrollments()

          setEnrollments(response)
        } catch (requestError) {
          console.error(requestError)

          setError(
            extractRequestError(
              requestError,
              'No fue posible revocar la inscripción Android Enterprise.',
            ),
          )
        }
      },
      [],
    )

  const copyToken =
    useCallback(async () => {
      if (!createdEnrollment) {
        return
      }

      try {
        await navigator.clipboard.writeText(
          createdEnrollment.enrollmentToken,
        )

        setCopied(true)

        window.setTimeout(
          () => setCopied(false),
          2500,
        )
      } catch (copyError) {
        console.error(copyError)

        setError(
          'No fue posible copiar el token Android Enterprise.',
        )
      }
    }, [createdEnrollment])

  const processCallback =
    useCallback(
      async () => {
        const parameters =
          new URLSearchParams(
            window.location.search,
          )

        const result =
          parameters.get(
            'androidEnterprise',
          )

        if (result === 'connected') {
          setSuccess(
            'Android Enterprise fue conectado correctamente.',
          )

          await load()
        }

        if (result === 'error') {
          setError(
            'Google no pudo completar la vinculación con Android Enterprise.',
          )
        }
      },
      [load],
    )

  return {
    status,
    enrollments,

    mode,
    setMode,

    expirationMinutes,
    setExpirationMinutes,

    publishedPolicies,
    selectedPolicyId,
    setSelectedPolicyId,
    policiesLoading,
    policiesError,
    canViewPolicies,
    policyRequired,

    createdEnrollment,
    qrDataUrl,

    loading,
    creating,
    connecting,
    copied,

    error,
    success,

    active,
    readyForSignup,
    statistics,

    load,
    connect,
    createEnrollment,
    revokeEnrollment,
    copyToken,
    processCallback,
  }
}

export type AndroidEnterpriseEnrollmentController =
  ReturnType<
    typeof useAndroidEnterpriseEnrollment
  >
