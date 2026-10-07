import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  Download,
  Eye,
  File,
  FileText,
  Image,
  Mail,
  Paperclip,
  RefreshCw,
  ShieldCheck,
  X,
} from 'lucide-react'

import {
  helpdeskAttachmentsApi,
  type HelpdeskAttachment,
} from '../../api/helpdeskAttachmentsApi'

import './HelpdeskTicketAttachmentsPanel.css'

interface Props {
  ticketId: string
}

function formatBytes(
  value: number,
) {
  if (
    value <
    1024
  ) {
    return `${value} B`
  }

  if (
    value <
    1024 *
    1024
  ) {
    return `${Math.round(
      value /
      1024,
    )} KB`
  }

  return `${(
    value /
    1024 /
    1024
  ).toFixed(
    1,
  )} MB`
}

function iconFor(
  contentType: string,
) {
  if (
    contentType.startsWith(
      'image/',
    )
  ) {
    return (
      <Image
        size={18}
      />
    )
  }

  if (
    contentType ===
    'application/pdf'
  ) {
    return (
      <FileText
        size={18}
      />
    )
  }

  return (
    <File
      size={18}
    />
  )
}

export function HelpdeskTicketAttachmentsPanel(
  {
    ticketId,
  }: Props,
) {
  const [
    items,
    setItems,
  ] =
    useState<
      HelpdeskAttachment[]
    >(
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
    previewUrl,
    setPreviewUrl,
  ] =
    useState<
      string |
      null
    >(
      null,
    )

  const [
    previewItem,
    setPreviewItem,
  ] =
    useState<
      HelpdeskAttachment |
      null
    >(
      null,
    )

  const load =
    useCallback(
      async () => {
        setLoading(
          true,
        )

        try {
          const result =
            await helpdeskAttachmentsApi
              .list(
                ticketId,
              )

          setItems(
            result,
          )
        }
        finally {
          setLoading(
            false,
          )
        }
      },
      [
        ticketId,
      ],
    )

  useEffect(
    () => {
      void load()
    },
    [
      load,
    ],
  )

  useEffect(
    () => {
      return () => {
        if (
          previewUrl
        ) {
          URL.revokeObjectURL(
            previewUrl,
          )
        }
      }
    },
    [
      previewUrl,
    ],
  )

  const inline =
    useMemo(
      () =>
        items.filter(
          x =>
            x.isInline,
        ),
      [
        items,
      ],
    )

  const normal =
    useMemo(
      () =>
        items.filter(
          x =>
            !x.isInline,
        ),
      [
        items,
      ],
    )

  async function openPreview(
    item:
      HelpdeskAttachment,
  ) {
    const blob =
      await helpdeskAttachmentsApi
        .preview(
          ticketId,
          item.id,
        )

    if (
      previewUrl
    ) {
      URL.revokeObjectURL(
        previewUrl,
      )
    }

    const url =
      URL.createObjectURL(
        blob,
      )

    setPreviewUrl(
      url,
    )

    setPreviewItem(
      item,
    )
  }

  async function download(
    item:
      HelpdeskAttachment,
  ) {
    const blob =
      await helpdeskAttachmentsApi
        .download(
          ticketId,
          item.id,
        )

    const url =
      URL.createObjectURL(
        blob,
      )

    const anchor =
      document.createElement(
        'a',
      )

    anchor.href =
      url

    anchor.download =
      item.fileName

    document.body
      .appendChild(
        anchor,
      )

    anchor.click()

    anchor.remove()

    URL.revokeObjectURL(
      url,
    )
  }

  function closePreview() {
    if (
      previewUrl
    ) {
      URL.revokeObjectURL(
        previewUrl,
      )
    }

    setPreviewUrl(
      null,
    )

    setPreviewItem(
      null,
    )
  }

  return (
    <>
      {/* ====================================================== */}
      {/* INLINE / SIGNATURE                                     */}
      {/* ====================================================== */}

      {
        inline.length >
        0
        &&
        (
          <section
            className="helpdesk-detail__card helpdesk-attachments"
          >
            <header
              className="helpdesk-attachments__header"
            >
              <div>
                <Mail
                  size={18}
                />

                <div>
                  <h2>
                    Firma y contenido del correo
                  </h2>

                  <p>
                    Elementos visuales incrustados por el remitente.
                  </p>
                </div>
              </div>

              <span>
                {
                  inline.length
                }
              </span>
            </header>

            <div
              className="helpdesk-signature-grid"
            >
              {
                inline.map(
                  item => (
                    <button
                      key={
                        item.id
                      }
                      type="button"
                      className="helpdesk-signature-card"
                      onClick={
                        () =>
                          void openPreview(
                            item,
                          )
                      }
                    >
                      <span
                        className="helpdesk-signature-card__icon"
                      >
                        {
                          iconFor(
                            item.contentType,
                          )
                        }
                      </span>

                      <div>
                        <strong>
                          {
                            item.fileName
                          }
                        </strong>

                        <small>
                          {
                            formatBytes(
                              item.sizeBytes,
                            )
                          }
                        </small>

                        {
                          item.contentId
                          &&
                          (
                            <small
                              title={
                                item.contentId
                              }
                            >
                              CID identificado
                            </small>
                          )
                        }
                      </div>

                      <Eye
                        size={16}
                      />
                    </button>
                  ),
                )
              }
            </div>

            <div
              className="helpdesk-signature-note"
            >
              <ShieldCheck
                size={15}
              />

              La firma es evidencia visual complementaria.
              La identidad principal continúa siendo el
              correo y el usuario sincronizado.
            </div>
          </section>
        )
      }

      {/* ====================================================== */}
      {/* NORMAL ATTACHMENTS                                     */}
      {/* ====================================================== */}

      <section
        className="helpdesk-detail__card helpdesk-attachments"
      >
        <header
          className="helpdesk-attachments__header"
        >
          <div>
            <Paperclip
              size={18}
            />

            <div>
              <h2>
                Adjuntos
              </h2>

              <p>
                Archivos relacionados con el caso.
              </p>
            </div>
          </div>

          <button
            type="button"
            className="helpdesk-attachments__refresh"
            disabled={
              loading
            }
            onClick={
              () =>
                void load()
            }
          >
            <RefreshCw
              size={14}
            />
          </button>
        </header>

        {
          loading
          &&
          normal.length ===
          0
            ? (
              <div
                className="helpdesk-attachments__empty"
              >
                Cargando adjuntos…
              </div>
            )
            : normal.length ===
              0
              ? (
                <div
                  className="helpdesk-attachments__empty"
                >
                  No hay archivos adjuntos convencionales.
                </div>
              )
              : (
                <div
                  className="helpdesk-attachments__list"
                >
                  {
                    normal.map(
                      item => (
                        <article
                          key={
                            item.id
                          }
                          className="helpdesk-attachment-row"
                        >
                          <span
                            className="helpdesk-attachment-row__icon"
                          >
                            {
                              iconFor(
                                item.contentType,
                              )
                            }
                          </span>

                          <div
                            className="helpdesk-attachment-row__info"
                          >
                            <strong>
                              {
                                item.fileName
                              }
                            </strong>

                            <small>
                              {
                                formatBytes(
                                  item.sizeBytes,
                                )
                              }
                              {' · '}
                              {
                                item.contentType
                              }
                            </small>
                          </div>

                          <div
                            className="helpdesk-attachment-row__actions"
                          >
                            {
                              item.canPreview
                              &&
                              (
                                <button
                                  type="button"
                                  onClick={
                                    () =>
                                      void openPreview(
                                        item,
                                      )
                                  }
                                >
                                  <Eye
                                    size={15}
                                  />

                                  Ver
                                </button>
                              )
                            }

                            <button
                              type="button"
                              onClick={
                                () =>
                                  void download(
                                    item,
                                  )
                              }
                            >
                              <Download
                                size={15}
                              />

                              Descargar
                            </button>
                          </div>
                        </article>
                      ),
                    )
                  }
                </div>
              )
        }
      </section>

      {/* ====================================================== */}
      {/* PREVIEW MODAL                                          */}
      {/* ====================================================== */}

      {
        previewUrl
        &&
        previewItem
        &&
        (
          <div
            className="helpdesk-preview"
          >
            <div
              className="helpdesk-preview__dialog"
            >
              <header>
                <div>
                  <strong>
                    {
                      previewItem.fileName
                    }
                  </strong>

                  <span>
                    {
                      formatBytes(
                        previewItem.sizeBytes,
                      )
                    }
                  </span>
                </div>

                <button
                  type="button"
                  aria-label="Cerrar"
                  onClick={
                    closePreview
                  }
                >
                  <X
                    size={20}
                  />
                </button>
              </header>

              <div
                className="helpdesk-preview__body"
              >
                {
                  previewItem.contentType
                    .startsWith(
                      'image/',
                    )
                    ? (
                      <img
                        src={
                          previewUrl
                        }
                        alt={
                          previewItem.fileName
                        }
                      />
                    )
                    : (
                      <iframe
                        src={
                          previewUrl
                        }
                        title={
                          previewItem.fileName
                        }
                      />
                    )
                }
              </div>
            </div>
          </div>
        )
      }
    </>
  )
}