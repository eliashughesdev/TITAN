import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { useLocation } from 'react-router-dom'

import { useAuth } from '../../auth/AuthContext'

export type TitanAnimationState =
  | 'idle'
  | 'walking'
  | 'running'
  | 'prepare-takeoff'
  | 'takeoff'
  | 'flying'
  | 'landing'
  | 'pointing'
  | 'thinking'
  | 'talking'
  | 'success'
  | 'warning'
  | 'error'
  | 'looking'
  | 'following'
  | 'hiding'
  | 'peeking'
  | 'sleeping'

export type TitanPriority =
  | 'idle'
  | 'autonomous'
  | 'assistant'
  | 'contextual'
  | 'user'
  | 'critical'

export type TitanFacingDirection =
  | 'left'
  | 'right'

export interface TitanPosition {
  x: number
  y: number
}

export interface TitanVelocity {
  x: number
  y: number
}

export interface TitanPageContext {
  pathname: string

  module:
    | 'dashboard'
    | 'devices'
    | 'device-detail'
    | 'groups'
    | 'enrollment'
    | 'policies'
    | 'policy-editor'
    | 'apps'
    | 'security'
    | 'compliance'
    | 'kiosk'
    | 'geofencing'
    | 'automation'
    | 'remote'
    | 'reports'
    | 'audit'
    | 'users'
    | 'roles'
    | 'settings'
    | 'unknown'

  entityType?:
    | 'device'
    | 'policy'

  entityId?: string
}

export interface TitanUserContext {
  id: string
  organizationId: string

  firstName: string
  lastName: string
  fullName: string
  email: string

  roles: string[]
  permissions: string[]
}

export interface TitanPreferences {
  visible: boolean

  doNotDisturb: boolean

  autonomousBehavior: boolean

  proactiveComments: boolean

  followPointer: boolean

  reducedMotion: boolean

  allowFlight: boolean

  soundEnabled: boolean
}

export interface TitanRuntimeState {
  animation: TitanAnimationState

  priority: TitanPriority

  message: string | null

  position: TitanPosition

  velocity: TitanVelocity

  direction: TitanFacingDirection

  targetAnchor: string | null
}

interface TitanAssistantContextValue {
  user: TitanUserContext | null

  page: TitanPageContext

  preferences: TitanPreferences

  runtime: TitanRuntimeState

  hasPermission:
    (permission: string) => boolean

  hasAnyPermission:
    (...permissions: string[]) => boolean

  hasRole:
    (role: string) => boolean

  setVisible:
    (visible: boolean) => void

  setDoNotDisturb:
    (enabled: boolean) => void

  setAutonomousBehavior:
    (enabled: boolean) => void

  setProactiveComments:
    (enabled: boolean) => void

  setFollowPointer:
    (enabled: boolean) => void

  setReducedMotion:
    (enabled: boolean) => void

  setAllowFlight:
    (enabled: boolean) => void

  setSoundEnabled:
    (enabled: boolean) => void

  setAnimation: (
    animation: TitanAnimationState,
    priority?: TitanPriority,
  ) => void

  setPosition:
    (position: TitanPosition) => void

  setVelocity:
    (velocity: TitanVelocity) => void

  setDirection:
    (direction: TitanFacingDirection) => void

  moveToAnchor: (
    anchor: string,
    priority?: TitanPriority,
  ) => void

  say: (
    message: string,
    priority?: TitanPriority,
  ) => void

  clearMessage: () => void

  releasePriority: () => void

  resetPreferences: () => void
}

const TitanAssistantContext =
  createContext<
    TitanAssistantContextValue | undefined
  >(undefined)

const STORAGE_KEY =
  'titanmdm:assistant:preferences:v2'

const POSITION_KEY =
  'titanmdm:assistant:position:v2'

const defaultPreferences:
  TitanPreferences = {
  visible: true,

  doNotDisturb: false,

  autonomousBehavior: true,

  proactiveComments: true,

  followPointer: true,

  reducedMotion: false,

  allowFlight: true,

  soundEnabled: false,
}

const defaultRuntime:
  TitanRuntimeState = {
  animation: 'idle',

  priority: 'idle',

  message: null,

  position: {
    x: 0,
    y: 0,
  },

  velocity: {
    x: 0,
    y: 0,
  },

  direction: 'right',

  targetAnchor: null,
}

const priorityWeight:
  Record<TitanPriority, number> = {
  idle: 0,

  autonomous: 10,

  assistant: 20,

  contextual: 30,

  user: 40,

  critical: 50,
}

