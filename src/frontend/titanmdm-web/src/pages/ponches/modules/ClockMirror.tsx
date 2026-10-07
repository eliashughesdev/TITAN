import { useCallback, useEffect, useState } from "react";
import { authFetch } from "../lib/api";
import { Btn, PageHeader, Panel } from "../ui/kit";

type Job = {
  id: string;
  device: string;
  codigo: string;
  nombre: string;
  status: string;
  attempts: number;
  detail: string;
  fields?: string[];
  created_at?: string;
  updated_at?: string;
};

type DeviceItem = {
  name: string;
};

type JobsResponse = {
  items?: Job[];
};

type DevicesResponse = {
  items?: DeviceItem[];
};

type MutationResponse = {
  message?: string;
  accepted?: boolean;
  confirmed?: boolean;
};

const BASE = "/api/records/collab-reconcile/mirror";

const STATUS_LABELS: Record<string, string> = {
  prepared: "Guardado, pendiente de publicar",
  pending: "Pendiente",
  running: "Enviando",
  retry: "Reintento automático pendiente",
  confirmed: "Confirmado en reloj",
  blocked: "Requiere revisión",
  failed: "Falló tras los reintentos",
  superseded: "Sustituido por otro cambio",
};

const FIELD_LABELS: Record<string, string> = {
  nombre: "Nombre",
  nombre_reloj: "Nombre",
  password: "PIN",
  password_device: "PIN",
  card: "Tarjeta",
  card_no: "Tarjeta",
  rfid: "Tarjeta",
  privilege: "Privilegio",
  group_id: "Grupo",
};

const ACTIVE_STATUSES = new Set([
  "prepared",
  "pending",
  "running",
  "retry",
]);

const RETRY_STATUSES = new Set([
  "retry",
  "blocked",
  "failed",
]);

async function readResponse<T>(
  response: Response
): Promise<T> {
  const data = await response.json().catch(() => ({}));

  if (!response.ok) {
    const detail = data?.detail;

    throw new Error(
      typeof detail === "string"
        ? detail
        : typeof data?.message === "string"
          ? data.message
          : `La solicitud falló: HTTP ${response.status}`
    );
  }

  return data as T;
}

function statusClass(status: string): string {
  if (status === "confirmed") {
    return "border-emerald-200 bg-emerald-50 text-emerald-800";
  }

  if (status === "blocked" || status === "failed") {
    return "border-rose-200 bg-rose-50 text-rose-800";
  }

  if (status === "superseded") {
    return "border-zinc-200 bg-zinc-100 text-zinc-600";
  }

  return "border-amber-200 bg-amber-50 text-amber-800";
}

function formatDate(value?: string): string {
  if (!value) return "—";

  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString("es-DO");
}

function visibleFields(fields?: string[]): string {
  const names = (fields?.length ? fields : ["nombre"])
    .filter((field) => field !== "codigo")
    .map((field) => FIELD_LABELS[field] || field);

  return [...new Set(names)].join(", ");
}

