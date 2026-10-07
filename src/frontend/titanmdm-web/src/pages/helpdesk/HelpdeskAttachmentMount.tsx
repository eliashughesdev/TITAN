import {
  useEffect,
  useState,
} from 'react'
import {
  createPortal,
} from 'react-dom'
import {
  useLocation,
} from 'react-router-dom'
import {
  useAuth,
} from '../../auth/AuthContext'
import {
  HelpdeskAttachmentPanel,
} from './HelpdeskAttachmentPanel'
import './HelpdeskAttachments.css'

interface TicketRoute {
  ticketId: string
  staff: boolean
}

function getTicketRoute(
  pathname: string,
): TicketRoute | null {
  const staff =
    pathname.match(
      /^\/helpdesk\/tickets\/([0-9a-f-]{36})\/?$/i,
    )

  if (staff) {
    return {
      ticketId:
        staff[1],
      staff: true,
    }
  }

  const personal =
    pathname.match(
      /^\/my-support\/([0-9a-f-]{36})\/?$/i,
    )

  if (personal) {
    return {
      ticketId:
        personal[1],
      staff: false,
    }
  }

  return null
}

export function HelpdeskAttachmentMount() {
  const location =
    useLocation()
  const { user } =
    useAuth()

  const route =
    getTicketRoute(
      location.pathname,
    )

  const [
    target,
    setTarget,
  ] =
    useState<
      Element | null
    >(null)

  useEffect(() => {
    if (!route) {
      setTarget(null)
      return
    }

    const selector =
      route.staff
        ? '.helpdesk-detail__main'
        : '.my-helpdesk__card:last-of-type'

    function locate() {
      const element =
        document.querySelector(
          selector,
        )

      setTarget(
        element,
      )

      return Boolean(
        element,
      )
    }

    if (locate())
      return

    const observer =
      new MutationObserver(
        () => {
          if (
            locate()
          ) {
            observer.disconnect()
          }
        },
      )

    observer.observe(
      document.body,
      {
        childList: true,
        subtree: true,
      },
    )

    return () =>
      observer.disconnect()
  }, [
    location.pathname,
    route?.ticketId,
    route?.staff,
  ])

  if (!route || !target)
    return null

  const canUpload =
    route.staff
      ? user?.permissions
          ?.includes(
            'tickets.comment',
          ) ?? false
      : true

  return createPortal(
    <HelpdeskAttachmentPanel
      key={
        `${route.ticketId}-${route.staff}`
      }
      ticketId={
        route.ticketId
      }
      staff={
        route.staff
      }
      canUpload={
        canUpload
      }
    />,
    target,
  )
}