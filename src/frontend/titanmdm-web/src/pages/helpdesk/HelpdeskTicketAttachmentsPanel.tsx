import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'

import {
  AlertTriangle,
  Download,
  File,
  FileText,
  Image as ImageIcon,
  Mail,
  Maximize2,
  Paperclip,
  RefreshCw,
  ShieldCheck,
  X,
} from 'lucide-react'

import {
  getAttachmentErrorMessage,
  helpdeskAttachmentsApi,
  type HelpdeskAttachment,
} from '../../api/helpdeskAttachmentsApi'

import './HelpdeskTicketAttachmentsPanel.css'

interface Props {
  ticketId: string
}

interface PreviewResource {
  item: HelpdeskAttachment
  url: string
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

function isImage(
  item: HelpdeskAttachment,
) {
  return item.contentType
    .toLowerCase()
    .startsWith(
      'image/',
    )
}

function isPdf(
  item: HelpdeskAttachment,
) {
  return item.contentType
    .toLowerCase() ===
    'application/pdf'
}

function FileIcon(
  {
    item,
  }: {
    item: HelpdeskAttachment
  },
) {
  if (
    isImage(
      item,
    )
  ) {
    return (
      <ImageIcon
        size={18}
      />
    )
  }

  if (
    isPdf(
      item,
    )
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
    urls,
    setUrls,
  ] =
    useState<
      Record<
        string,
        string
      >
    >(
      {},
    )

  const urlsRef =
    useRef<
      Record<
        string,
        string
      >
    >(
      {},
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
    useState<
      string |
      null
    >(
      null,
    )

  const [
    enlarged,
    setEnlarged,
  ] =
    useState<
      PreviewResource |
      null
    >(
      null,
    )

  const revokeUrls =
    useCallback(
      () => {
        Object
          .values(
            urlsRef.current,
          )
          .forEach(
            url =>
              URL.revokeObjectURL(
                url,
              ),
          )

        urlsRef.current =
          {}

        setUrls(
          {},
        )
      },
      [],
    )

  const load =
    useCallback(
      async () => {
        setLoading(
          true,
        )

        setError(
          null,
        )

        revokeUrls()

        try {
          const result =
            await helpdeskAttachmentsApi
              .list(
                ticketId,
              )

          setItems(
            result,
          )

          /*
           * Cargamos automáticamente todas las imágenes.
           *
           * Ya no hace falta pulsar "Ver".
           */
          const imageItems =
            result.filter(
              isImage,
            )

          const loaded =
            await Promise.all(
              imageItems.map(
                async item => {
                  try {
                    const blob =
                      await helpdeskAttachmentsApi
                        .preview(
                          ticketId,
                          item.id,
                        )

                    return {
                      id:
                        item.id,

                      url:
                        URL.createObjectURL(
                          blob,
                        ),
                    }
                  }
                  catch {
                    return null
                  }
                },
              ),
            )

          const nextUrls:
            Record<
              string,
              string
            > =
            {}

          for (
            const resource
            of loaded
          ) {
            if (
              resource
            ) {
              nextUrls[
                resource.id
              ] =
                resource.url
            }
          }

          urlsRef.current =
            nextUrls

          setUrls(
            nextUrls,
          )
        }
        catch (
          loadError
        ) {
          setItems(
            [],
          )

          setError(
            getAttachmentErrorMessage(
              loadError,
            ),
          )
        }
        finally {
          setLoading(
            false,
          )
        }
      },
      [
        revokeUrls,
        ticketId,
      ],
    )

  useEffect(
    () => {
      void load()

      return () => {
        revokeUrls()
      }
    },
    [
      load,
      revokeUrls,
    ],
  )

  const inline =
    useMemo(
      () =>
        items.filter(
          item =>
            item.isInline,
        ),
      [
        items,
      ],
    )

  const normal =
    useMemo(
      () =>
        items.filter(
          item =>
            !item.isInline,
        ),
      [
        items,
      ],
    )

  async function download(
    item:
      HelpdeskAttachment,
  ) {
    try {
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
    catch (
      downloadError
    ) {
      setError(
        getAttachmentErrorMessage(
          downloadError,
        ),
      )
    }
  }

  async function openPdf(
    item:
      HelpdeskAttachment,
  ) {
    try {
      const blob =
        await helpdeskAttachmentsApi
          .preview(
            ticketId,
            item.id,
          )

      const url =
        URL.createObjectURL(
          blob,
        )

      window.open(
        url,
        '_blank',
        'noopener,noreferrer',
      )

      window.setTimeout(
        () =>
          URL.revokeObjectURL(
            url,
          ),
        60_000,
      )
    }
    catch (
      previewError
    ) {
      setError(
        getAttachmentErrorMessage(
          previewError,
        ),
      )
    }
  }

  function renderImage(
    item:
      HelpdeskAttachment,
    signature:
      boolean,
  ) {
    const url =
      urls[
        item.id
      ]

    return (
      <article
        key={
          item.id
        }
        className={
          signature
            ? 'helpdesk-mail-image helpdesk-mail-image--signature'
            : 'helpdesk-mail-image'
        }
      >
        <div
          className="helpdesk-mail-image__top"
        >
          <div>
            <FileIcon
              item={
                item
              }
            />

            <span>
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
            </span>
          </div>

          <button
            type="button"
            title="Descargar"
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
          </button>
        </div>

        {
          url
            ? (
              <button
                type="button"
                className="helpdesk-mail-image__preview"
                title="Ampliar imagen"
                onClick={
                  () =>
                    setEnlarged(
                      {
                        item,
                        url,
                      },
                    )
                }
              >
                <img
                  src={
                    url
                  }
                  alt={
                    signature
                      ? 'Firma visual del remitente'
                      : item.fileName
                  }
                />

                <span>
                  <Maximize2
                    size={16}
                  />

                  Ampliar
                </span>
              </button>
            )
            : (
              <div
                className="helpdesk-mail-image__missing"
              >
                <AlertTriangle
                  size={18}
                />

                No fue posible mostrar esta imagen.
              </div>
            )
        }
      </article>
    )
  }

  return (
    <>
      {/* ====================================================== */}
      {/* FIRMA / INLINE                                         */}
      {/* ====================================================== */}

      {
        inline.length >
        0
        &&
        (
          <section
            className="helpdesk-detail__card helpdesk-mail-assets"
          >
            <header
              className="helpdesk-mail-assets__header"
            >
              <div>
                <Mail
                  size={18}
                />

                <div>
                  <h2>
                    Firma y contenido visual del correo
                  </h2>

                  <p>
                    Imágenes incrustadas por el remitente.
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
              className="helpdesk-signature-images"
            >
              {
                inline.map(
                  item =>
                    isImage(
                      item,
                    )
                      ? renderImage(
                          item,
                          true,
                        )
                      : (
                        <article
                          key={
                            item.id
                          }
                          className="helpdesk-file-card"
                        >
                          <FileIcon
                            item={
                              item
                            }
                          />

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
                          </div>

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
                        </article>
                      ),
                )
              }
            </div>

            <div
              className="helpdesk-signature-security"
            >
              <ShieldCheck
                size={15}
              />

              <span>
                La firma se conserva como evidencia visual
                complementaria. La identidad principal sigue
                validándose mediante el remitente y el usuario
                sincronizado en TitanMDM.
              </span>
            </div>
          </section>
        )
      }

      {/* ====================================================== */}
      {/* ADJUNTOS                                               */}
      {/* ====================================================== */}

      <section
        className="helpdesk-detail__card helpdesk-mail-assets"
      >
        <header
          className="helpdesk-mail-assets__header"
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
                Evidencias y archivos recibidos con el ticket.
              </p>
            </div>
          </div>

          <button
            type="button"
            className="helpdesk-mail-assets__refresh"
            disabled={
              loading
            }
            title="Actualizar adjuntos"
            onClick={
              () =>
                void load()
            }
          >
            <RefreshCw
              size={15}
            />
          </button>
        </header>

        {
          error
          &&
          (
            <div
              className="helpdesk-mail-assets__error"
            >
              <AlertTriangle
                size={17}
              />

              {
                error
              }
            </div>
          )
        }

        {
          loading
            ? (
              <div
                className="helpdesk-mail-assets__empty"
              >
                Cargando archivos…
              </div>
            )
            : normal.length ===
              0
              ? (
                <div
                  className="helpdesk-mail-assets__empty"
                >
                  Este ticket no contiene adjuntos convencionales.
                </div>
              )
              : (
                <div
                  className="helpdesk-ticket-files"
                >
                  {
                    normal.map(
                      item => {
                        if (
                          isImage(
                            item,
                          )
                        ) {
                          return renderImage(
                            item,
                            false,
                          )
                        }

                        return (
                          <article
                            key={
                              item.id
                            }
                            className="helpdesk-file-card"
                          >
                            <span
                              className="helpdesk-file-card__icon"
                            >
                              <FileIcon
                                item={
                                  item
                                }
                              />
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
                                {' · '}
                                {
                                  item.contentType
                                }
                              </small>
                            </div>

                            <div
                              className="helpdesk-file-card__actions"
                            >
                              {
                                isPdf(
                                  item,
                                )
                                &&
                                (
                                  <button
                                    type="button"
                                    onClick={
                                      () =>
                                        void openPdf(
                                          item,
                                        )
                                    }
                                  >
                                    Ver PDF
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
                        )
                      },
                    )
                  }
                </div>
              )
        }
      </section>

      {/* ====================================================== */}
      {/* IMAGE VIEWER                                           */}
      {/* ====================================================== */}

      {
        enlarged
        &&
        (
          <div
            className="helpdesk-image-viewer"
            role="presentation"
            onClick={
              () =>
                setEnlarged(
                  null,
                )
            }
          >
            <div
              className="helpdesk-image-viewer__dialog"
              role="dialog"
              aria-modal="true"
              onClick={
                event =>
                  event.stopPropagation()
              }
            >
              <header>
                <div>
                  <strong>
                    {
                      enlarged.item.fileName
                    }
                  </strong>

                  <span>
                    {
                      formatBytes(
                        enlarged.item.sizeBytes,
                      )
                    }
                  </span>
                </div>

                <button
                  type="button"
                  aria-label="Cerrar"
                  onClick={
                    () =>
                      setEnlarged(
                        null,
                      )
                  }
                >
                  <X
                    size={20}
                  />
                </button>
              </header>

              <div
                className="helpdesk-image-viewer__content"
              >
                <img
                  src={
                    enlarged.url
                  }
                  alt={
                    enlarged.item.fileName
                  }
                />
              </div>
            </div>
          </div>
        )
      }
    </>
  )
}