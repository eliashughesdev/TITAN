import { useEffect, useState } from "react";
import {
  ArrowDownToLine,
  ArrowUpFromLine,
  CheckCircle2,
  Clock3,
  Database,
  RefreshCw,
  ShieldCheck,
  Watch,
} from "lucide-react";

import { authFetch } from "../lib/api";
import { Btn, PageHeader, Panel } from "../ui/kit";

type Payload = {
  codigo: string;
  tipo: "entrada" | "salida";
  dispositivo: string;
  target: "sql";
};

type PreviewData = {
  codigo: string;
  nombre?: string;
  tipo: string;
  dispositivo: string;
  origen?: string;
  actor?: string;
  action?: string;
  version?: string;
};

type Result = {
  id?: number;
  codigo?: string;
  fecha?: string;
  hora?: string;
  message?: string;
  sql_confirmed?: boolean;
};

async function readResponse<T>(
  response: Response
): Promise<T> {
  const data = await response.json().catch(() => ({}));

  if (!response.ok) {
    throw new Error(
      typeof data.detail === "string"
        ? data.detail
        : `La solicitud falló: HTTP ${response.status}`
    );
  }

  return data as T;
}

export default function RemotePunch() {
  const [codigo, setCodigo] = useState("");
  const [tipo, setTipo] = useState<"entrada" | "salida">(
    "entrada"
  );
  const [dispositivo, setDispositivo] = useState(
    "Ponche Remoto"
  );
  const [devices, setDevices] = useState<string[]>([
    "Ponche Remoto",
  ]);

  const [loadingDevices, setLoadingDevices] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const [preview, setPreview] = useState<{
    payload: Payload;
    data: PreviewData;
  } | null>(null);

  const [result, setResult] = useState<Result | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    async function load() {
      try {
        const response = await authFetch(
          "/api/records/remote-devices",
          { signal: controller.signal }
        );

        const data = await readResponse<{
          items?: string[];
        }>(response);

        if (!controller.signal.aborted) {
          setDevices(
            data.items?.length
              ? data.items
              : ["Ponche Remoto"]
          );
        }
      } catch (cause) {
        if (!controller.signal.aborted) {
          setError(
            cause instanceof Error
              ? cause.message
              : "No se pudo cargar el inventario"
          );
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoadingDevices(false);
        }
      }
    }

    void load();

    return () => controller.abort();
  }, []);

  function clearState() {
    setPreview(null);
    setResult(null);
    setError("");
  }

  async function onPreview(
    event: React.FormEvent
  ) {
    event.preventDefault();

    if (busy) return;

    const code = codigo.trim();

    if (!/^[0-9]+$/.test(code)) {
      setError("El código debe contener solo números.");
      return;
    }

    const payload: Payload = {
      codigo: code,
      tipo,
      dispositivo,
      target: "sql",
    };

    setBusy(true);
    setError("");
    setResult(null);
    setPreview(null);

    try {
      const response = await authFetch(
        "/api/records/remote-punch",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            ...payload,
            dry_run: true,
            confirm: false,
          }),
        }
      );

      const data = await readResponse<PreviewData>(
        response
      );

      if (data.version !== "daily-row-v2") {
        throw new Error(
          "El backend Python sigue usando una versión anterior. " +
          "Reinícialo después de guardar records_remote.py."
        );
      }

      setPreview({ payload, data });

    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo previsualizar"
      );

    } finally {
      setBusy(false);
    }
  }

  async function onConfirm() {
    if (!preview || busy) return;

    const payload = preview.payload;

    setBusy(true);
    setError("");

    try {
      const response = await authFetch(
        "/api/records/remote-punch",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            ...payload,
            dry_run: false,
            confirm: true,
          }),
        }
      );

      const data = await readResponse<Result>(
        response
      );

      if (data.sql_confirmed !== true) {
        throw new Error(
          "El backend no confirmó el registro en SQL."
        );
      }

      setResult(data);
      setPreview(null);

    } catch (cause) {
      setPreview(null);

      setError(
        (cause instanceof Error
          ? cause.message
          : "No se pudo confirmar el registro") +
        " Comprueba el historial antes de repetirlo."
      );

    } finally {
      setBusy(false);
    }
  }

  const inputClass =
    "w-full rounded-xl border border-zinc-200 " +
    "bg-white/90 px-3 py-3 text-sm outline-none " +
    "transition focus:border-[#c8102e] " +
    "focus:ring-2 focus:ring-[#c8102e]/20 " +
    "disabled:opacity-60";

  return (
    <div className="page-enter space-y-5">
      <PageHeader
        kicker="Operación"
        title="Ponche remoto"
        subtitle="Registro manual de entrada o salida con verificación previa."
      />

      <div className="grid gap-4 sm:grid-cols-3">
        <div
          className={
            `kpi-tile ${
              tipo === "entrada"
                ? "kpi-emerald"
                : "kpi-sky"
            }`
          }
        >
          <span className="shine" />

          <div className="flex items-center justify-between">
            <p className="text-xs text-white/80">
              Tipo de registro
            </p>
            <Clock3 size={22} />
          </div>

          <p className="mt-3 text-3xl font-black capitalize">
            {tipo}
          </p>
        </div>

        <div className="kpi-tile kpi-slate">
          <span className="shine" />

          <div className="flex items-center justify-between">
            <p className="text-xs text-white/80">
              Referencia
            </p>
            <Watch size={22} />
          </div>

          <p className="mt-3 truncate text-lg font-black">
            {dispositivo}
          </p>
        </div>

        <div className="kpi-tile kpi-sky">
          <span className="shine" />

          <div className="flex items-center justify-between">
            <p className="text-xs text-white/80">
              Destino del registro
            </p>
            <Database size={22} />
          </div>

          <p className="mt-3 text-2xl font-black">
            SQL Server
          </p>

          <p className="mt-1 text-xs text-white/80">
            Confirmación después de guardar
          </p>
        </div>
      </div>

      <div className="grid items-start gap-5 lg:grid-cols-2">
        <Panel className="space-y-4 !p-6">
          <div className="flex items-center gap-3">
            <span className="grid h-11 w-11 place-items-center rounded-xl bg-[#c8102e]/10 text-[#c8102e]">
              <Clock3 size={23} />
            </span>

            <div>
              <h3 className="text-lg font-bold">
                Registrar asistencia
              </h3>
              <p className="text-sm text-zinc-500">
                Selecciona el colaborador y la operación.
              </p>
            </div>
          </div>

          <form
            onSubmit={onPreview}
            className="space-y-4"
          >
            <label className="block text-sm font-semibold">
              Código del colaborador

              <input
                required
                maxLength={24}
                inputMode="numeric"
                value={codigo}
                disabled={busy}
                onChange={(event) => {
                  clearState();
                  setCodigo(event.target.value);
                }}
                placeholder="Ejemplo: 62627"
                className={`${inputClass} mt-2`}
              />
            </label>

            <label className="block text-sm font-semibold">
              Referencia del reloj

              <select
                value={dispositivo}
                disabled={busy || loadingDevices}
                onChange={(event) => {
                  clearState();
                  setDispositivo(event.target.value);
                }}
                className={`${inputClass} mt-2`}
              >
                {devices.map((name) => (
                  <option key={name} value={name}>
                    {name}
                  </option>
                ))}
              </select>
            </label>

            <fieldset disabled={busy}>
              <legend className="mb-2 text-sm font-semibold">
                Tipo de registro
              </legend>

              <div className="grid grid-cols-2 gap-3">
                {([
                  {
                    value: "entrada",
                    label: "Entrada",
                    Icon: ArrowDownToLine,
                    activeClass:
                      "border-emerald-600 bg-emerald-600 text-white",
                  },
                  {
                    value: "salida",
                    label: "Salida",
                    Icon: ArrowUpFromLine,
                    activeClass:
                      "border-sky-600 bg-sky-600 text-white",
                  },
                ] as const).map((item) => (
                  <button
                    key={item.value}
                    type="button"
                    aria-pressed={tipo === item.value}
                    onClick={() => {
                      clearState();
                      setTipo(item.value);
                    }}
                    className={
                      "btn-modern flex items-center justify-center " +
                      "gap-2 rounded-xl border px-4 py-3 " +
                      "text-sm font-semibold transition " +
                      (tipo === item.value
                        ? item.activeClass
                        : "border-zinc-200 bg-white text-zinc-600")
                    }
                  >
                    <item.Icon size={18} />
                    {item.label}
                  </button>
                ))}
              </div>
            </fieldset>

            {error && (
              <p
                role="alert"
                className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800"
              >
                {error}
              </p>
            )}

            <div className="flex flex-wrap gap-3 pt-2">
              <Btn
                type="submit"
                tone="primary"
                disabled={
                  busy ||
                  loadingDevices ||
                  !codigo.trim()
                }
              >
                <ShieldCheck size={17} />
                {busy ? "Procesando…" : "Previsualizar"}
              </Btn>

              <Btn
                tone="ghost"
                disabled={busy}
                onClick={() => {
                  clearState();
                  setCodigo("");
                }}
              >
                <RefreshCw size={16} />
                Limpiar
              </Btn>
            </div>
          </form>
        </Panel>

        <div className="space-y-4">
          <Panel className="!p-6">
            <div className="flex items-center gap-2 font-semibold">
              <ShieldCheck
                size={19}
                className="text-[#c8102e]"
              />
              Alcance del registro
            </div>

            <p className="mt-3 text-sm leading-6 text-zinc-600">
              La operación completa la entrada o salida
              vacía de la fila diaria. Si ese campo ya
              tiene un valor, se conserva y la solicitud
              se rechaza.
            </p>

            <p className="mt-3 text-sm leading-6 text-zinc-600">
              El registro se guarda en SQL. La escritura
              de asistencia en la memoria del reloj sigue
              pendiente de compatibilidad del SDK oficial.
            </p>
          </Panel>

          {preview && (
            <Panel className="!border-[#c8102e]/30 !p-6">
              <h3 className="text-lg font-bold">
                Confirmar registro
              </h3>

              <dl className="mt-4 grid grid-cols-2 gap-3 text-sm">
                <dt className="text-zinc-500">Código</dt>
                <dd className="font-semibold">
                  {preview.data.codigo}
                </dd>

                <dt className="text-zinc-500">Colaborador</dt>
                <dd>{preview.data.nombre || "Sin nombre"}</dd>

                <dt className="text-zinc-500">Tipo</dt>
                <dd className="capitalize">
                  {preview.data.tipo}
                </dd>

                <dt className="text-zinc-500">Referencia</dt>
                <dd>{preview.data.dispositivo}</dd>

                <dt className="text-zinc-500">Operador</dt>
                <dd>{preview.data.actor || "Sesión actual"}</dd>

                <dt className="text-zinc-500">Acción</dt>
                <dd>
                  {preview.data.action === "update"
                    ? "Completar fila existente"
                    : "Crear fila del día"}
                </dd>
              </dl>

              <p className="mt-4 text-xs text-zinc-500">
                La fecha y hora se tomarán del servidor
                cuando confirmes.
              </p>

              <div className="mt-5 flex flex-wrap gap-3">
                <Btn
                  tone="primary"
                  disabled={busy}
                  onClick={() => void onConfirm()}
                >
                  <Database size={17} />
                  Confirmar en SQL
                </Btn>

                <Btn
                  tone="ghost"
                  disabled={busy}
                  onClick={() => setPreview(null)}
                >
                  Cancelar
                </Btn>
              </div>
            </Panel>
          )}

          {result && (
            <div
              role="status"
              className="rounded-2xl border border-emerald-200 bg-emerald-50 p-5 text-sm text-emerald-900"
            >
              <div className="flex items-center gap-2 font-bold">
                <CheckCircle2 size={21} />
                Registro confirmado en SQL
              </div>

              <p className="mt-3">
                Código: {result.codigo} · Registro:{" "}
                {result.id}
              </p>

              <p className="mt-1">
                {result.fecha} {result.hora}
              </p>

              <p className="mt-3">
                {result.message}
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}