function resolvePageContext(
  pathname: string,
): TitanPageContext {
  const segments =
    pathname
      .split('/')
      .filter(Boolean)

  if (segments.length === 0) {
    return {
      pathname,
      module: 'dashboard',
    }
  }

  const [module, entityId] =
    segments

  switch (module) {
    case 'dashboard':
      return {
        pathname,
        module: 'dashboard',
      }

    case 'devices':
      return entityId
        ? {
            pathname,
            module: 'device-detail',
            entityType: 'device',
            entityId,
          }
        : {
            pathname,
            module: 'devices',
          }

    case 'groups':
      return {
        pathname,
        module: 'groups',
      }

    case 'enrollment':
      return {
        pathname,
        module: 'enrollment',
      }

    case 'policies':
      return entityId
        ? {
            pathname,
            module: 'policy-editor',
            entityType: 'policy',
            entityId:
              entityId === 'new'
                ? undefined
                : entityId,
          }
        : {
            pathname,
            module: 'policies',
          }

    case 'apps':
      return {
        pathname,
        module: 'apps',
      }

    case 'security':
      return {
        pathname,
        module: 'security',
      }

    case 'compliance':
      return {
        pathname,
        module: 'compliance',
      }

    case 'kiosk':
      return {
        pathname,
        module: 'kiosk',
      }

    case 'geofencing':
      return {
        pathname,
        module: 'geofencing',
      }

    case 'automation':
      return {
        pathname,
        module: 'automation',
      }

    case 'remote':
      return {
        pathname,
        module: 'remote',
      }

    case 'reports':
      return {
        pathname,
        module: 'reports',
      }

    case 'audit':
      return {
        pathname,
        module: 'audit',
      }

    case 'users':
      return {
        pathname,
        module: 'users',
      }

    case 'roles':
      return {
        pathname,
        module: 'roles',
      }

    case 'settings':
      return {
        pathname,
        module: 'settings',
      }

    default:
      return {
        pathname,
        module: 'unknown',
      }
  }
}

function loadPreferences():
  TitanPreferences {
  try {
    const stored =
      window.localStorage.getItem(
        STORAGE_KEY,
      )

    if (!stored) {
      return defaultPreferences
    }

    return {
      ...defaultPreferences,
      ...JSON.parse(stored),
    }
  } catch {
    return defaultPreferences
  }
}

function loadPosition():
  TitanPosition {
  try {
    const stored =
      window.localStorage.getItem(
        POSITION_KEY,
      )

    if (!stored) {
      return defaultRuntime.position
    }

    const parsed =
      JSON.parse(
        stored,
      ) as TitanPosition

    if (
      typeof parsed.x !== 'number' ||
      typeof parsed.y !== 'number'
    ) {
      return defaultRuntime.position
    }

    return parsed
  } catch {
    return defaultRuntime.position
  }
}

interface TitanAssistantProviderProps {
  children: ReactNode
}

