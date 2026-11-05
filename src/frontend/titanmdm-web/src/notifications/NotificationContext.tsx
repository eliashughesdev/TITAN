/*
 * Fase 3 - Sistema de notificaciones unificado de TitanMDM.
 *
 * - Centro de alertas: GET /api/notifications (SQL Server, filtrado
 *   por RBAC y sede). Polling cada 30 s y al recuperar el foco.
 * - Toasts: alertas criticas nuevas + cualquier modulo via
 *   useNotifications().notify(...) + errores 5xx/red del API
 *   (evento titanmdm:api-error emitido por apiClient).
 * - Estado "leido" por usuario en localStorage (los ids del feed son
 *   estables mientras la condicion no cambie).
 */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'

import { Link } from 'react-router-dom'

import { API_ERROR_EVENT, type ApiErrorEventDetail } from '../api/apiEvents'
import { notificationsApi, type NotificationItem, type NotificationSeverity } from '../api/notificationsApi'
import { useAuth } from '../auth/AuthContext'
import './notifications.css'


const POLL_INTERVAL_MS = 30_000
const TOAST_TTL_MS = 6_000
const MAX_TOASTS = 4
const MAX_READ_IDS = 500

export type ToastKind = 'success' | 'error' | 'info' | 'warning'

export interface Toast {
  id: string
  kind: ToastKind
  title: string
  message?: string
  link?: string | null
}

interface NotificationContextValue {
  items: NotificationItem[]
  unreadCount: number
  loading: boolean
  error: string
  unavailableSources: string[]
  toasts: Toast[]
  isRead: (id: string) => boolean
  markRead: (id: string) => void
  markAllRead: () => void
  refresh: () => Promise<void>
  notify: (toast: Omit<Toast, 'id'>) => void
  dismissToast: (id: string) => void
}

const NotificationContext = createContext<NotificationContextValue | undefined>(undefined)

const storageKey = (userId: string) => `titanmdm.notifications.read.${userId}`

function loadReadIds(userId: string): Set<string> {
  try {
    const raw: unknown = JSON.parse(localStorage.getItem(storageKey(userId)) ?? '[]')
    return new Set(Array.isArray(raw) ? raw.filter((x): x is string => typeof x === 'string') : [])
  } catch {
    return new Set()
  }
}

function saveReadIds(userId: string, ids: Set<string>) {
  try {
    localStorage.setItem(storageKey(userId), JSON.stringify([...ids].slice(-MAX_READ_IDS)))
  } catch {
    // almacenamiento no disponible: el estado vive solo en memoria
  }
}

const toastKindFor = (severity: NotificationSeverity): ToastKind =>
  severity === 'critical' ? 'error' : severity === 'warning' ? 'warning' : 'info'

