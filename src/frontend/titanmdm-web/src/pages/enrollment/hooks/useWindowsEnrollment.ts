import {
  useCallback,
  useMemo,
  useState,
} from 'react'

import {
  enrollmentApi,
} from '../../../api/enrollmentApi'

import type {
  CreatedEnrollmentToken,
  EnrollmentToken,
} from '../../../types/enrollment'

import {
  extractRequestError,
  getEffectiveTokenStatus,
} from '../utils/enrollmentFormatters'

function downloadBlob(
  blob: Blob,
  fileName: string,
) {
  const url =
    URL.createObjectURL(
      blob,
    )

  const anchor =
    document.createElement(
      'a',
    )

  anchor.href =
    url

  anchor.download =
    fileName

  document.body
    .appendChild(
      anchor,
    )

  anchor.click()

  anchor.remove()

  URL.revokeObjectURL(
    url,
  )
}

export function useWindowsEnrollment() {
  const [
    tokens,
    setTokens,
  ] =
    useState<
      EnrollmentToken[]
    >([])

  const [
    expirationMinutes,
    setExpirationMinutes,
  ] =
    useState(60)

  const [
    maxUses,
    setMaxUses,
  ] =
    useState(1)

  const [
    createdToken,
    setCreatedToken,
  ] =
    useState<
      CreatedEnrollmentToken | null
    >(null)

  const [
    loading,
    setLoading,
  ] =
    useState(false)

  const [
    creating,
    setCreating,
  ] =
    useState(false)

  const [
    downloadingIndividual,
    setDownloadingIndividual,
  ] =
    useState(false)

  const [
    downloadingGpo,
    setDownloadingGpo,
  ] =
    useState(false)

  const [
    copied,
    setCopied,
  ] =
    useState(false)

  const [
    error,
    setError,
  ] =
    useState<
      string | null
    >(null)

  const [
    success,
    setSuccess,
  ] =
    useState<
      string | null
    >(null)

  const windowsTokens =
    useMemo(
      () =>
        tokens.filter(
          token =>
            token.platform ===
            'Windows',
        ),
      [
        tokens,
      ],
    )

  const statistics =
    useMemo(
      () => ({
        total:
          windowsTokens.length,

        active:
          windowsTokens.filter(
            token =>
              getEffectiveTokenStatus(
                token.status,
                token.expiresAtUtc,
              ) ===
              'Active',
          ).length,

        expired:
          windowsTokens.filter(
            token =>
              getEffectiveTokenStatus(
                token.status,
                token.expiresAtUtc,
              ) ===
              'Expired',
          ).length,

        revoked:
          windowsTokens.filter(
            token =>
              token.status ===
              'Revoked',
          ).length,
      }),
      [
        windowsTokens,
      ],
    )

  const loadTokens =
    useCallback(
      async () => {
        try {
          setLoading(
            true,
          )

          setError(
            null,
          )

          const response =
            await enrollmentApi
              .getTokens()

          setTokens(
            response,
          )
        } catch (
          requestError
        ) {
          setError(
            extractRequestError(
              requestError,
              'No fue posible cargar las credenciales Windows.',
            ),
          )
        } finally {
          setLoading(
            false,
          )
        }
      },
      [],
    )

  const createToken =
    useCallback(
      async () => {
        if (
          maxUses <
            1
          ||
          maxUses >
            1000
        ) {
          setError(
            'Los usos permitidos deben estar entre 1 y 1000.',
          )

          return
        }

        try {
          setCreating(
            true,
          )

          setError(
            null,
          )

          setSuccess(
            null,
          )

          setCreatedToken(
            null,
          )

          setCopied(
            false,
          )

          const response =
            await enrollmentApi
              .createToken({
                platform:
                  'Windows',

                expirationMinutes,

                maxUses,
              })

          setCreatedToken(
            response,
          )

          setSuccess(
            'La credencial Windows fue creada correctamente.',
          )

          await loadTokens()
        } catch (
          requestError
        ) {
          setError(
            extractRequestError(
              requestError,
              'No fue posible crear la credencial Windows.',
            ),
          )
        } finally {
          setCreating(
            false,
          )
        }
      },
      [
        expirationMinutes,
        maxUses,
        loadTokens,
      ],
    )

  const downloadIndividualInstaller =
    useCallback(
      async () => {
        try {
          setDownloadingIndividual(
            true,
          )

          setError(
            null,
          )

          setSuccess(
            null,
          )

          const blob =
            await enrollmentApi
              .downloadWindowsInstaller({
                deploymentMode:
                  'individual',

                expirationMinutes,

                maxUses:
                  1,
              })

          downloadBlob(
            blob,
             'TitanMDM-Agent-Setup.exe',
          )

          setSuccess(
             'Instalador TitanMDM generado correctamente. La credencial incluida permite un solo enrolamiento.',
          )

          await loadTokens()
        } catch (
          requestError
        ) {
          setError(
            extractRequestError(
              requestError,
              'No fue posible generar el instalador Windows.',
            ),
          )
        } finally {
          setDownloadingIndividual(
            false,
          )
        }
      },
      [
        expirationMinutes,
        loadTokens,
      ],
    )

  const downloadGpoInstaller =
    useCallback(
      async () => {
        if (
          maxUses <
            1
          ||
          maxUses >
            1000
        ) {
          setError(
            'Para GPO, MaxUses debe estar entre 1 y 1000.',
          )

          return
        }

        try {
          setDownloadingGpo(
            true,
          )

          setError(
            null,
          )

          setSuccess(
            null,
          )

          const blob =
            await enrollmentApi
              .downloadWindowsInstaller({
                deploymentMode:
                  'gpo',

                expirationMinutes,

                maxUses,
              })

          downloadBlob(
            blob,
             'TitanMDM-GPO.zip',
          )

          setSuccess(
            `Paquete GPO generado para hasta ${maxUses} equipos.`,
          )

          await loadTokens()
        } catch (
          requestError
        ) {
          setError(
            extractRequestError(
              requestError,
              'No fue posible generar el script GPO.',
            ),
          )
        } finally {
          setDownloadingGpo(
            false,
          )
        }
      },
      [
        expirationMinutes,
        maxUses,
        loadTokens,
      ],
    )

  const revokeToken =
    useCallback(
      async (
        token:
          EnrollmentToken,
      ) => {
        const status =
          getEffectiveTokenStatus(
            token.status,
            token.expiresAtUtc,
          )

        if (
          status !==
          'Active'
        ) {
          return
        }

        if (
          !window.confirm(
            '¿Deseas revocar esta credencial Windows?',
          )
        ) {
          return
        }

        try {
          setError(
            null,
          )

          setSuccess(
            null,
          )

          await enrollmentApi
            .revokeToken(
              token.id,
            )

          setSuccess(
            'La credencial Windows fue revocada.',
          )

          await loadTokens()
        } catch (
          requestError
        ) {
          setError(
            extractRequestError(
              requestError,
              'No fue posible revocar la credencial Windows.',
            ),
          )
        }
      },
      [
        loadTokens,
      ],
    )

  const copyToken =
    useCallback(
      async () => {
        if (
          !createdToken
        ) {
          return
        }

        try {
          await navigator
            .clipboard
            .writeText(
              createdToken.token,
            )

          setCopied(
            true,
          )

          window.setTimeout(
            () =>
              setCopied(
                false,
              ),
            2500,
          )
        } catch {
          setError(
            'No fue posible copiar la credencial Windows.',
          )
        }
      },
      [
        createdToken,
      ],
    )

  return {
    tokens:
      windowsTokens,

    statistics,

    expirationMinutes,
    setExpirationMinutes,

    maxUses,
    setMaxUses,

    createdToken,

    loading,
    creating,

    downloadingIndividual,
    downloadingGpo,

    copied,

    error,
    success,

    loadTokens,
    createToken,

    downloadIndividualInstaller,
    downloadGpoInstaller,

    revokeToken,
    copyToken,
  }
}

export type WindowsEnrollmentController =
  ReturnType<
    typeof useWindowsEnrollment
  >