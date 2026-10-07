import {
  useEffect,
  useMemo,
  useState,
} from 'react'

import AndroidEnrollmentPanel from './components/AndroidEnrollmentPanel'
import EnrollmentPlatformTabs from './components/EnrollmentPlatformTabs'
import WindowsEnrollmentPanel from './components/WindowsEnrollmentPanel'

import type {
  EnrollmentPlatformTab,
} from './components/EnrollmentPlatformTabs'

import {
  useAndroidAgentEnrollment,
} from './hooks/useAndroidAgentEnrollment'

import {
  useAndroidEnterpriseEnrollment,
} from './hooks/useAndroidEnterpriseEnrollment'

import {
  useWindowsEnrollment,
} from './hooks/useWindowsEnrollment'

import './EnrollmentPage.css'

/*
 * ============================================================
 * TITANMDM ENROLLMENT CENTER
 * ============================================================
 *
 * Esta página solamente orquesta las plataformas.
 *
 * La lógica de negocio vive en:
 *
 * hooks/
 *   useAndroidEnterpriseEnrollment.ts
 *   useAndroidAgentEnrollment.ts
 *   useWindowsEnrollment.ts
 *
 * La interfaz vive en:
 *
 * components/
 *   EnrollmentPlatformTabs.tsx
 *   AndroidEnrollmentPanel.tsx
 *   AndroidEnterpriseEnrollment.tsx
 *   AndroidAgentEnrollment.tsx
 *   WindowsEnrollmentPanel.tsx
 *
 * Esto permite integrar posteriormente RBAC granular sin volver
 * a convertir EnrollmentPage en un componente monolítico.
 */

