import { useEffect, useState } from "react";
import { Download, RefreshCw } from "lucide-react";

import { authFetch } from "../lib/api";
import { Btn, PageHeader, Panel } from "../ui/kit";

type Clock = {
  dispositivo: string;
  total_registros: number;
  registrado: boolean;
};

type Catalog = {
  items: Clock[];
  sql_online: boolean;
  warning?: string | null;
};

type ExportFormat = "excel" | "pdf";

async function responseError(response: Response): Promise<string> {
  const fallback = `La solicitud falló: HTTP ${response.status}.`;

  try {
    const body = await response.json();

    if (typeof body.detail === "string") {
      return body.detail;
    }

    if (typeof body.message === "string") {
      return body.message;
    }

    return fallback;
  } catch {
    return fallback;
  }
}

export default function DataExport() {
  const [clocks, setClocks] = useState<Clock[]>([]);
  const [selectedClock, setSelectedClock] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [limit, setLimit] = useState(1000);

  const [loading, setLoading] = useState(true);
  const [sqlOnline, setSqlOnline] = useState(false);
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState<ExportFormat | null>(null);

  const [error, setError] = useState("");
  const [warning, setWarning] = useState("");
  const [success, setSuccess] = useState("");

  useEffect(() => {
    const controller = new AbortController();

    async function loadClocks() {
      setLoading(true);
      setError("");
      setWarning("");

      try {
        const response = await authFetch(
          "/api/records/export-devices",
          { signal: controller.signal },
        );

        if (!response.ok) {
          throw new Error(await responseError(response));
        }

        const catalog: Catalog = await response.json();

        if (!Array.isArray(catalog.items)) {
          throw new Error("El catálogo de relojes no tiene un formato válido.");
        }

        if (controller.signal.aborted) {
          return;
        }

        setClocks(catalog.items);
        setSqlOnline(catalog.sql_online === true);
        setWarning(catalog.warning || "");

        setSelectedClock((current) =>
          catalog.items.some((clock) => clock.dispositivo === current)
            ? current
            : "",
        );
      } catch (cause) {
        if (!controller.signal.aborted) {
          setSqlOnline(false);
          setError(
            cause instanceof Error
              ? cause.message
              : "No se pudieron cargar los relojes.",
          );
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }

    void loadClocks();

    return () => controller.abort();
  }, [reload]);

  async function download(format: ExportFormat) {
    if (busy || loading) {
      return;
    }

    setError("");
    setSuccess("");

    if (!sqlOnline) {
      setError("La exportación requiere conexión con SQL.");
      return;
    }

    if (dateFrom && dateTo && dateFrom > dateTo) {
      setError("La fecha inicial no puede ser posterior a la fecha final.");
      return;
    }

    if (format === "pdf" && limit > 3000) {
      setError(
        "Selecciona un máximo de 3,000 registros para PDF. " +
          "Para volúmenes mayores utiliza Excel.",
      );
      return;
    }

    setBusy(format);

    try {
      const parameters = new URLSearchParams({
        limit: String(limit),
      });

      if (selectedClock) {
        parameters.set("dispositivo", selectedClock);
      }

      if (dateFrom) {
        parameters.set("fecha_desde", dateFrom);
      }

      if (dateTo) {
        parameters.set("fecha_hasta", dateTo);
      }

      const response = await authFetch(
        `/api/records/export/${format}?${parameters.toString()}`,
        { method: "GET" },
      );

      if (!response.ok) {
        throw new Error(await responseError(response));
      }

      const blob = await response.blob();

      if (
        blob.size === 0 ||
        blob.type.includes("application/json") ||
        blob.type.includes("text/html")
      ) {
        throw new Error(
          "El servidor no devolvió un archivo de exportación válido.",
        );
      }

      const clockName = (selectedClock || "todos")
        .replace(/[^\p{L}\p{N}_-]+/gu, "_")
        .slice(0, 80);

      const extension = format === "excel" ? "xlsx" : "pdf";
      const today = new Date().toISOString().slice(0, 10);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");

      anchor.href = url;
      anchor.download = `ponches_${today}_${clockName}.${extension}`;

      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();

      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);

      setSuccess(
        `${format === "excel" ? "Excel" : "PDF"} generado. ` +
          "Revisa las descargas del navegador.",
      );
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo generar la exportación.",
      );
    } finally {
      setBusy(null);
    }
  }

  const fieldClass =
    "w-full rounded-xl border border-zinc-200 bg-white px-3 py-2.5 " +
    "text-sm text-zinc-900 outline-none focus:border-[#c8102e] " +
    "focus:ring-2 focus:ring-[#c8102e]/10 disabled:opacity-50";

  return (
    <div className="space-y-5">
      <PageHeader
        kicker="Ponches"
        title="Exportación de registros"
        subtitle="Descarga los ponches por reloj y rango de fechas."
        actions={
          <Btn
            tone="ghost"
            disabled={loading || busy !== null}
            onClick={() => setReload((current) => current + 1)}
          >
            <RefreshCw
              size={16}
              className={loading ? "animate-spin" : ""}
            />
            Actualizar relojes
          </Btn>
        }
      />

      <div className="grid gap-3 sm:grid-cols-3">
        <Panel>
          <p className="text-sm text-zinc-500">Relojes disponibles</p>
          <p className="mt-1 text-2xl font-bold text-[#c8102e]">
            {loading ? "…" : clocks.length}
          </p>
        </Panel>

        <Panel>
          <p className="text-sm text-zinc-500">Conexión SQL</p>
          <p className="mt-1 text-lg font-semibold">
            {loading
              ? "Consultando"
              : sqlOnline
                ? "Disponible"
                : "Desconectada"}
          </p>
        </Panel>

        <Panel>
          <p className="text-sm text-zinc-500">Formatos</p>
          <p className="mt-1 text-lg font-semibold">Excel y PDF</p>
        </Panel>
      </div>

      <Panel className="max-w-3xl">
        <div className="space-y-5">
          <div>
            <label
              htmlFor="export-clock"
              className="mb-2 block text-sm font-semibold"
            >
              Reloj
            </label>

            <select
              id="export-clock"
              className={fieldClass}
              value={selectedClock}
              disabled={loading || busy !== null}
              onChange={(event) => setSelectedClock(event.target.value)}
            >
              <option value="">Todos los relojes</option>

              {clocks.map((clock) => (
                <option
                  key={clock.dispositivo}
                  value={clock.dispositivo}
                >
                  {clock.dispositivo}
                  {" — "}
                  {clock.total_registros.toLocaleString()} registros
                </option>
              ))}
            </select>

            <p className="mt-2 text-xs text-zinc-500">
              Incluye los relojes del inventario y los nombres presentes
              en SQL. Un reloj sin ponches puede aparecer con cero registros.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label
                htmlFor="export-from"
                className="mb-2 block text-sm font-semibold"
              >
                Desde
              </label>

              <input
                id="export-from"
                type="date"
                className={fieldClass}
                value={dateFrom}
                disabled={busy !== null}
                onChange={(event) => setDateFrom(event.target.value)}
              />
            </div>

            <div>
              <label
                htmlFor="export-to"
                className="mb-2 block text-sm font-semibold"
              >
                Hasta
              </label>

              <input
                id="export-to"
                type="date"
                className={fieldClass}
                value={dateTo}
                disabled={busy !== null}
                onChange={(event) => setDateTo(event.target.value)}
              />
            </div>
          </div>

          <div>
            <label
              htmlFor="export-limit"
              className="mb-2 block text-sm font-semibold"
            >
              Máximo de registros
            </label>

            <select
              id="export-limit"
              className={fieldClass}
              value={limit}
              disabled={busy !== null}
              onChange={(event) => setLimit(Number(event.target.value))}
            >
              {[200, 500, 1000, 3000, 5000, 10000, 50000].map((value) => (
                <option key={value} value={value}>
                  {value.toLocaleString()}
                  {value > 3000 ? " — solo Excel" : ""}
                </option>
              ))}
            </select>
          </div>

          {warning && (
            <div
              role="status"
              className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800"
            >
              {warning}
            </div>
          )}

          {error && (
            <div
              role="alert"
              className="rounded-xl bg-red-50 p-3 text-sm text-red-700"
            >
              {error}
            </div>
          )}

          {success && (
            <div
              role="status"
              className="rounded-xl bg-green-50 p-3 text-sm text-green-800"
            >
              {success}
            </div>
          )}

          <div className="flex flex-wrap gap-3">
            <Btn
              tone="primary"
              disabled={loading || busy !== null || !sqlOnline}
              onClick={() => void download("excel")}
            >
              <Download size={16} />
              {busy === "excel" ? "Generando Excel…" : "Descargar Excel"}
            </Btn>

            <Btn
              tone="ghost"
              disabled={loading || busy !== null || !sqlOnline}
              onClick={() => void download("pdf")}
            >
              <Download size={16} />
              {busy === "pdf" ? "Generando PDF…" : "Descargar PDF"}
            </Btn>

            <Btn
              tone="ghost"
              disabled={busy !== null}
              onClick={() => {
                setSelectedClock("");
                setDateFrom("");
                setDateTo("");
                setLimit(1000);
                setError("");
                setSuccess("");
              }}
            >
              Limpiar filtros
            </Btn>
          </div>
        </div>
      </Panel>
    </div>
  );
}