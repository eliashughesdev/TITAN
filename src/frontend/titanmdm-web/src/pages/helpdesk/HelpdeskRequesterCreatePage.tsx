import {
  ArrowLeft,
  Headphones,
} from 'lucide-react'

import {
  useNavigate,
} from 'react-router-dom'

import {
  HelpdeskCreateRequest,
} from './HelpdeskCreateRequest'

import './HelpdeskPages.css'

export function HelpdeskRequesterCreatePage() {
  const navigate =
    useNavigate()

  return (
    <main
      className={
        'titan-page ' +
        'helpdesk-page ' +
        'my-helpdesk'
      }
    >
      <header
        className="my-helpdesk__hero"
      >
        <div>
          <span
            className="helpdesk-inbox__eyebrow"
          >
            <Headphones
              size={15}
            />

            Mesa de Ayuda
          </span>

          <h1>
            Crear solicitud
          </h1>

          <p>
            Describe lo que necesitas
            y el equipo TIC dará
            seguimiento a tu caso.
          </p>
        </div>

        <button
          type="button"
          className={
            'helpdesk-ui-button ' +
            'helpdesk-ui-button--secondary'
          }
          onClick={
            () =>
              navigate(
                '/my-support?workspace=helpdesk',
              )
          }
        >
          <ArrowLeft
            size={16}
          />

          Mis tickets
        </button>
      </header>

      <HelpdeskCreateRequest
        console={false}
        onCreated={
          ticketId =>
            navigate(
              `/my-support/${ticketId}?workspace=helpdesk`,
            )
        }
        onCancel={
          () =>
            navigate(
              '/my-support?workspace=helpdesk',
            )
        }
      />
    </main>
  )
}