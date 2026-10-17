import {
  Bell,
  CheckCheck,
  Mail,
  MailOpen,
  RefreshCw,
  XCircle,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useState,
} from 'react'

import {
  automationApi,
  type AutomationNotification,
} from '../../api/automationApi'

import {
  describeAutomationError,
  formatDate,
} from './automationUi'

interface AutomationNotificationsPanelProps {
  onUnreadChange?: (count: number) => void
}

export function AutomationNotificationsPanel({
  onUnreadChange,
}: AutomationNotificationsPanelProps) {
  const [notifications, setNotifications] =
    useState<AutomationNotification[]>([])

  const [loading, setLoading] =
    useState(true)

  const [working, setWorking] =
    useState(false)

  const [error, setError] =
    useState<string | null>(null)

  const fetchData = useCallback(
    () => automationApi.getNotifications(50),
    [],
  )

  const applyData = useCallback(
    (data: AutomationNotification[]) => {
      setNotifications(data)
      setError(null)

      onUnreadChange?.(
        data.filter((item) => !item.isRead)
          .length,
      )
    },
    [onUnreadChange],
  )

  const applyError = useCallback(
    (loadError: unknown) => {
      console.error(
        'Automation notifications error:',
        loadError,
      )

      setError(
        describeAutomationError(
          loadError,
          'No fue posible cargar las notificaciones.',
        ),
      )
    },
    [],
  )

  useEffect(() => {
    let active = true

    fetchData()
      .then((data) => {
        if (active) {
          applyData(data)
        }
      })
      .catch((loadError) => {
        if (active) {
          applyError(loadError)
        }
      })
      .finally(() => {
        if (active) {
          setLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [fetchData, applyData, applyError])

  const refresh = useCallback(
    () =>
      fetchData()
        .then((data) => {
          applyData(data)
        })
        .catch((loadError) => {
          applyError(loadError)
        })
        .finally(() => {
          setLoading(false)
        }),
    [fetchData, applyData, applyError],
  )

  const markRead = useCallback(
    async (notification: AutomationNotification) => {
      if (notification.isRead) {
        return
      }

      try {
        setWorking(true)

        await automationApi.markNotificationRead(
          notification.id,
        )

        setNotifications((current) =>
          current.map((item) =>
            item.id === notification.id
              ? { ...item, isRead: true }
              : item,
          ),
        )

        onUnreadChange?.(
          Math.max(
            0,
            notifications.filter(
              (item) => !item.isRead,
            ).length - 1,
          ),
        )
      } catch (markError) {
        console.error(
          'Automation notification read error:',
          markError,
        )

        setError(
          describeAutomationError(
            markError,
            'No fue posible marcar la notificación.',
          ),
        )
      } finally {
        setWorking(false)
      }
    },
    [notifications, onUnreadChange],
  )

  const markAllRead = useCallback(async () => {
    const pending = notifications.filter(
      (item) => !item.isRead,
    )

    if (pending.length === 0) {
      return
    }

    try {
      setWorking(true)

      await Promise.all(
        pending.map((item) =>
          automationApi.markNotificationRead(
            item.id,
          ),
        ),
      )

      setNotifications((current) =>
        current.map((item) => ({
          ...item,
          isRead: true,
        })),
      )

      onUnreadChange?.(0)
    } catch (markError) {
      console.error(
        'Automation notifications read-all error:',
        markError,
      )

      setError(
        describeAutomationError(
          markError,
          'No fue posible marcar las notificaciones.',
        ),
      )
    } finally {
      setWorking(false)
    }
  }, [notifications, onUnreadChange])

  const unread = notifications.filter(
    (item) => !item.isRead,
  ).length

  return (
    <section className="automation-notifications">
      <header className="automation-panel-header">
        <div>
          <Bell size={18} />
          <div>
            <strong>
              Bandeja de notificaciones
            </strong>
            <span>
              Avisos generados por las reglas
              empresariales.
            </span>
          </div>
        </div>

        <div className="automation-panel-actions">
          <button
            type="button"
            className="automation-secondary"
            disabled={working || unread === 0}
            onClick={() => void markAllRead()}
          >
            <CheckCheck size={15} />
            Marcar todo leído
          </button>

          <button
            type="button"
            className="automation-secondary"
            disabled={loading}
            onClick={() => {
              setLoading(true)
              void refresh()
            }}
          >
            <RefreshCw size={15} />
            Actualizar
          </button>
        </div>
      </header>

      {error && (
        <div className="automation-notice error">
          <XCircle size={16} />
          {error}
        </div>
      )}

      {loading && notifications.length === 0 ? (
        <div className="automation-panel-loading">
          Cargando notificaciones…
        </div>
      ) : notifications.length === 0 ? (
        <div className="automation-empty">
          <Bell size={30} />
          <strong>Sin notificaciones</strong>
          <span>
            Las reglas empresariales todavía no
            han generado avisos.
          </span>
        </div>
      ) : (
        <ul className="automation-notification-list">
          {notifications.map((notification) => (
            <li
              key={notification.id}
              className={
                notification.isRead
                  ? 'automation-notification read'
                  : 'automation-notification'
              }
            >
              <div className="automation-notification-icon">
                {notification.isRead ? (
                  <MailOpen size={17} />
                ) : (
                  <Mail size={17} />
                )}
              </div>

              <div className="automation-notification-body">
                <div className="automation-notification-top">
                  <strong>
                    {notification.title}
                  </strong>

                  <span
                    className={
                      'automation-severity ' +
                      notification.severity.toLowerCase()
                    }
                  >
                    {notification.severity}
                  </span>
                </div>

                <p>{notification.message}</p>

                <div className="automation-notification-meta">
                  <span>
                    {notification.automationName ??
                      'Regla eliminada'}
                  </span>

                  <span>
                    {notification.channel}
                  </span>

                  <span>
                    {formatDate(
                      notification.createdAtUtc,
                    )}
                  </span>
                </div>
              </div>

              <button
                type="button"
                className="automation-secondary"
                disabled={
                  working || notification.isRead
                }
                onClick={() =>
                  void markRead(notification)
                }
              >
                {notification.isRead
                  ? 'Leída'
                  : 'Marcar leída'}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