export function TitanAssistantProvider({
  children,
}: TitanAssistantProviderProps) {
  const location =
    useLocation()

  const auth =
    useAuth()

  const [
    preferences,
    setPreferences,
  ] =
    useState<TitanPreferences>(
      loadPreferences,
    )

  const [
    runtime,
    setRuntime,
  ] =
    useState<TitanRuntimeState>(
      () => ({
        ...defaultRuntime,

        position:
          loadPosition(),
      }),
    )

  const page =
    useMemo(
      () =>
        resolvePageContext(
          location.pathname,
        ),
      [location.pathname],
    )

  const user =
    useMemo<
      TitanUserContext | null
    >(() => {
      if (!auth.user) {
        return null
      }

      return {
        id:
          auth.user.id,

        organizationId:
          auth.user.organizationId,

        firstName:
          auth.user.firstName,

        lastName:
          auth.user.lastName,

        fullName:
          `${auth.user.firstName} ${auth.user.lastName}`.trim(),

        email:
          auth.user.email,

        roles:
          [...auth.user.roles],

        permissions:
          [...auth.user.permissions],
      }
    }, [auth.user])

  useEffect(() => {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(
        preferences,
      ),
    )
  }, [preferences])

  useEffect(() => {
    window.localStorage.setItem(
      POSITION_KEY,
      JSON.stringify(
        runtime.position,
      ),
    )
  }, [runtime.position])

  const hasPermission =
    useCallback(
      (permission: string) =>
        auth.hasPermission(
          permission,
        ),
      [auth],
    )

  const hasAnyPermission =
    useCallback(
      (...permissions: string[]) =>
        permissions.some(
          (permission) =>
            auth.hasPermission(
              permission,
            ),
        ),
      [auth],
    )

  const hasRole =
    useCallback(
      (role: string) =>
        auth.hasRole(role),
      [auth],
    )

  const setVisible =
    useCallback(
      (visible: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            visible,
          }),
        )
      },
      [],
    )

  const setDoNotDisturb =
    useCallback(
      (enabled: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            doNotDisturb:
              enabled,
          }),
        )
      },
      [],
    )

  const setAutonomousBehavior =
    useCallback(
      (enabled: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            autonomousBehavior:
              enabled,
          }),
        )
      },
      [],
    )

  const setProactiveComments =
    useCallback(
      (enabled: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            proactiveComments:
              enabled,
          }),
        )
      },
      [],
    )

  const setFollowPointer =
    useCallback(
      (enabled: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            followPointer:
              enabled,
          }),
        )
      },
      [],
    )

  const setReducedMotion =
    useCallback(
      (enabled: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            reducedMotion:
              enabled,
          }),
        )
      },
      [],
    )

  const setAllowFlight =
    useCallback(
      (enabled: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            allowFlight:
              enabled,
          }),
        )
      },
      [],
    )

  const setSoundEnabled =
    useCallback(
      (enabled: boolean) => {
        setPreferences(
          (current) => ({
            ...current,
            soundEnabled:
              enabled,
          }),
        )
      },
      [],
    )

  const setAnimation =
    useCallback(
      (
        animation:
          TitanAnimationState,

        priority:
          TitanPriority =
          'assistant',
      ) => {
        setRuntime(
          (current) => {
            if (
              priorityWeight[
                priority
              ] <
              priorityWeight[
                current.priority
              ]
            ) {
              return current
            }

            return {
              ...current,

              animation,

              priority,
            }
          },
        )
      },
      [],
    )

  const setPosition =
    useCallback(
      (
        position:
          TitanPosition,
      ) => {
        setRuntime(
          (current) => ({
            ...current,

            position,
          }),
        )
      },
      [],
    )

  const setVelocity =
    useCallback(
      (
        velocity:
          TitanVelocity,
      ) => {
        setRuntime(
          (current) => ({
            ...current,

            velocity,
          }),
        )
      },
      [],
    )

  const setDirection =
    useCallback(
      (
        direction:
          TitanFacingDirection,
      ) => {
        setRuntime(
          (current) => ({
            ...current,

            direction,
          }),
        )
      },
      [],
    )

  const moveToAnchor =
    useCallback(
      (
        anchor: string,

        priority:
          TitanPriority =
          'contextual',
      ) => {
        setRuntime(
          (current) => {
            if (
              priorityWeight[
                priority
              ] <
              priorityWeight[
                current.priority
              ]
            ) {
              return current
            }

            return {
              ...current,

              targetAnchor:
                anchor,

              priority,
            }
          },
        )
      },
      [],
    )

  const say =
    useCallback(
      (
        message: string,

        priority:
          TitanPriority =
          'assistant',
      ) => {
        setRuntime(
          (current) => {
            if (
              priorityWeight[
                priority
              ] <
              priorityWeight[
                current.priority
              ]
            ) {
              return current
            }

            return {
              ...current,

              message,

              animation:
                'talking',

              priority,
            }
          },
        )
      },
      [],
    )

  const clearMessage =
    useCallback(() => {
      setRuntime(
        (current) => ({
          ...current,

          message: null,

          animation:
            'idle',

          priority:
            'idle',
        }),
      )
    }, [])

  const releasePriority =
    useCallback(() => {
      setRuntime(
        (current) => ({
          ...current,

          priority:
            'idle',

          targetAnchor:
            null,
        }),
      )
    }, [])

  const resetPreferences =
    useCallback(() => {
      setPreferences(
        defaultPreferences,
      )

      setRuntime(
        (current) => ({
          ...current,

          targetAnchor:
            null,

          animation:
            'idle',

          priority:
            'idle',
        }),
      )
    }, [])

  const value =
    useMemo<
      TitanAssistantContextValue
    >(
      () => ({
        user,
        page,

        preferences,
        runtime,

        hasPermission,
        hasAnyPermission,
        hasRole,

        setVisible,
        setDoNotDisturb,
        setAutonomousBehavior,
        setProactiveComments,
        setFollowPointer,
        setReducedMotion,
        setAllowFlight,
        setSoundEnabled,

        setAnimation,
        setPosition,
        setVelocity,
        setDirection,

        moveToAnchor,

        say,
        clearMessage,

        releasePriority,

        resetPreferences,
      }),
      [
        user,
        page,
        preferences,
        runtime,

        hasPermission,
        hasAnyPermission,
        hasRole,

        setVisible,
        setDoNotDisturb,
        setAutonomousBehavior,
        setProactiveComments,
        setFollowPointer,
        setReducedMotion,
        setAllowFlight,
        setSoundEnabled,

        setAnimation,
        setPosition,
        setVelocity,
        setDirection,

        moveToAnchor,

        say,
        clearMessage,

        releasePriority,

        resetPreferences,
      ],
    )

  return (
    <TitanAssistantContext.Provider
      value={value}
    >
      {children}
    </TitanAssistantContext.Provider>
  )
}

export function useTitanAssistant():
  TitanAssistantContextValue {
  const context =
    useContext(
      TitanAssistantContext,
    )

  if (!context) {
    throw new Error(
      'useTitanAssistant must be used inside TitanAssistantProvider.',
    )
  }

  return context
}