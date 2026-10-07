import {
  useEffect,
  useState,
} from 'react'

import AndroidAgentEnrollment from './AndroidAgentEnrollment'
import AndroidEnterpriseEnrollment from './AndroidEnterpriseEnrollment'

import type {
  AndroidAgentEnrollmentController,
} from '../hooks/useAndroidAgentEnrollment'

import type {
  AndroidEnterpriseEnrollmentController,
} from '../hooks/useAndroidEnterpriseEnrollment'

type AndroidEnrollmentTechnology =
  | 'enterprise'
  | 'agent'

interface AndroidEnrollmentPanelProps {
  enterprise:
    AndroidEnterpriseEnrollmentController

  agent:
    AndroidAgentEnrollmentController

  canViewEnterprise?: boolean
  canViewAgent?: boolean

  canCreateEnterprise?: boolean
  canRevokeEnterprise?: boolean
  canConnectEnterprise?: boolean

  canCreateAgent?: boolean
  canRevokeAgent?: boolean
}

export default function AndroidEnrollmentPanel({
  enterprise,
  agent,

  canViewEnterprise = true,
  canViewAgent = true,

  canCreateEnterprise = true,
  canRevokeEnterprise = true,
  canConnectEnterprise = true,

  canCreateAgent = true,
  canRevokeAgent = true,
}: AndroidEnrollmentPanelProps) {
  const [
    activeTechnology,
    setActiveTechnology,
  ] =
    useState<AndroidEnrollmentTechnology>(
      canViewEnterprise
        ? 'enterprise'
        : 'agent',
    )

  useEffect(() => {
    if (
      activeTechnology ===
        'enterprise' &&
      !canViewEnterprise &&
      canViewAgent
    ) {
      setActiveTechnology('agent')
      return
    }

    if (
      activeTechnology === 'agent' &&
      !canViewAgent &&
      canViewEnterprise
    ) {
      setActiveTechnology(
        'enterprise',
      )
    }
  }, [
    activeTechnology,
    canViewEnterprise,
    canViewAgent,
  ])

  if (
    !canViewEnterprise &&
    !canViewAgent
  ) {
    return (
      <section className="enrollment-panel">
        <div className="enrollment-empty-table">
          No tienes permisos para consultar
          métodos de inscripción Android.
        </div>
      </section>
    )
  }

  return (
    <div className="android-enrollment-panel">
      {canViewEnterprise &&
        canViewAgent && (
          <div
            className="android-technology-tabs"
            role="tablist"
            aria-label="Tecnología de inscripción Android"
          >
            <button
              type="button"
              role="tab"
              aria-selected={
                activeTechnology ===
                'enterprise'
              }
              className={
                activeTechnology ===
                'enterprise'
                  ? 'android-technology-tab android-technology-tab-active'
                  : 'android-technology-tab'
              }
              onClick={() =>
                setActiveTechnology(
                  'enterprise',
                )
              }
            >
              <strong>
                Android Enterprise
              </strong>

              <small>
                Google AMAPI
              </small>
            </button>

            <button
              type="button"
              role="tab"
              aria-selected={
                activeTechnology ===
                'agent'
              }
              className={
                activeTechnology ===
                'agent'
                  ? 'android-technology-tab android-technology-tab-active'
                  : 'android-technology-tab'
              }
              onClick={() =>
                setActiveTechnology(
                  'agent',
                )
              }
            >
              <strong>
                TitanMDM Agent
              </strong>

              <small>
                Agente Kotlin
              </small>
            </button>
          </div>
        )}

      {activeTechnology ===
        'enterprise' &&
        canViewEnterprise && (
          <AndroidEnterpriseEnrollment
            controller={enterprise}
            canCreate={
              canCreateEnterprise
            }
            canRevoke={
              canRevokeEnterprise
            }
            canConnect={
              canConnectEnterprise
            }
          />
        )}

      {activeTechnology ===
        'agent' &&
        canViewAgent && (
          <AndroidAgentEnrollment
            controller={agent}
            canCreate={
              canCreateAgent
            }
            canRevoke={
              canRevokeAgent
            }
          />
        )}
    </div>
  )
}