import {
  useCallback,
  useMemo,
  useState,
} from 'react'

import { enrollmentApi } from '../../../api/enrollmentApi'

import type {
  CreatedEnrollmentToken,
  EnrollmentToken,
} from '../../../types/enrollment'

import {
  extractRequestError,
  getEffectiveTokenStatus,
} from '../utils/enrollmentFormatters'

export interface AndroidAgentStatistics {
  total: number
  active: number
  expired: number
  revoked: number
}

export function useAndroidAgentEnrollment() {
  const [
    tokens,
    setTokens,
  ] = useState<EnrollmentToken[]>([])

  const [
    expirationMinutes,
    setExpirationMinutes,
  ] = useState(60)

  const [
    maxUses,
    setMaxUses,
  ] = useState(1)

  const [
    createdToken,
    setCreatedToken,
  ] =
    useState<CreatedEnrollmentToken | null>(
      null,
    )

  const [
    loading,
    setLoading,
  ] = useState(false)

  const [
    creating,
    setCreating,
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

  const androidTokens =
    useMemo(
      () =>
        tokens.filter(
          (token) =>
            token.platform === 'Android',
        ),
      [tokens],
    )

  const statistics =
    useMemo<AndroidAgentStatistics>(
      () => ({
        total: androidTokens.length,

        active:
          androidTokens.filter(
            (token) =>
              getEffectiveTokenStatus(
                token.status,
                token.expiresAtUtc,
              ) === 'Active',
          ).length,

        expired:
          androidTokens.filter(
            (token) =>
              getEffectiveTokenStatus(
                token.status,
                token.expiresAtUtc,
              ) === 'Expired',
          ).length,

        revoked:
          androidTokens.filter(
            (token) =>
              token.status === 'Revoked',
          ).length,
      }),
      [androidTokens],
    )

  const loadTokens =
    useCallback(async () => {
      try {
        setLoading(true)
        setError(null)

        const response =
          await enrollmentApi.getTokens()

        setTokens(response)
      } catch (requestError) {
        console.error(requestError)

        setError(
          extractRequestError(
            requestError,
            'No fue posible cargar las credenciales Android Agent.',
          ),
        )
      } finally {
        setLoading(false)
      }
    }, [])

  const createToken =
    useCallback(async () => {
      if (
        maxUses < 1 ||
        maxUses > 1000
      ) {
        setError(
          'Los usos permitidos deben estar entre 1 y 1000.',
        )

        return
      }

      try {
        setCreating(true)
        setError(null)
        setSuccess(null)
        setCreatedToken(null)
        setCopied(false)

        const response =
          await enrollmentApi.createToken({
            platform: 'Android',
            expirationMinutes,
            maxUses,
          })

        setCreatedToken(response)

        setSuccess(
          'La credencial TitanMDM Android Agent fue creada correctamente.',
        )

        await loadTokens()
      } catch (requestError) {
        console.error(requestError)

        setError(
          extractRequestError(
            requestError,
            'No fue posible crear la credencial Android Agent.',
          ),
        )
      } finally {
        setCreating(false)
      }
    }, [
      expirationMinutes,
      maxUses,
      loadTokens,
    ])

  const revokeToken =
    useCallback(
      async (
        token: EnrollmentToken,
      ) => {
        const effectiveStatus =
          getEffectiveTokenStatus(
            token.status,
            token.expiresAtUtc,
          )

        if (effectiveStatus !== 'Active') {
          return
        }

        const confirmed =
          window.confirm(
            '¿Deseas revocar esta credencial Android Agent?',
          )

        if (!confirmed) {
          return
        }

        try {
          setError(null)
          setSuccess(null)

          await enrollmentApi.revokeToken(
            token.id,
          )

          setSuccess(
            'La credencial Android Agent fue revocada.',
          )

          await loadTokens()
        } catch (requestError) {
          console.error(requestError)

          setError(
            extractRequestError(
              requestError,
              'No fue posible revocar la credencial Android Agent.',
            ),
          )
        }
      },
      [loadTokens],
    )

  const copyToken =
    useCallback(async () => {
      if (!createdToken) {
        return
      }

      try {
        await navigator.clipboard.writeText(
          createdToken.token,
        )

        setCopied(true)

        window.setTimeout(
          () => setCopied(false),
          2500,
        )
      } catch (copyError) {
        console.error(copyError)

        setError(
          'No fue posible copiar la credencial Android Agent.',
        )
      }
    }, [createdToken])

  return {
    tokens: androidTokens,
    statistics,

    expirationMinutes,
    setExpirationMinutes,

    maxUses,
    setMaxUses,

    createdToken,

    loading,
    creating,
    copied,

    error,
    success,

    loadTokens,
    createToken,
    revokeToken,
    copyToken,
  }
}

export type AndroidAgentEnrollmentController =
  ReturnType<
    typeof useAndroidAgentEnrollment
  >