export default function ClockMirror() {
  const [devices, setDevices] = useState<string[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);

  const [codigo, setCodigo] = useState("");
  const [nombre, setNombre] = useState("");
  const [confirmedByUser, setConfirmedByUser] = useState(false);

  const [busy, setBusy] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [retryJob, setRetryJob] = useState<Job | null>(null);
  const [lastRefresh, setLastRefresh] = useState("");

  const loadJobs = useCallback(
    async (signal?: AbortSignal) => {
      const response = await authFetch(
        `${BASE}/jobs`,
        { signal }
      );

      const data = await readResponse<JobsResponse>(
        response
      );

      if (!signal?.aborted) {
        setJobs(
          Array.isArray(data.items)
            ? data.items
            : []
        );

        setLastRefresh(
          new Date().toLocaleTimeString("es-DO")
        );
      }
    },
    []
  );

  useEffect(() => {
    const controller = new AbortController();
    let refreshing = false;

    async function initialize() {
      try {
        const response = await authFetch(
          `${BASE}/devices`,
          { signal: controller.signal }
        );

        const data = await readResponse<DevicesResponse>(
          response
        );

        if (!controller.signal.aborted) {
          setDevices(
            [...new Set(
              (data.items || [])
                .map((item) => item.name)
                .filter(Boolean)
            )]
          );
        }

        await loadJobs(controller.signal);

      } catch (cause) {
        if (!controller.signal.aborted) {
          setError(
            cause instanceof Error
              ? cause.message
              : "No se pudo cargar el seguimiento"
          );
        }

      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }

    void initialize();

    const timer = window.setInterval(() => {
      if (refreshing || controller.signal.aborted) {
        return;
      }

      refreshing = true;

      void loadJobs(controller.signal)
        .catch((cause) => {
          if (!controller.signal.aborted) {
            setError(
              cause instanceof Error
                ? cause.message
                : "No se pudo actualizar el historial"
            );
          }
        })
        .finally(() => {
          refreshing = false;
        });
    }, 10000);

    return () => {
      controller.abort();
      window.clearInterval(timer);
    };
  }, [loadJobs]);

  useEffect(() => {
    setConfirmedByUser(false);
  }, [codigo, nombre, selected]);

  useEffect(() => {
    if (!retryJob) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busy) {
        setRetryJob(null);
      }
    };

    window.addEventListener("keydown", onKeyDown);

    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [retryJob, busy]);

  async function refresh() {
    if (busy) return;

    setBusy("refresh");
    setError("");

    try {
      await loadJobs();

    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo actualizar el historial"
      );

    } finally {
      setBusy("");
    }
  }

  async function submitRename() {
    if (busy || !confirmedByUser) return;

    const code = codigo.trim();
    const clockName = nombre.trim();

    if (!/^[0-9]+$/.test(code)) {
      setError(
        "El código debe contener solo números. Conserva los ceros iniciales."
      );
      return;
    }

    if (
      !clockName ||
      new TextEncoder().encode(clockName).length > 24
    ) {
      setError(
        "Escribe un nombre de hasta 24 bytes UTF-8. Puedes usar una abreviatura."
      );
      return;
    }

    if (!selected.length || selected.length > 20) {
      setError(
        "Selecciona entre uno y veinte relojes."
      );
      return;
    }

    setBusy("rename");
    setError("");
    setMessage("");

    try {
      const response = await authFetch(
        `${BASE}/rename`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            codigo: code,
            nombre: clockName,
            devices: selected,
            confirm: true,
          }),
        }
      );

      const data = await readResponse<MutationResponse>(
        response
      );

      setMessage(
        data.message ||
        "Cambio en cola; pendiente de confirmación física."
      );

      setConfirmedByUser(false);

      await loadJobs();

    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo enviar el cambio"
      );

    } finally {
      setBusy("");
    }
  }

  async function retryOperation() {
    if (!retryJob || busy) return;

    setBusy(retryJob.id);
    setError("");
    setMessage("");

    try {
      const response = await authFetch(
        `${BASE}/jobs/${encodeURIComponent(retryJob.id)}/retry`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            confirm: true,
          }),
        }
      );

      const data = await readResponse<MutationResponse>(
        response
      );

      setMessage(
        data.message ||
        "Reintento en cola; pendiente de confirmación física."
      );

      setRetryJob(null);

      await loadJobs();

    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo reintentar la operación"
      );

    } finally {
      setBusy("");
    }
  }

  function toggleDevice(
    name: string,
    checked: boolean
  ) {
    setSelected((current) => {
      if (!checked) {
        return current.filter(
          (value) => value !== name
        );
      }

      if (
        current.includes(name) ||
        current.length >= 20
      ) {
        return current;
      }

      return [...current, name];
    });
  }

  const activeCount = jobs.filter(
    (job) => ACTIVE_STATUSES.has(job.status)
  ).length;

  const confirmedCount = jobs.filter(
    (job) => job.status === "confirmed"
  ).length;

  const problemCount = jobs.filter(
    (job) =>
      job.status === "blocked" ||
      job.status === "failed"
  ).length;

  const inputClass =
    "mt-2 w-full rounded-xl border border-zinc-200 " +
    "bg-white px-3 py-2.5 text-sm outline-none " +
    "focus:border-[#c8102e] focus:ring-2 " +
    "focus:ring-[#c8102e]/10 disabled:opacity-60";

  return (
    <div className="page-enter space-y-5">
      <PageHeader
        kicker="Ponches"
        title="Escritura y verificación de relojes"
        subtitle={
          "Seguimiento de los cambios enviados desde " +
          "Colaboradores y de su confirmación física."
        }
      />

      <div className="grid gap-3 sm:grid-cols-3">
        {[
          {
            title: "En seguimiento",
            value: activeCount,
            color: "text-amber-700",
          },
          {
            title: "Confirmadas",
            value: confirmedCount,
            color: "text-emerald-700",
          },
          {
            title: "Requieren revisión",
            value: problemCount,
            color: "text-rose-700",
          },
        ].map((item) => (
          <Panel key={item.title}>
            <p className="text-sm text-zinc-500">
              {item.title}
            </p>

            <p
              className={
                `mt-2 text-3xl font-bold ${item.color}`
              }
            >
              {item.value}
            </p>
          </Panel>
        ))}
      </div>

      {error && (
        <p
          role="alert"
          className={
            "rounded-xl border border-rose-200 " +
            "bg-rose-50 p-3 text-sm text-rose-800"
          }
        >
          {error}
        </p>
      )}

      {message && (
        <p
          role="status"
          className={
            "rounded-xl border border-blue-200 " +
            "bg-blue-50 p-3 text-sm text-blue-800"
          }
        >
          {message}
        </p>
      )}

      <Panel>
        <h3 className="text-lg font-semibold">
          Corrección manual de nombre
        </h3>

        <p className="mb-4 mt-2 text-sm text-zinc-600">
          Para modificar la ficha completa utiliza
          Colaboradores. Esta herramienta envía solamente
          el nombre y conserva las credenciales existentes.
        </p>

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm font-semibold">
            Código del colaborador

            <input
              className={inputClass}
              value={codigo}
              disabled={!!busy}
              maxLength={24}
              inputMode="numeric"
              onChange={(event) =>
                setCodigo(event.target.value)
              }
              placeholder="Conserva los ceros iniciales"
            />
          </label>

          <label className="text-sm font-semibold">
            Nombre para el reloj

            <input
              className={inputClass}
              value={nombre}
              disabled={!!busy}
              maxLength={80}
              onChange={(event) =>
                setNombre(event.target.value)
              }
              placeholder="Máximo 24 bytes UTF-8"
            />

            <span className="mt-1 block text-xs font-normal text-zinc-500">
              {new TextEncoder().encode(nombre.trim()).length}
              {" / 24 bytes"}
            </span>
          </label>
        </div>

        <fieldset
          disabled={!!busy || loading}
          className="mt-4"
        >
          <legend className="mb-2 text-sm font-semibold">
            Relojes de destino — {selected.length} de 20
          </legend>

          {devices.length === 0 ? (
            <p className="rounded-xl bg-zinc-50 p-3 text-sm text-zinc-500">
              {loading
                ? "Cargando relojes…"
                : "No hay relojes registrados disponibles."}
            </p>
          ) : (
            <div className="grid gap-2 sm:grid-cols-3">
              {devices.map((name) => (
                <label
                  key={name}
                  className={
                    "flex items-center gap-2 rounded-xl " +
                    "border border-zinc-200 p-3 text-sm"
                  }
                >
                  <input
                    type="checkbox"
                    checked={selected.includes(name)}
                    disabled={
                      selected.length >= 20 &&
                      !selected.includes(name)
                    }
                    onChange={(event) =>
                      toggleDevice(
                        name,
                        event.target.checked
                      )
                    }
                  />

                  <span>{name}</span>
                </label>
              ))}
            </div>
          )}
        </fieldset>

        <label className="my-4 flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={confirmedByUser}
            disabled={!!busy}
            onChange={(event) =>
              setConfirmedByUser(event.target.checked)
            }
          />

          Confirmo la escritura del nombre en los
          relojes seleccionados.
        </label>

        <Btn
          tone="primary"
          disabled={
            !!busy ||
            loading ||
            !confirmedByUser ||
            !codigo.trim() ||
            !nombre.trim() ||
            selected.length === 0
          }
          onClick={() => void submitRename()}
        >
          {busy === "rename"
            ? "Guardando operaciones…"
            : "Enviar cambio a los relojes"}
        </Btn>
      </Panel>

      <Panel>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold">
              Últimas 100 operaciones
            </h3>

            <p className="mt-1 text-xs text-zinc-500">
              Actualización automática cada 10 segundos.
              {lastRefresh && (
                <> Última lectura: {lastRefresh}.</>
              )}
            </p>
          </div>

          <Btn
            tone="ghost"
            disabled={!!busy}
            onClick={() => void refresh()}
          >
            {busy === "refresh"
              ? "Actualizando…"
              : "Actualizar"}
          </Btn>
        </div>

        <p className="mb-4 text-sm text-zinc-500">
          Los contadores corresponden a este historial.
          Solo “Confirmado en reloj” indica que la
          lectura posterior verificó la escritura.
        </p>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-zinc-200">
                {[
                  "Reloj",
                  "Código",
                  "Nombre",
                  "Campos",
                  "Estado",
                  "Intentos",
                  "Actualización",
                  "Detalle",
                  "Acción",
                ].map((title) => (
                  <th
                    key={title}
                    className="whitespace-nowrap p-2"
                  >
                    {title}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody>
              {jobs.map((job) => (
                <tr
                  key={job.id}
                  className="border-b border-zinc-100"
                >
                  <td className="p-2">
                    {job.device}
                  </td>

                  <td className="p-2 font-mono">
                    {job.codigo}
                  </td>

                  <td className="p-2">
                    {job.nombre || "—"}
                  </td>

                  <td className="p-2">
                    {visibleFields(job.fields)}
                  </td>

                  <td className="p-2">
                    <span
                      className={
                        "inline-block rounded-lg border " +
                        "px-2 py-1 text-xs font-semibold " +
                        statusClass(job.status)
                      }
                    >
                      {STATUS_LABELS[job.status] ||
                        job.status}
                    </span>
                  </td>

                  <td className="p-2">
                    {job.attempts}
                  </td>

                  <td className="whitespace-nowrap p-2 text-xs text-zinc-500">
                    {formatDate(
                      job.updated_at || job.created_at
                    )}
                  </td>

                  <td className="min-w-56 p-2">
                    {job.detail || "En cola"}
                  </td>

                  <td className="p-2">
                    {RETRY_STATUSES.has(job.status) && (
                      <Btn
                        tone="ghost"
                        disabled={!!busy}
                        onClick={() => {
                          setError("");
                          setMessage("");
                          setRetryJob(job);
                        }}
                      >
                        Reintentar
                      </Btn>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {jobs.length === 0 && (
            <p className="py-5 text-sm text-zinc-500">
              {loading
                ? "Cargando operaciones…"
                : "No hay operaciones registradas."}
            </p>
          )}
        </div>
      </Panel>

      {retryJob && (
        <div
          className={
            "fixed inset-0 z-50 flex items-center " +
            "justify-center bg-black/40 p-4"
          }
          role="dialog"
          aria-modal="true"
          aria-labelledby="clock-retry-title"
        >
          <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
            <h3
              id="clock-retry-title"
              className="text-lg font-bold"
            >
              Confirmar reintento
            </h3>

            <p className="my-4 text-sm text-zinc-600">
              Se reenviará la operación del código{" "}
              <strong>{retryJob.codigo}</strong> al reloj{" "}
              <strong>{retryJob.device}</strong>.
            </p>

            <p className="mb-4 rounded-xl bg-amber-50 p-3 text-sm text-amber-900">
              Corrige primero la causa indicada:
              {" "}
              {retryJob.detail || "Sin detalle disponible."}
            </p>

            <p className="mb-4 text-xs text-zinc-500">
              Si existe un cambio posterior para este
              colaborador y reloj, el backend rechazará
              el reintento antiguo.
            </p>

            <div className="flex justify-end gap-3">
              <Btn
                tone="ghost"
                disabled={!!busy}
                onClick={() => setRetryJob(null)}
              >
                Cancelar
              </Btn>

              <Btn
                tone="primary"
                disabled={!!busy}
                onClick={() => void retryOperation()}
              >
                {busy
                  ? "Enviando…"
                  : "Confirmar reintento"}
              </Btn>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}