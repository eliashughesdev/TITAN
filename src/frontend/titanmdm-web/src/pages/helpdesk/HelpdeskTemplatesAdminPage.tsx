import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type FormEvent,
} from 'react'

import axios
  from 'axios'

import {
  CheckCircle2,
  ClipboardList,
  Edit3,
  FilePlus2,
  Plus,
  RefreshCw,
  Save,
  Trash2,
  X,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import './HelpdeskPages.css'
import './HelpdeskTemplatesAdminPage.css'

interface Template {
  id: string
  title: string
  description: string
  category: string
  ticketType: string
  isActive: boolean
  revision: number
  questions: string[]
  updatedAtUtc: string
}

interface TemplateResult {
  items: Template[]
  canManage: boolean
}

interface PlanningCatalog {
  groups: {
    id: string
    name: string
    tasks: string[]
    isActive: boolean
  }[]
}

interface Editor {
  id: string | null
  title: string
  description: string
  category: string
  ticketType: string
  questions: string[]
  isActive: boolean
  revision: number | null
}

const emptyEditor:
  Editor = {
    id:
      null,

    title:
      '',

    description:
      '',

    category:
      'general',

    ticketType:
      'incident',

    questions:
      [],

    isActive:
      true,

    revision:
      null,
  }

function getError(
  exception: unknown,
) {
  if (
    axios.isAxiosError(
      exception)
  ) {
    const data =
      exception.response
        ?.data as
        | {
            message?: string
          }
        | undefined

    return data?.message
      ??
      `No se pudo completar la operación (${exception.response?.status ?? 'sin conexión'}).`
  }

  return exception instanceof Error
    ? exception.message
    : 'No se pudo completar la operación.'
}

export function HelpdeskTemplatesAdminPage() {
  const [
    templates,
    setTemplates,
  ] =
    useState<Template[]>(
      [],
    )

  const [
    categories,
    setCategories,
  ] =
    useState<string[]>(
      [
        'general',
      ],
    )

  const [
    editor,
    setEditor,
  ] =
    useState<Editor>(
      emptyEditor,
    )

  const [
    question,
    setQuestion,
  ] =
    useState(
      '',
    )

  const [
    loading,
    setLoading,
  ] =
    useState(
      true,
    )

  const [
    saving,
    setSaving,
  ] =
    useState(
      false,
    )

  const [
    canManage,
    setCanManage,
  ] =
    useState(
      false,
    )

  const [
    showInactive,
    setShowInactive,
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
    success,
    setSuccess,
  ] =
    useState(
      '',
    )

  const load =
    useCallback(
      async () => {
        setLoading(
          true,
        )

        setError(
          '',
        )

        try {
          const [
            templatesResponse,
            planningResponse,
          ] =
            await Promise.all(
              [
                apiClient
                  .get<
                    TemplateResult
                  >(
                    '/my/helpdesk/templates',
                    {
                      params: {
                        all:
                          true,
                      },
                    },
                  ),

                apiClient
                  .get<
                    PlanningCatalog
                  >(
                    '/helpdesk/group-planning',
                  ),
              ],
            )

          setTemplates(
            templatesResponse
              .data
              .items,
          )

          setCanManage(
            templatesResponse
              .data
              .canManage,
          )

          const discovered =
            planningResponse
              .data
              .groups
              .filter(
                group =>
                  group.isActive,
              )
              .flatMap(
                group =>
                  group.tasks,
              )
              .map(
                value =>
                  value
                    .trim()
                    .toLowerCase(),
              )
              .filter(
                Boolean,
              )

          setCategories(
            [
              ...new Set(
                [
                  'general',
                  ...discovered,
                ],
              ),
            ].sort(),
          )
        }
        catch (
          exception
        ) {
          setError(
            getError(
              exception,
            ),
          )
        }
        finally {
          setLoading(
            false,
          )
        }
      },
      [],
    )

  useEffect(
    () => {
      void load()
    },
    [
      load,
    ],
  )

  const visibleTemplates =
    useMemo(
      () =>
        templates.filter(
          item =>
            showInactive
            ||
            item.isActive,
        ),
      [
        templates,
        showInactive,
      ],
    )

  function createNew() {
    setEditor(
      emptyEditor,
    )

    setQuestion(
      '',
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )
  }

  function edit(
    item:
      Template,
  ) {
    setEditor({
      id:
        item.id,

      title:
        item.title,

      description:
        item.description,

      category:
        item.category,

      ticketType:
        item.ticketType,

      questions:
        [
          ...item.questions,
        ],

      isActive:
        item.isActive,

      revision:
        item.revision,
    })

    setQuestion(
      '',
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )
  }

  function addQuestion() {
    const value =
      question
        .trim()

    if (!value) {
      return
    }

    if (
      editor.questions
        .some(
          existing =>
            existing
              .toLowerCase() ===
            value
              .toLowerCase(),
        )
    ) {
      setError(
        'Esa pregunta ya existe en la plantilla.',
      )

      return
    }

    setEditor(
      current => ({
        ...current,

        questions: [
          ...current.questions,
          value,
        ],
      }),
    )

    setQuestion(
      '',
    )

    setError(
      '',
    )
  }

  function removeQuestion(
    index:
      number,
  ) {
    setEditor(
      current => ({
        ...current,

        questions:
          current.questions
            .filter(
              (
                _,
                position,
              ) =>
                position !==
                index,
            ),
      }),
    )
  }

  async function save(
    event:
      FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (
      !canManage
      ||
      saving
    ) {
      return
    }

    if (
      !editor.title
        .trim()
    ) {
      setError(
        'Indica el nombre de la plantilla.',
      )

      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    const payload = {
      title:
        editor.title
          .trim(),

      description:
        editor.description
          .trim(),

      category:
        editor.category,

      ticketType:
        editor.ticketType,

      questions:
        editor.questions,

      isActive:
        editor.isActive,

      revision:
        editor.revision,
    }

    try {
      if (editor.id) {
        await apiClient
          .put(
            `/my/helpdesk/templates/${editor.id}`,
            payload,
          )

        setSuccess(
          'Plantilla actualizada correctamente.',
        )
      }
      else {
        await apiClient
          .post(
            '/my/helpdesk/templates',
            payload,
          )

        setSuccess(
          'Plantilla creada correctamente.',
        )
      }

      await load()

      createNew()
    }
    catch (
      exception
    ) {
      setError(
        getError(
          exception,
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  async function toggleActive(
    item:
      Template,
  ) {
    if (
      !canManage
      ||
      saving
    ) {
      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    try {
      await apiClient
        .put(
          `/my/helpdesk/templates/${item.id}`,
          {
            title:
              item.title,

            description:
              item.description,

            category:
              item.category,

            ticketType:
              item.ticketType,

            questions:
              item.questions,

            isActive:
              !item.isActive,

            revision:
              item.revision,
          },
        )

      setSuccess(
        item.isActive
          ? 'Plantilla desactivada.'
          : 'Plantilla activada.',
      )

      await load()
    }
    catch (
      exception
    ) {
      setError(
        getError(
          exception,
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  return (
    <main
      className="titan-page helpdesk-page hd-template-admin"
    >
      <header
        className="helpdesk-inbox__header"
      >
        <div>
          <span
            className="helpdesk-inbox__eyebrow"
          >
            <ClipboardList
              size={15}
            />

            CATÁLOGO DE SOLICITUDES
          </span>

          <h1>
            Plantillas de tickets
          </h1>

          <p>
            Crea solicitudes guiadas
            para que los colaboradores
            reporten incidencias con la
            información necesaria desde
            el primer contacto.
          </p>
        </div>

        <div
          className="helpdesk-inbox__header-actions"
        >
          <button
            type="button"
            className="helpdesk-ui-button helpdesk-ui-button--secondary"
            disabled={
              loading
              ||
              saving
            }
            onClick={
              () =>
                void load()
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar
          </button>

          {canManage && (
            <button
              type="button"
              className="helpdesk-ui-button helpdesk-ui-button--primary"
              onClick={
                createNew
              }
            >
              <FilePlus2
                size={16}
              />

              Nueva plantilla
            </button>
          )}
        </div>
      </header>

      {error && (
        <div
          role="alert"
          className="helpdesk-inbox__error"
        >
          {error}
        </div>
      )}

      {success && (
        <div
          role="status"
          className="hd-template-admin__success"
        >
          <CheckCircle2
            size={16}
          />

          {success}
        </div>
      )}

      <section
        className="hd-template-admin__summary"
      >
        <article>
          <span>
            Total
          </span>

          <strong>
            {
              templates.length
            }
          </strong>
        </article>

        <article>
          <span>
            Activas
          </span>

          <strong>
            {
              templates.filter(
                x =>
                  x.isActive,
              ).length
            }
          </strong>
        </article>

        <article>
          <span>
            Inactivas
          </span>

          <strong>
            {
              templates.filter(
                x =>
                  !x.isActive,
              ).length
            }
          </strong>
        </article>

        <article>
          <span>
            Categorías
          </span>

          <strong>
            {
              categories.length
            }
          </strong>
        </article>
      </section>

      <div
        className="hd-template-admin__layout"
      >
        <section
          className="hd-template-admin__catalog"
        >
          <header>
            <div>
              <h2>
                Catálogo
              </h2>

              <p>
                Selecciona una plantilla
                para editarla.
              </p>
            </div>

            <label
              className="hd-template-admin__inactive"
            >
              <input
                type="checkbox"
                checked={
                  showInactive
                }
                onChange={
                  event =>
                    setShowInactive(
                      event.target
                        .checked,
                    )
                }
              />

              Mostrar inactivas
            </label>
          </header>

          {loading ? (
            <p>
              Cargando plantillas…
            </p>
          ) : !visibleTemplates
              .length ? (
            <div
              className="hd-template-admin__empty"
            >
              <ClipboardList
                size={32}
              />

              <strong>
                No hay plantillas
              </strong>

              <span>
                Crea la primera plantilla
                para el portal de soporte.
              </span>
            </div>
          ) : (
            <div
              className="hd-template-admin__list"
            >
              {visibleTemplates.map(
                item => (
                  <article
                    key={
                      item.id
                    }
                    className={
                      editor.id ===
                        item.id
                        ? 'is-selected'
                        : ''
                    }
                  >
                    <button
                      type="button"
                      className="hd-template-admin__select"
                      onClick={
                        () =>
                          edit(
                            item,
                          )
                      }
                    >
                      <div>
                        <span
                          className={
                            item.isActive
                              ? 'is-active'
                              : 'is-inactive'
                          }
                        >
                          {
                            item.isActive
                              ? 'Activa'
                              : 'Inactiva'
                          }
                        </span>

                        <strong>
                          {
                            item.title
                          }
                        </strong>

                        <small>
                          {
                            item.category
                          }
                          {' · '}
                          {
                            item.ticketType
                          }
                        </small>
                      </div>

                      <Edit3
                        size={16}
                      />
                    </button>

                    {canManage && (
                      <button
                        type="button"
                        className="hd-template-admin__toggle"
                        disabled={
                          saving
                        }
                        onClick={
                          () =>
                            void toggleActive(
                              item,
                            )
                        }
                      >
                        {
                          item.isActive
                            ? 'Desactivar'
                            : 'Activar'
                        }
                      </button>
                    )}
                  </article>
                ),
              )}
            </div>
          )}
        </section>

        <form
          className="hd-template-admin__editor"
          onSubmit={
            event =>
              void save(
                event,
              )
          }
        >
          <header>
            <div>
              <h2>
                {
                  editor.id
                    ? 'Editar plantilla'
                    : 'Nueva plantilla'
                }
              </h2>

              <p>
                Configura qué datos
                debe proporcionar el
                solicitante.
              </p>
            </div>

            {editor.id && (
              <button
                type="button"
                className="hd-template-admin__close"
                onClick={
                  createNew
                }
              >
                <X
                  size={17}
                />
              </button>
            )}
          </header>

          <div
            className="hd-template-admin__fields"
          >
            <label
              className="hd-template-admin__wide"
            >
              Nombre

              <input
                required
                maxLength={150}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  editor.title
                }
                onChange={
                  event =>
                    setEditor(
                      current => ({
                        ...current,

                        title:
                          event.target
                            .value,
                      }),
                    )
                }
                placeholder="Ej.: Problema con acceso al sistema"
              />
            </label>

            <label
              className="hd-template-admin__wide"
            >
              Descripción

              <textarea
                rows={3}
                maxLength={1000}
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  editor.description
                }
                onChange={
                  event =>
                    setEditor(
                      current => ({
                        ...current,

                        description:
                          event.target
                            .value,
                      }),
                    )
                }
                placeholder="Explica cuándo debe utilizarse esta plantilla."
              />
            </label>

            <label>
              Categoría

              <select
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  editor.category
                }
                onChange={
                  event =>
                    setEditor(
                      current => ({
                        ...current,

                        category:
                          event.target
                            .value,
                      }),
                    )
                }
              >
                {categories.map(
                  category => (
                    <option
                      key={
                        category
                      }
                      value={
                        category
                      }
                    >
                      {
                        category
                      }
                    </option>
                  ),
                )}
              </select>
            </label>

            <label>
              Tipo

              <select
                disabled={
                  !canManage
                  ||
                  saving
                }
                value={
                  editor.ticketType
                }
                onChange={
                  event =>
                    setEditor(
                      current => ({
                        ...current,

                        ticketType:
                          event.target
                            .value,
                      }),
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

            <label
              className="hd-template-admin__active"
            >
              <input
                type="checkbox"
                disabled={
                  !canManage
                  ||
                  saving
                }
                checked={
                  editor.isActive
                }
                onChange={
                  event =>
                    setEditor(
                      current => ({
                        ...current,

                        isActive:
                          event.target
                            .checked,
                      }),
                    )
                }
              />

              Disponible para usuarios
            </label>
          </div>

          <section
            className="hd-template-admin__questions"
          >
            <header>
              <div>
                <h3>
                  Preguntas
                </h3>

                <p>
                  Información adicional
                  que debe completar el
                  solicitante.
                </p>
              </div>

              <span>
                {
                  editor.questions
                    .length
                }
              </span>
            </header>

            <div
              className="hd-template-admin__question-add"
            >
              <input
                value={
                  question
                }
                disabled={
                  !canManage
                  ||
                  saving
                }
                onChange={
                  event =>
                    setQuestion(
                      event.target
                        .value,
                    )
                }
                onKeyDown={
                  event => {
                    if (
                      event.key ===
                      'Enter'
                    ) {
                      event
                        .preventDefault()

                      addQuestion()
                    }
                  }
                }
                placeholder="Ej.: ¿Cuál es el mensaje de error?"
              />

              <button
                type="button"
                disabled={
                  !canManage
                  ||
                  saving
                }
                onClick={
                  addQuestion
                }
              >
                <Plus
                  size={16}
                />

                Agregar
              </button>
            </div>

            {!editor.questions
                .length ? (
              <p
                className="hd-template-admin__no-questions"
              >
                Esta plantilla todavía
                no tiene preguntas
                adicionales.
              </p>
            ) : (
              <ol>
                {editor.questions.map(
                  (
                    item,
                    index,
                  ) => (
                    <li
                      key={
                        `${item}-${index}`
                      }
                    >
                      <span>
                        {
                          item
                        }
                      </span>

                      <button
                        type="button"
                        disabled={
                          !canManage
                          ||
                          saving
                        }
                        onClick={
                          () =>
                            removeQuestion(
                              index,
                            )
                        }
                      >
                        <Trash2
                          size={15}
                        />
                      </button>
                    </li>
                  ),
                )}
              </ol>
            )}
          </section>

          {canManage && (
            <footer>
              <button
                type="submit"
                className="helpdesk-ui-button helpdesk-ui-button--primary"
                disabled={
                  saving
                }
              >
                <Save
                  size={16}
                />

                {
                  saving
                    ? 'Guardando…'
                    : editor.id
                      ? 'Guardar cambios'
                      : 'Crear plantilla'
                }
              </button>
            </footer>
          )}
        </form>
      </div>
    </main>
  )
}