export function NotificationProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const userId = user?.id ?? null

  const [items, setItems] = useState<NotificationItem[]>([])
  const [unavailableSources, setUnavailableSources] = useState<string[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [readIds, setReadIds] = useState<Set<string>>(new Set())
  const [toasts, setToasts] = useState<Toast[]>([])

  const knownIdsRef = useRef<Set<string> | null>(null)
  const timersRef = useRef(new Map<string, number>())

  const dismissToast = useCallback((id: string) => {
    setToasts(current => current.filter(toast => toast.id !== id))
    const timer = timersRef.current.get(id)
    if (timer) {
      window.clearTimeout(timer)
      timersRef.current.delete(id)
    }
  }, [])

  const notify = useCallback(
    (toast: Omit<Toast, 'id'>) => {
      const id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
      setToasts(current => [...current, { ...toast, id }].slice(-MAX_TOASTS))
      timersRef.current.set(id, window.setTimeout(() => dismissToast(id), TOAST_TTL_MS))
    },
    [dismissToast],
  )

  useEffect(() => {
    const timers = timersRef.current
    return () => {
      timers.forEach(timer => window.clearTimeout(timer))
      timers.clear()
    }
  }, [])

  // Estado leido por usuario; se reinicia al cambiar de sesion.
  useEffect(() => {
    knownIdsRef.current = null
    setItems([])
    setReadIds(userId ? loadReadIds(userId) : new Set())
  }, [userId])

  const refresh = useCallback(async () => {
    if (!userId) return

    setLoading(true)
    try {
      const feed = await notificationsApi.feed()
      setItems(feed.items)
      setUnavailableSources(feed.unavailableSources)
      setError('')

      // Toast solo para alertas criticas que aparecen despues de la primera carga.
      const known = knownIdsRef.current
      if (known) {
        feed.items
          .filter(item => item.severity === 'critical' && !known.has(item.id))
          .slice(0, 2)
          .forEach(item =>
            notify({ kind: toastKindFor(item.severity), title: item.title, message: item.message, link: item.link }),
          )
      }
      knownIdsRef.current = new Set(feed.items.map(item => item.id))
    } catch {
      setError('No fue posible actualizar las notificaciones.')
    } finally {
      setLoading(false)
    }
  }, [notify, userId])

  useEffect(() => {
    if (!userId) return

    void refresh()
    const interval = window.setInterval(() => {
      if (document.visibilityState === 'visible') void refresh()
    }, POLL_INTERVAL_MS)
    const onVisible = () => {
      if (document.visibilityState === 'visible') void refresh()
    }
    document.addEventListener('visibilitychange', onVisible)

    return () => {
      window.clearInterval(interval)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [refresh, userId])

  // Manejo de errores unificado: fallos de servidor/red del API.
  useEffect(() => {
    const onApiError = (event: Event) => {
      const detail = (event as CustomEvent<ApiErrorEventDetail | undefined>).detail
      notify({
        kind: 'error',
        title: 'Error de comunicación con TitanMDM',
        message: detail?.message ?? 'El servidor no pudo completar la operación.',
      })
    }
    window.addEventListener(API_ERROR_EVENT, onApiError)
    return () => window.removeEventListener(API_ERROR_EVENT, onApiError)
  }, [notify])

  const markRead = useCallback(
    (id: string) => {
      if (!userId) return
      setReadIds(current => {
        if (current.has(id)) return current
        const next = new Set(current).add(id)
        saveReadIds(userId, next)
        return next
      })
    },
    [userId],
  )

  const markAllRead = useCallback(() => {
    if (!userId) return
    setReadIds(current => {
      const next = new Set(current)
      items.forEach(item => next.add(item.id))
      saveReadIds(userId, next)
      return next
    })
  }, [items, userId])

  const isRead = useCallback((id: string) => readIds.has(id), [readIds])

  const unreadCount = useMemo(() => items.filter(item => !readIds.has(item.id)).length, [items, readIds])

  const value = useMemo<NotificationContextValue>(
    () => ({
      items,
      unreadCount,
      loading,
      error,
      unavailableSources,
      toasts,
      isRead,
      markRead,
      markAllRead,
      refresh,
      notify,
      dismissToast,
    }),
    [items, unreadCount, loading, error, unavailableSources, toasts, isRead, markRead, markAllRead, refresh, notify, dismissToast],
  )

  return (
    <NotificationContext.Provider value={value}>
      {children}
      <Toaster toasts={toasts} onDismiss={dismissToast} />
    </NotificationContext.Provider>
  )
}

function Toaster({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: string) => void }) {
  if (toasts.length === 0) return null

  return (
    <div className="titan-toaster" role="region" aria-label="Notificaciones emergentes" aria-live="polite">
      {toasts.map(toast => (
        <div key={toast.id} className={`titan-toast titan-toast--${toast.kind}`} role="status">
          <div className="titan-toast__body">
            <strong>{toast.title}</strong>
            {toast.message && <span>{toast.message}</span>}
            {toast.link && (
              <Link className="titan-toast__link" to={toast.link} onClick={() => onDismiss(toast.id)}>
                Ver detalle
              </Link>
            )}
          </div>
          <button type="button" className="titan-toast__close" aria-label="Cerrar" onClick={() => onDismiss(toast.id)}>
            ×
          </button>
        </div>
      ))}
    </div>
  )
}

export function useNotifications(): NotificationContextValue {
  const context = useContext(NotificationContext)
  if (!context) {
    throw new Error('useNotifications must be used inside NotificationProvider.')
  }
  return context
}