export default function EnrollmentPage() {
  /*
   * ==========================================================
   * PERMISSIONS PLACEHOLDERS
   * ==========================================================
   *
   * Estos valores son deliberadamente explícitos.
   *
   * Más adelante serán sustituidos por el sistema RBAC real:
   *
   * enrollment.android.view
   * enrollment.android.enterprise.view
   * enrollment.android.enterprise.create
   * enrollment.android.enterprise.revoke
   * enrollment.android.enterprise.connect
   *
   * enrollment.android.agent.view
   * enrollment.android.agent.create
   * enrollment.android.agent.revoke
   *
   * enrollment.windows.view
   * enrollment.windows.create
   * enrollment.windows.revoke
   */

  const permissions = useMemo(
    () => ({
      android: {
        view: true,

        enterprise: {
          view: true,
          create: true,
          revoke: true,
          connect: true,
        },

        agent: {
          view: true,
          create: true,
          revoke: true,
        },
      },

      windows: {
        view: true,
        create: true,
        revoke: true,
      },
    }),
    [],
  )

  /*
   * ==========================================================
   * CONTROLLERS
   * ==========================================================
   */

  const androidEnterprise =
    useAndroidEnterpriseEnrollment()

  const androidAgent =
    useAndroidAgentEnrollment()

  const windows =
    useWindowsEnrollment()

  /*
   * ==========================================================
   * ACTIVE PLATFORM
   * ==========================================================
   *
   * Android es la prioridad actual del proyecto.
   *
   * Cuando RBAC esté conectado:
   * - si puede ver Android -> Android
   * - si solo puede ver Windows -> Windows
   */

  const initialPlatform:
    EnrollmentPlatformTab =
      permissions.android.view
        ? 'android'
        : 'windows'

  const [
    activePlatform,
    setActivePlatform,
  ] =
    useState<EnrollmentPlatformTab>(
      initialPlatform,
    )

  /*
   * ==========================================================
   * INITIALIZATION
   * ==========================================================
   */

  useEffect(() => {
    document.title =
      'Inscripción | TitanMDM'

    /*
     * Cargamos únicamente aquello que el usuario
     * tendría permiso de consultar.
     *
     * Actualmente todos los permisos están habilitados.
     */

    const initialRequests:
      Promise<unknown>[] = []

    if (
      permissions.android.view &&
      permissions.android.enterprise.view
    ) {
      initialRequests.push(
        androidEnterprise.load(),
      )
    }

    if (
      permissions.android.view &&
      permissions.android.agent.view
    ) {
      initialRequests.push(
        androidAgent.loadTokens(),
      )
    }

    if (permissions.windows.view) {
      initialRequests.push(
        windows.loadTokens(),
      )
    }

    void Promise.allSettled(
      initialRequests,
    )

    /*
     * Android Enterprise puede regresar desde Google
     * utilizando parámetros en la URL.
     */

    if (
      permissions.android.view &&
      permissions.android.enterprise.view
    ) {
      void androidEnterprise.processCallback()
    }

    /*
     * Los métodos load son estables mediante useCallback.
     * Esta inicialización solamente debe ejecutarse al
     * montar la página.
     */
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /*
   * ==========================================================
   * ACCESS NORMALIZATION
   * ==========================================================
   *
   * Cuando conectemos RBAC real, si el usuario pierde acceso
   * a una plataforma mientras está dentro de la pantalla,
   * TitanMDM lo enviará automáticamente a una disponible.
   */

  useEffect(() => {
    if (
      activePlatform === 'android' &&
      !permissions.android.view &&
      permissions.windows.view
    ) {
      setActivePlatform('windows')
      return
    }

    if (
      activePlatform === 'windows' &&
      !permissions.windows.view &&
      permissions.android.view
    ) {
      setActivePlatform('android')
    }
  }, [
    activePlatform,
    permissions,
  ])

  /*
   * ==========================================================
   * REFRESH
   * ==========================================================
   */

  const refreshing =
    activePlatform === 'android'
      ? (
          androidEnterprise.loading ||
          androidAgent.loading
        )
      : windows.loading

  async function handleRefresh() {
    if (activePlatform === 'android') {
      const requests:
        Promise<unknown>[] = []

      if (
        permissions.android.enterprise.view
      ) {
        requests.push(
          androidEnterprise.load(),
        )
      }

      if (
        permissions.android.agent.view
      ) {
        requests.push(
          androidAgent.loadTokens(),
        )
      }

      await Promise.allSettled(
        requests,
      )

      return
    }

    if (
      activePlatform === 'windows' &&
      permissions.windows.view
    ) {
      await windows.loadTokens()
    }
  }

  /*
   * ==========================================================
   * NO ACCESS
   * ==========================================================
   *
   * Esta condición empezará a ser útil cuando conectemos
   * los permisos reales.
   */

  const hasEnrollmentAccess =
    permissions.android.view ||
    permissions.windows.view

  if (!hasEnrollmentAccess) {
    return (
      <div className="enrollment-page">
        <section className="enrollment-heading">
          <div>
            <p className="enrollment-eyebrow">
              Gestión de dispositivos
            </p>

            <h1>
              Centro de inscripción
            </h1>

            <p className="enrollment-description">
              Tu cuenta no tiene acceso a
              ninguna plataforma de inscripción.
            </p>
          </div>
        </section>

        <section className="enrollment-panel">
          <div className="enrollment-empty-table">
            No tienes permisos para consultar
            inscripciones Android o Windows.
          </div>
        </section>
      </div>
    )
  }

  /*
   * ==========================================================
   * UI
   * ==========================================================
   */

  return (
    <div className="enrollment-page">
      <section className="enrollment-heading">
        <div>
          <p className="enrollment-eyebrow">
            Gestión de dispositivos
          </p>

          <h1>
            Centro de inscripción
          </h1>

          <p className="enrollment-description">
            Incorpora y administra dispositivos
            Android y Windows desde un centro
            organizado por plataforma.
          </p>
        </div>

        <button
          className="enrollment-refresh-button"
          type="button"
          disabled={refreshing}
          onClick={() =>
            void handleRefresh()
          }
        >
          {refreshing
            ? 'Actualizando...'
            : 'Actualizar'}
        </button>
      </section>

      <EnrollmentPlatformTabs
        activePlatform={
          activePlatform
        }
        canViewAndroid={
          permissions.android.view
        }
        canViewWindows={
          permissions.windows.view
        }
        onPlatformChange={
          setActivePlatform
        }
      />

      {activePlatform === 'android' &&
        permissions.android.view && (
          <AndroidEnrollmentPanel
            enterprise={
              androidEnterprise
            }
            agent={androidAgent}

            canViewEnterprise={
              permissions.android
                .enterprise.view
            }

            canViewAgent={
              permissions.android
                .agent.view
            }

            canCreateEnterprise={
              permissions.android
                .enterprise.create
            }

            canRevokeEnterprise={
              permissions.android
                .enterprise.revoke
            }

            canConnectEnterprise={
              permissions.android
                .enterprise.connect
            }

            canCreateAgent={
              permissions.android
                .agent.create
            }

            canRevokeAgent={
              permissions.android
                .agent.revoke
            }
          />
        )}

      {activePlatform === 'windows' &&
        permissions.windows.view && (
          <WindowsEnrollmentPanel
            controller={windows}
            canCreate={
              permissions.windows.create
            }
            canRevoke={
              permissions.windows.revoke
            }
          />
        )}
    </div>
  )
}