import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
} from 'react'

import axios from 'axios'

import apiClient from '../../api/apiClient'

import {
  HelpdeskRequestTemplates,
  type HelpdeskTemplateDraft,
} from './HelpdeskRequestTemplates'

type Group = {
  id: string
  name: string
  categories: string[]
}

type Props = {
  console?: boolean
  onCreated: (id: string) => void
  onCancel: () => void
}

type AssistantSuggestionResponse = {
  suggestedSubject?: string
  suggestedCategory?: string
  recommendations?: string[]
}

export function HelpdeskCreateRequest({
  console: consoleMode = false,
  onCreated,
  onCancel,
}: Props) {
  const [groups, setGroups] = useState<Group[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const [groupId, setGroupId] = useState('')
  const [category, setCategory] = useState('')
  const [subject, setSubject] = useState('')
  const [description, setDescription] = useState('')

  const [type, setType] =
    useState<'incident' | 'request'>('incident')

  const [assistantEnabled, setAssistantEnabled] =
    useState(false)

  const [suggesting, setSuggesting] =
    useState(false)

  const [recommendations, setRecommendations] =
    useState<string[]>([])

  const [notice, setNotice] =
    useState('')

  const firstInput =
    useRef<HTMLInputElement | null>(null)

  const dialog =
    useRef<HTMLElement | null>(null)

  const submitting =
    useRef(false)

  const selected =
    useMemo(
      () =>
        groups.find(
          group =>
            group.id === groupId,
        ),
      [groupId, groups],
    )

  /*
   * ============================================================
   * INITIALIZATION
   * ============================================================
   */
  useEffect(() => {
    const abort =
      new AbortController()

    /*
     * IMPORTANTE:
     *
     * No separar "as HTMLElement | null"
     * en diferentes líneas.
     *
     * En TSX puede terminar interpretándose
     * incorrectamente por el parser.
     */
    const previous =
      document.activeElement as HTMLElement | null

    firstInput.current?.focus()

    /*
     * ----------------------------------------------------------
     * GROUPS / CATEGORIES AVAILABLE TO CURRENT USER
     * ----------------------------------------------------------
     */
    void apiClient
      .get<Group[]>(
        '/my/helpdesk/request-form/groups',
        {
          signal:
            abort.signal,
        },
      )
      .then(
        ({ data }) => {
          if (
            abort.signal.aborted
          ) {
            return
          }

          setGroups(
            Array.isArray(data)
              ? data
              : [],
          )
        },
      )
      .catch(
        exception => {
          if (
            abort.signal.aborted
          ) {
            return
          }

          console.error(
            'Helpdesk groups loading failed.',
            exception,
          )

          setError(
            'No se pudieron cargar los grupos. ' +
            'Cierra y vuelve a abrir el formulario.',
          )
        },
      )
      .finally(() => {
        if (
          !abort.signal.aborted
        ) {
          setLoading(false)
        }
      })

    /*
     * ----------------------------------------------------------
     * ASSISTANT AVAILABILITY
     * ----------------------------------------------------------
     *
     * Un error aquí NO puede impedir
     * la creación manual de tickets.
     */
    void apiClient
      .get<{
        enabled: boolean
      }>(
        '/helpdesk/operations/assistant/me',
        {
          signal:
            abort.signal,
        },
      )
      .then(
        ({ data }) => {
          if (
            abort.signal.aborted
          ) {
            return
          }

          setAssistantEnabled(
            data.enabled === true,
          )
        },
      )
      .catch(() => {
        /*
         * Fallo silencioso intencional.
         * Helpdesk sigue siendo funcional
         * aunque IA esté deshabilitada.
         */
        setAssistantEnabled(false)
      })

    return () => {
      abort.abort()

      /*
       * Devolver foco al elemento
       * desde el cual se abrió el modal.
       */
      previous?.focus()
    }
  }, [])

  /*
   * ============================================================
   * TEMPLATE APPLICATION
   * ============================================================
   */
  function applyTemplate(
    draft: HelpdeskTemplateDraft,
  ) {
    setSubject(
      draft.subject,
    )

    setDescription(
      draft.description,
    )

    setType(
      draft.ticketType,
    )

    /*
     * Buscar grupos que soporten
     * la categoría de la plantilla.
     */
    const matchingGroups =
      groups.filter(
        group =>
          group.categories.some(
            item =>
              item.localeCompare(
                draft.category,
                undefined,
                {
                  sensitivity:
                    'accent',
                },
              ) === 0,
          ),
      )

    /*
     * Si el grupo actualmente seleccionado
     * ya soporta la categoría,
     * lo conservamos.
     */
    const currentMatch =
      matchingGroups.find(
        group =>
          group.id === groupId,
      )

    /*
     * Si solamente existe un grupo compatible,
     * podemos seleccionarlo automáticamente.
     */
    const preferred =
      currentMatch ??
      (
        matchingGroups.length === 1
          ? matchingGroups[0]
          : undefined
      )

    if (preferred) {
      const realCategory =
        preferred.categories.find(
          item =>
            item.localeCompare(
              draft.category,
              undefined,
              {
                sensitivity:
                  'accent',
              },
            ) === 0,
        )

      setGroupId(
        preferred.id,
      )

      setCategory(
        realCategory ??
        draft.category,
      )

      setNotice(
        'Plantilla aplicada correctamente.',
      )

      return
    }

    /*
     * No inventamos grupo si existen
     * múltiples candidatos.
     */
    setGroupId('')
    setCategory('')

    if (
      matchingGroups.length > 1
    ) {
      setNotice(
        'Plantilla aplicada. ' +
        'Selecciona el grupo de trabajo correspondiente.',
      )

      return
    }

    setNotice(
      'Plantilla aplicada. ' +
      'Selecciona el grupo y la categoría correspondientes.',
    )
  }

  /*
   * ============================================================
   * OPENROUTER / TITAN ASSISTANT
   * ============================================================
   */
  async function suggest() {
    if (
      suggesting ||
      saving
    ) {
      return
    }

    const cleanSubject =
      subject.trim()

    const cleanDescription =
      description.trim()

    if (
      cleanDescription.length < 15
    ) {
      setNotice(
        'Describe el problema con un poco más de detalle ' +
        'antes de solicitar una sugerencia.',
      )

      return
    }

    setSuggesting(true)
    setNotice('')
    setRecommendations([])

    try {
      const { data } =
        await apiClient
          .post<AssistantSuggestionResponse>(
            '/my/helpdesk/assistant/suggest',
            {
              subject:
                cleanSubject,

              description:
                cleanDescription,
            },
          )

      const suggestedSubject =
        data.suggestedSubject
          ?.trim() ?? ''

      const suggestedCategory =
        data.suggestedCategory
          ?.trim() ?? ''

      /*
       * El modelo puede sugerir un mejor asunto,
       * pero nunca debe crear el ticket directamente.
       */
      if (
        suggestedSubject.length > 0
      ) {
        setSubject(
          suggestedSubject,
        )
      }

      /*
       * Intentar asociar categoría
       * solamente cuando exista realmente
       * dentro de TitanMDM.
       *
       * No confiamos ciegamente
       * en una categoría generada por IA.
       */
      if (
        suggestedCategory.length > 0
      ) {
        const compatibleGroups =
          groups.filter(
            group =>
              group.categories.some(
                candidate =>
                  candidate.localeCompare(
                    suggestedCategory,
                    undefined,
                    {
                      sensitivity:
                        'accent',
                    },
                  ) === 0,
              ),
          )

        const currentCompatible =
          compatibleGroups.find(
            group =>
              group.id === groupId,
          )

        const preferred =
          currentCompatible ??
          (
            compatibleGroups.length === 1
              ? compatibleGroups[0]
              : undefined
          )

        if (preferred) {
          const realCategory =
            preferred.categories.find(
              candidate =>
                candidate.localeCompare(
                  suggestedCategory,
                  undefined,
                  {
                    sensitivity:
                      'accent',
                  },
                ) === 0,
            )

          setGroupId(
            preferred.id,
          )

          if (
            realCategory
          ) {
            setCategory(
              realCategory,
            )
          }
        }
      }

      const validRecommendations =
        Array.isArray(
          data.recommendations,
        )
          ? data.recommendations
              .map(
                item =>
                  String(item)
                    .trim(),
              )
              .filter(
                item =>
                  item.length > 0,
              )
              .slice(
                0,
                5,
              )
          : []

      setRecommendations(
        validRecommendations,
      )

      setNotice(
        'Titan analizó la solicitud. ' +
        'Revisa las recomendaciones antes de crear el ticket.',
      )
    }
    catch (
      exception
    ) {
      console.error(
        'Helpdesk assistant suggestion failed.',
        exception,
      )

      /*
       * OpenRouter jamás debe convertirse
       * en dependencia obligatoria
       * para crear un ticket.
       */
      setNotice(
        'Titan no está disponible temporalmente. ' +
        'Puedes continuar creando la solicitud normalmente.',
      )
    }
    finally {
      setSuggesting(false)
    }
  }

  /*
   * ============================================================
   * TICKET CREATION
   * ============================================================
   */
  async function submit(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      submitting.current ||
      suggesting
    ) {
      return
    }

    const cleanSubject =
      subject.trim()

    const cleanDescription =
      description.trim()

    if (
      cleanSubject.length === 0
    ) {
      setError(
        'El asunto es obligatorio.',
      )

      return
    }

    if (
      cleanDescription.length === 0
    ) {
      setError(
        'La descripción es obligatoria.',
      )

      return
    }

    if (
      !selected
    ) {
      setError(
        'Selecciona un grupo de trabajo.',
      )

      return
    }

    if (
      !selected.categories.includes(
        category,
      )
    ) {
      setError(
        'Selecciona una categoría válida.',
      )

      return
    }

    submitting.current =
      true

    setSaving(true)
    setError('')

    try {
      const { data } =
        await apiClient
          .post<{
            id: string
          }>(
            '/my/helpdesk/request-form/tickets',
            {
              subject:
                cleanSubject,

              description:
                cleanDescription,

              type,

              groupId,

              category,

              console:
                consoleMode,
            },
          )

      if (
        !data?.id
      ) {
        throw new Error(
          'Ticket created without id.',
        )
      }

      onCreated(
        data.id,
      )
    }
    catch (
      exception
    ) {
      console.error(
        'Helpdesk ticket creation failed.',
        exception,
      )

      setError(
        axios.isAxiosError(
          exception,
        )
        &&
        typeof exception
          .response
          ?.data
          ?.message ===
          'string'
          ? exception
              .response
              .data
              .message
          : 'No se pudo crear la solicitud.',
      )
    }
    finally {
      submitting.current =
        false

      setSaving(false)
    }
  }

  /*
   * ============================================================
   * UI STATE
   * ============================================================
   */
  const locked =
    saving ||
    suggesting

  const ticketIsValid =
    !loading &&
    !locked &&
    subject.trim().length > 0 &&
    description.trim().length > 0 &&
    !!selected &&
    selected.categories.includes(
      category,
    )

  /*
   * ============================================================
   * RENDER
   * ============================================================
   */
  return (
    <div
      className="helpdesk-inbox__overlay"
      onMouseDown={
        event => {
          if (
            event.target ===
              event.currentTarget
            &&
            !locked
          ) {
            onCancel()
          }
        }
      }
    >
      <section
        ref={dialog}
        className="helpdesk-inbox__dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="hd-create-title"
        onKeyDown={
          event => {
            /*
             * ESC
             */
            if (
              event.key ===
                'Escape'
              &&
              !locked
            ) {
              onCancel()

              return
            }

            /*
             * Focus trap
             */
            if (
              event.key !==
              'Tab'
            ) {
              return
            }

            const nodes =
              dialog.current
                ?.querySelectorAll<HTMLElement>(
                  [
                    'button:not(:disabled)',
                    'input:not(:disabled)',
                    'textarea:not(:disabled)',
                    'select:not(:disabled)',
                    'a[href]',
                  ].join(','),
                )

            if (
              !nodes ||
              nodes.length === 0
            ) {
              return
            }

            const first =
              nodes[0]

            const last =
              nodes[
                nodes.length -
                1
              ]

            if (
              event.shiftKey &&
              document.activeElement ===
                first
            ) {
              event.preventDefault()

              last.focus()

              return
            }

            if (
              !event.shiftKey &&
              document.activeElement ===
                last
            ) {
              event.preventDefault()

              first.focus()
            }
          }
        }
      >
        <header>
          <div>
            <h2
              id="hd-create-title"
            >
              {
                consoleMode
                  ? 'Nuevo ticket'
                  : 'Nueva solicitud'
              }
            </h2>

            <p>
              Completa la solicitud
              o utiliza una plantilla.
            </p>
          </div>

          <button
            type="button"
            aria-label="Cerrar"
            disabled={
              locked
            }
            onClick={
              onCancel
            }
          >
            ×
          </button>
        </header>

        {error && (
          <div
            className="helpdesk-inbox__error"
            role="alert"
          >
            {error}
          </div>
        )}

        <form
          onSubmit={
            event =>
              void submit(
                event,
              )
          }
        >
          {/*
           * ====================================================
           * TEMPLATE SELECTOR
           * ====================================================
           *
           * Si no existen plantillas activas,
           * HelpdeskRequestTemplates retorna null.
           */}
          <HelpdeskRequestTemplates
            disabled={
              loading ||
              locked
            }
            onApply={
              applyTemplate
            }
          />

          {notice && (
            <div
              role="status"
              aria-live="polite"
            >
              {notice}
            </div>
          )}

          <label>
            Asunto

            <input
              ref={
                firstInput
              }
              required
              maxLength={250}
              value={
                subject
              }
              disabled={
                locked
              }
              onChange={
                event => {
                  setSubject(
                    event
                      .target
                      .value,
                  )

                  if (
                    error
                  ) {
                    setError('')
                  }
                }
              }
            />
          </label>

          <label>
            Descripción

            <textarea
              required
              maxLength={4000}
              rows={5}
              value={
                description
              }
              disabled={
                locked
              }
              onChange={
                event => {
                  setDescription(
                    event
                      .target
                      .value,
                  )

                  if (
                    error
                  ) {
                    setError('')
                  }
                }
              }
            />
          </label>

          <div
            className="helpdesk-inbox__form-grid"
          >
            <label>
              Tipo

              <select
                value={
                  type
                }
                disabled={
                  locked
                }
                onChange={
                  event =>
                    setType(
                      event
                        .target
                        .value ===
                        'request'
                        ? 'request'
                        : 'incident',
                    )
                }
              >
                <option value="incident">
                  Incidente
                </option>

                <option value="request">
                  Solicitud
                </option>
              </select>
            </label>

            <label>
              Grupo de trabajo

              <select
                required
                value={
                  groupId
                }
                disabled={
                  loading ||
                  locked
                }
                onChange={
                  event => {
                    setGroupId(
                      event
                        .target
                        .value,
                    )

                    setCategory('')
                    setNotice('')
                    setError('')
                  }
                }
              >
                <option value="">
                  {
                    loading
                      ? 'Cargando grupos…'
                      : 'Selecciona un grupo'
                  }
                </option>

                {groups.map(
                  group => (
                    <option
                      key={
                        group.id
                      }
                      value={
                        group.id
                      }
                    >
                      {group.name}
                    </option>
                  ),
                )}
              </select>
            </label>
          </div>

          <label>
            Categoría

            <select
              required
              value={
                category
              }
              disabled={
                loading ||
                locked ||
                !selected
                  ?.categories
                  .length
              }
              onChange={
                event => {
                  setCategory(
                    event
                      .target
                      .value,
                  )

                  setError('')
                }
              }
            >
              <option value="">
                Selecciona una categoría
              </option>

              {selected
                ?.categories
                .map(
                  item => (
                    <option
                      key={
                        item
                      }
                      value={
                        item
                      }
                    >
                      {item}
                    </option>
                  ),
                )}
            </select>
          </label>

          {!loading &&
            groups.length ===
              0 && (
              <p>
                No hay grupos activos.
                TIC debe configurar
                grupos y categorías.
              </p>
            )}

          {selected &&
            selected.categories
              .length ===
              0 && (
              <p>
                Este grupo no tiene
                categorías configuradas.
              </p>
            )}

          <p>
            La prioridad se determina
            automáticamente por el sistema
            o por el equipo TIC.
          </p>

          {/*
           * ====================================================
           * TITAN / OPENROUTER
           * ====================================================
           */}
          {assistantEnabled && (
            <div
              className="my-helpdesk__assistant"
            >
              <button
                type="button"
                className={
                  'helpdesk-ui-button ' +
                  'helpdesk-ui-button--secondary'
                }
                disabled={
                  locked ||
                  description
                    .trim()
                    .length <
                    15
                }
                onClick={
                  () =>
                    void suggest()
                }
              >
                {
                  suggesting
                    ? 'Titan está analizando…'
                    : 'Pedir sugerencia a Titan'
                }
              </button>

              {recommendations.length >
                0 && (
                <div>
                  <strong>
                    Recomendaciones
                  </strong>

                  <ul>
                    {
                      recommendations.map(
                        (
                          recommendation,
                          index,
                        ) => (
                          <li
                            key={
                              `${index}-${recommendation}`
                            }
                          >
                            {
                              recommendation
                            }
                          </li>
                        ),
                      )
                    }
                  </ul>
                </div>
              )}
            </div>
          )}

          <div
            className="helpdesk-inbox__dialog-actions"
          >
            <button
              type="button"
              className={
                'helpdesk-ui-button ' +
                'helpdesk-ui-button--secondary'
              }
              disabled={
                locked
              }
              onClick={
                onCancel
              }
            >
              Cancelar
            </button>

            <button
              type="submit"
              className={
                'helpdesk-ui-button ' +
                'helpdesk-ui-button--primary'
              }
              disabled={
                !ticketIsValid
              }
            >
              {
                saving
                  ? 'Creando…'
                  : 'Crear ticket'
              }
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}