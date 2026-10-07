import {
  useCallback,
  useEffect,
  useState,
  type FormEvent,
} from 'react'
import {
  Download,
  Paperclip,
  Upload,
} from 'lucide-react'
import apiClient from '../../api/apiClient'

interface Attachment {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  isInternal: boolean
  createdAtUtc: string
}

interface Props {
  ticketId: string
  staff?: boolean
  canUpload?: boolean
}

export function HelpdeskAttachmentPanel({
  ticketId,
  staff = false,
  canUpload = true,
}: Props) {
  const [items, setItems] =
    useState<Attachment[]>([])
  const [file, setFile] =
    useState<File | null>(null)
  const [internal, setInternal] =
    useState(false)
  const [loading, setLoading] =
    useState(true)
  const [saving, setSaving] =
    useState(false)
  const [error, setError] =
    useState('')

  const load = useCallback(async () => {
    try {
      setLoading(true)

      const response =
        await apiClient.get<
          Attachment[]
        >(
          `/helpdesk/tickets/${ticketId}/attachments`,
        )

      setItems(
        response.data,
      )
      setError('')
    } catch {
      setError(
        'No se pudieron cargar los adjuntos.',
      )
    } finally {
      setLoading(false)
    }
  }, [ticketId])

  useEffect(() => {
    void load()
  }, [load])

  async function upload(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (!file || saving)
      return

    const form =
      new FormData()

    form.append(
      'file',
      file,
    )

    form.append(
      'isInternal',
      String(
        staff &&
          internal,
      ),
    )

    setSaving(true)
    setError('')

    try {
      await apiClient.post(
        `/helpdesk/tickets/${ticketId}/attachments`,
        form,
      )

      setFile(null)
      setInternal(false)

      const input =
        document.getElementById(
          `attachment-${ticketId}`,
        ) as HTMLInputElement | null

      if (input)
        input.value = ''

      await load()
    } catch {
      setError(
        'No se pudo subir el archivo. Usa PDF, PNG o JPEG de hasta 10 MiB.',
      )
    } finally {
      setSaving(false)
    }
  }

  async function download(
    item: Attachment,
  ) {
    try {
      const response =
        await apiClient.get<Blob>(
          `/helpdesk/tickets/${ticketId}/attachments/${item.id}`,
          {
            responseType:
              'blob',
          },
        )

      const url =
        URL.createObjectURL(
          response.data,
        )

      const anchor =
        document.createElement(
          'a',
        )

      anchor.href = url
      anchor.download =
        item.fileName

      document.body.appendChild(
        anchor,
      )

      anchor.click()
      anchor.remove()

      window.setTimeout(
        () =>
          URL.revokeObjectURL(
            url,
          ),
        1000,
      )
    } catch {
      setError(
        'No se pudo descargar el adjunto.',
      )
    }
  }

  return (
    <section className="helpdesk-attachments">
      <div className="helpdesk-attachments__heading">
        <Paperclip
          size={19}
        />
        <div>
          <h2>
            Adjuntos
          </h2>
          <p>
            PDF, PNG o JPEG;
            máximo 10 MiB.
          </p>
        </div>
      </div>

      {error && (
        <p role="alert">
          {error}
        </p>
      )}

      {loading ? (
        <p>
          Cargando archivos…
        </p>
      ) : items.length ===
        0 ? (
        <p>
          Todavía no hay
          archivos adjuntos.
        </p>
      ) : (
        <ul>
          {items.map(
            (item) => (
              <li
                key={
                  item.id
                }
              >
                <span>
                  {
                    item.fileName
                  }
                  {staff &&
                    item.isInternal &&
                    ' · Interno'}
                  {' · '}
                  {(
                    item.sizeBytes /
                    1024
                  ).toFixed(
                    0,
                  )}{' '}
                  KiB
                </span>

                <button
                  type="button"
                  onClick={() =>
                    void download(
                      item,
                    )
                  }
                >
                  <Download
                    size={
                      16
                    }
                  />
                  Descargar
                </button>
              </li>
            ),
          )}
        </ul>
      )}

      {canUpload && (
        <form
          onSubmit={(
            event,
          ) =>
            void upload(
              event,
            )
          }
        >
          <input
            id={`attachment-${ticketId}`}
            type="file"
            accept=".pdf,.png,.jpg,.jpeg"
            onChange={(
              event,
            ) =>
              setFile(
                event.target
                  .files?.[0] ??
                  null,
              )
            }
          />

          {staff && (
            <label>
              <input
                type="checkbox"
                checked={
                  internal
                }
                onChange={(
                  event,
                ) =>
                  setInternal(
                    event.target.checked,
                  )
                }
              />
              Solo personal TIC
            </label>
          )}

          <button
            type="submit"
            disabled={
              !file ||
              saving
            }
          >
            <Upload
              size={
                16
              }
            />

            {saving
              ? 'Subiendo…'
              : 'Adjuntar archivo'}
          </button>
        </form>
      )}
    </section>
  )
}