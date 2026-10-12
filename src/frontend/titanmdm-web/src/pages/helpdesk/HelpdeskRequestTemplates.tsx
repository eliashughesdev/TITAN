import {
  useEffect,
  useMemo,
  useState,
} from 'react'

import axios
  from 'axios'

import {
  CheckCircle2,
  ClipboardList,
  X,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import './HelpdeskRequestTemplates.css'

export interface HelpdeskTemplateDraft {
  templateId: string
  revision: number

  subject: string
  description: string

  category: string

  ticketType:
    | 'incident'
    | 'request'
}

interface Props {
  disabled?: boolean

  onApply:
    (
      draft:
        HelpdeskTemplateDraft,
    ) => void
}

interface Template {
  id: string
  title: string
  description: string
  category: string

  ticketType:
    | 'incident'
    | 'request'

  questions: string[]

  isActive: boolean
  revision: number
}

function getMessage(
  error: unknown,
) {
  return (
    axios.isAxiosError<{
      message?: string
    }>(
      error,
    )
      ? error.response
          ?.data
          ?.message
      : null
  )
  ??
  'No se pudieron cargar las plantillas.'
}

export function HelpdeskRequestTemplates({
  disabled = false,
  onApply,
}: Props) {
  const [
    items,
    setItems,
  ] =
    useState<Template[]>(
      [],
    )

  const [
    loading,
    setLoading,
  ] =
    useState(
      true,
    )

  const [
    error,
    setError,
  ] =
    useState(
      '',
    )

  const [
    selectedId,
    setSelectedId,
  ] =
    useState(
      '',
    )

  const [
    subject,
    setSubject,
  ] =
    useState(
      '',
    )

  const [
    answers,
    setAnswers,
  ] =
    useState<
      Record<number, string>
    >(
      {},
    )

  useEffect(
    () => {
      const controller =
        new AbortController()

      setLoading(
        true,
      )

      setError(
        '',
      )

      void apiClient
        .get<{
          items: Template[]
        }>(
          '/my/helpdesk/templates',
          {
            signal:
              controller.signal,
          },
        )
        .then(
          response => {
            if (
              controller.signal
                .aborted
            ) {
              return
            }

            const active =
              response.data
                .items
                .filter(
                  item =>
                    item.isActive,
                )
                .sort(
                  (
                    left,
                    right,
                  ) =>
                    left.title
                      .localeCompare(
                        right.title,
                        'es',
                      ),
                )

            setItems(
              active,
            )
          },
        )
        .catch(
          exception => {
            if (
              controller.signal
                .aborted
            ) {
              return
            }

            setError(
              getMessage(
                exception,
              ),
            )
          },
        )
        .finally(
          () => {
            if (
              !controller.signal
                .aborted
            ) {
              setLoading(
                false,
              )
            }
          },
        )

      return () =>
        controller.abort()
    },
    [],
  )

  const selected =
    useMemo(
      () =>
        items.find(
          item =>
            item.id ===
            selectedId,
        ),
      [
        items,
        selectedId,
      ],
    )

  const preparedDescription =
    useMemo(
      () => {
        if (!selected) {
          return ''
        }

        return selected.questions
          .map(
            (
              question,
              index,
            ) => {
              const answer =
                (
                  answers[index]
                  ??
                  ''
                )
                .trim()

              return (
                `${question}\n` +
                `${answer}`
              )
            },
          )
          .join(
            '\n\n',
          )
          .trim()
      },
      [
        answers,
        selected,
      ],
    )

  const answersComplete =
    Boolean(
      selected
      &&
      selected.questions
        .every(
          (
            _,
            index,
          ) =>
            Boolean(
              answers[index]
                ?.trim(),
            ),
        ),
    )

  const complete =
    Boolean(
      selected
      &&
      subject
        .trim()
        .length >
        0
      &&
      answersComplete
      &&
      preparedDescription
        .length <=
        4000,
    )

  function reset() {
    setSelectedId(
      '',
    )

    setSubject(
      '',
    )

    setAnswers(
      {},
    )
  }

  function selectTemplate(
    id: string,
  ) {
    setSelectedId(
      id,
    )

    setAnswers(
      {},
    )

    const next =
      items.find(
        item =>
          item.id ===
          id,
      )

    setSubject(
      next?.title
      ??
      '',
    )
  }

  function apply() {
    if (
      !selected
      ||
      !complete
    ) {
      return
    }

    onApply({
      templateId:
        selected.id,

      revision:
        selected.revision,

      subject:
        subject.trim(),

      description:
        preparedDescription,

      category:
        selected.category,

      ticketType:
        selected.ticketType,
    })

    reset()
  }

  if (loading) {
  return (
    <section
      className="hrt rounded-2xl border border-slate-200 bg-white p-5"
      aria-busy="true"
      aria-live="polite"
    >
      <div className="flex items-center gap-3">
        <span className="h-5 w-5 animate-pulse rounded-full bg-indigo-200" />
        <div>
          <strong className="text-sm text-slate-700">
            Cargando plantillas
          </strong>
          <p className="text-xs text-slate-500">
            Preparando formularios de solicitud...
          </p>
        </div>
      </div>
    </section>
  )
}

if (error) {
  return (
    <section
      className="hrt rounded-2xl border border-red-200 bg-red-50 p-5"
      role="alert"
    >
      <div className="flex items-start gap-3">
        <span className="text-red-600">
          <ClipboardList size={22} />
        </span>

        <div className="min-w-0 flex-1">
          <strong className="text-sm text-red-800">
            No se pudieron cargar las plantillas
          </strong>

          <p className="mt-1 text-xs text-red-700">
            {error}
          </p>

          <p className="mt-2 text-xs text-red-700">
            Puedes continuar creando una solicitud manualmente.
            Las plantillas volverán a estar disponibles cuando
            se restablezca la conexión.
          </p>

          <button
            type="button"
            className="mt-3 rounded-lg border border-red-200 bg-white px-3 py-2 text-xs font-semibold text-red-800 hover:bg-red-100"
            onClick={() => window.location.reload()}
          >
            Reintentar
          </button>
        </div>
      </div>
    </section>
  )
}

if (items.length === 0) {
  return (
    <section className="hrt rounded-2xl border border-slate-200 bg-slate-50 p-5">
      <div className="flex items-start gap-3">
        <ClipboardList size={22} className="text-slate-500" />

        <div>
          <strong className="text-sm text-slate-700">
            No hay plantillas activas
          </strong>

          <p className="mt-1 text-xs text-slate-500">
            Puedes crear tu solicitud manualmente.
            El administrador TIC podrá publicar formularios
            para agilizar futuras solicitudes.
          </p>
        </div>
      </div>
    </section>
  )
}
  return (
    <section
      className="hrt"
      aria-label="Plantillas de solicitud"
    >
      <div
        className="hrt__title"
      >
        <ClipboardList
          size={17}
        />

        <div>
          <strong>
            Plantilla opcional
          </strong>

          <span>
            Utiliza una solicitud guiada
            para enviar toda la información
            necesaria al equipo TIC.
          </span>
        </div>
      </div>

      <label
        className="hrt-template-selector"
        htmlFor="helpdesk-template"
      >
        <span>
          Tipo de solicitud
        </span>

        <select
          id="helpdesk-template"
          disabled={
            disabled
          }
          value={
            selectedId
          }
          onChange={
            event =>
              selectTemplate(
                event.target.value,
              )
          }
        >
          <option value="">
            Crear manualmente
          </option>

          {items.map(
            item => (
              <option
                key={
                  item.id
                }
                value={
                  item.id
                }
              >
                {
                  item.title
                }
              </option>
            ),
          )}
        </select>
      </label>

      {selected && (
        <div
          className="hrt-template-panel"
        >
          <header
            className="hrt-template-panel__header"
          >
            <div>
              <strong>
                {
                  selected.title
                }
              </strong>

              <small>
                {
                  selected.category
                }
                {' · '}
                {
                  selected.ticketType ===
                    'incident'
                    ? 'Incidente'
                    : 'Solicitud'
                }
              </small>
            </div>

            <button
              type="button"
              aria-label="Quitar plantilla"
              disabled={
                disabled
              }
              onClick={
                reset
              }
            >
              <X
                size={16}
              />
            </button>
          </header>

          {selected.description && (
            <p
              className="hrt-template-description"
            >
              {
                selected.description
              }
            </p>
          )}

          <label>
            Asunto

            <input
              required
              maxLength={
                250
              }
              disabled={
                disabled
              }
              value={
                subject
              }
              onChange={
                event =>
                  setSubject(
                    event.target.value,
                  )
              }
            />
          </label>

          <div
            className="hrt__questions"
          >
            {selected.questions.map(
              (
                question,
                index,
              ) => (
                <label
                  key={
                    `${selected.id}-${index}`
                  }
                >
                  <span>
                    {
                      index +
                      1
                    }
                    .
                    {' '}
                    {
                      question
                    }
                  </span>

                  <textarea
                    required
                    rows={
                      3
                    }
                    maxLength={
                      800
                    }
                    disabled={
                      disabled
                    }
                    value={
                      answers[index]
                      ??
                      ''
                    }
                    onChange={
                      event =>
                        setAnswers(
                          current => ({
                            ...current,

                            [index]:
                              event.target.value,
                          }),
                        )
                    }
                  />

                  <small>
                    {
                      (
                        answers[index]
                        ??
                        ''
                      ).length
                    }
                    /800
                  </small>
                </label>
              ),
            )}
          </div>

          <footer
            className="hrt-template-footer"
          >
            <div>
              {complete ? (
                <span
                  className="hrt__complete"
                >
                  <CheckCircle2
                    size={14}
                  />

                  Información completa
                </span>
              ) : (
                <small>
                  Completa todas las preguntas.
                </small>
              )}

              <small>
                {
                  preparedDescription
                    .length
                }
                /4000 caracteres
              </small>
            </div>

            <button
              type="button"
              className="helpdesk-ui-button helpdesk-ui-button--primary"
              disabled={
                disabled
                ||
                !complete
              }
              onClick={
                apply
              }
            >
              Aplicar plantilla
            </button>
          </footer>
        </div>
      )}
    </section>
  )
}