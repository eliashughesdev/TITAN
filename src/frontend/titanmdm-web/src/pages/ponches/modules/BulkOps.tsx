import {
  useEffect,
  useMemo,
  useState,
} from "react";

import {
  AlertTriangle,
  CheckCircle2,
  Play,
  RefreshCw,
  ShieldCheck,
  Upload,
  Users,
  X,
} from "lucide-react";

import {
  authFetch,
} from "../lib/api";

import {
  Btn,
  PageHeader,
  Panel,
} from "../ui/kit";

type Device = {
  name: string;
  online?: boolean;
};

type BulkRow = {
  codigo: string;
  nombre: string;
  password: string;
  card: number;
  dispositivo: string;
};

type Result = {
  codigo: string;
  ok: boolean;
  error?: string;

  zk?: {
    mode?: string;
  };
};

type BulkResponse = {
  ok?: number;
  fail?: number;
  would?: string;
  count?: number;
  results?: Result[];
  message?: string;
  detail?: string;
};

async function jsonOrThrow<T>(
  response: Response,
): Promise<T> {
  const data =
    await response
      .json()
      .catch(() => ({})) as T & {
        detail?: string;
        message?: string;
      };

  if (!response.ok) {
    throw new Error(
      data.detail ||
      data.message ||
      `Error HTTP ${response.status}`,
    );
  }

  return data;
}

export default function BulkOps() {
  const [
    devices,
    setDevices,
  ] =
    useState<Device[]>([]);

  const [
    device,
    setDevice,
  ] =
    useState("");

  const [
    raw,
    setRaw,
  ] =
    useState(
      "codigo,nombre,password,card\n" +
      "1001,Juan Perez,1234,0\n" +
      "1002,Ana Gomez,1234,0",
    );

  const [
    loadingDevices,
    setLoadingDevices,
  ] =
    useState(true);

  const [
    running,
    setRunning,
  ] =
    useState(false);

  const [
    error,
    setError,
  ] =
    useState("");

  const [
    message,
    setMessage,
  ] =
    useState("");

  const [
    results,
    setResults,
  ] =
    useState<Result[]>([]);

  const [
    preview,
    setPreview,
  ] =
    useState<{
      rows: BulkRow[];
      result: BulkResponse;
    } | null>(null);

  const parse =
    (): BulkRow[] => {
      const lines =
        raw
          .split(/\r?\n/)
          .map(
            line =>
              line.trim(),
          )
          .filter(Boolean);

      if (!lines.length) {
        return [];
      }

      const first =
        lines[0]
          .toLowerCase();

      const start =
        first.includes("codigo")
          ? 1
          : 0;

      const rows: BulkRow[] =
        [];

      for (
        const line
        of lines.slice(start)
      ) {
        const parts =
          line
            .split(",")
            .map(
              value =>
                value.trim(),
            );

        if (!parts[0]) {
          continue;
        }

        rows.push({
          codigo:
            parts[0],

          nombre:
            parts[1] || "",

          password:
            parts[2] || "",

          card:
            Number(
              parts[3] || 0,
            ),

          dispositivo:
            device,
        });
      }

      return rows;
    };

  const parsedRows =
    useMemo(
      () =>
        parse(),
      [
        raw,
        device,
      ],
    );

  useEffect(
    () => {
      const controller =
        new AbortController();

      const load =
        async () => {
          setLoadingDevices(true);

          try {
            const response =
              await authFetch(
                "/api/records/managed-devices",
                {
                  signal:
                    controller.signal,
                },
              );

            const data =
              await jsonOrThrow<{
                items?: Device[];
              }>(
                response,
              );

            if (
              !controller
                .signal
                .aborted
            ) {
              setDevices(
                data.items ||
                [],
              );
            }
          }
          catch (cause) {
            if (
              !controller
                .signal
                .aborted
            ) {
              setError(
                cause instanceof Error
                  ? cause.message
                  : "No se pudieron cargar los relojes.",
              );
            }
          }
          finally {
            if (
              !controller
                .signal
                .aborted
            ) {
              setLoadingDevices(
                false,
              );
            }
          }
        };

      void load();

      return () =>
        controller.abort();
    },
    [],
  );

  const resetPreview =
    () => {
      setPreview(null);
      setResults([]);
      setMessage("");
    };

  const validate =
    (): BulkRow[] => {
      if (!device) {
        throw new Error(
          "Selecciona un reloj destino.",
        );
      }

      if (
        parsedRows.length ===
        0
      ) {
        throw new Error(
          "No existen filas válidas.",
        );
      }

      if (
        parsedRows.length >
        500
      ) {
        throw new Error(
          "La operación masiva está limitada a 500 usuarios por lote.",
        );
      }

      const duplicateCodes =
        parsedRows
          .map(
            row =>
              row.codigo,
          )
          .filter(
            (
              value,
              index,
              array,
            ) =>
              array.indexOf(
                value,
              ) !==
              index,
          );

      if (
        duplicateCodes.length
      ) {
        throw new Error(
          `Existen códigos duplicados: ${
            [
              ...new Set(
                duplicateCodes,
              ),
            ]
              .slice(0, 10)
              .join(", ")
          }`,
        );
      }

      return parsedRows;
    };

  const inspect =
    async () => {
      if (running) {
        return;
      }

      setError("");
      setMessage("");
      setResults([]);

      try {
        const rows =
          validate();

        setRunning(true);

        const response =
          await authFetch(
            "/api/records/bulk-enroll",
            {
              method:
                "POST",

              headers: {
                "Content-Type":
                  "application/json",
              },

              body:
                JSON.stringify({
                  rows,

                  dry_run:
                    true,

                  confirm:
                    false,
                }),
            },
          );

        const result =
          await jsonOrThrow<BulkResponse>(
            response,
          );

        setPreview({
          rows,
          result,
        });
      }
      catch (cause) {
        setError(
          cause instanceof Error
            ? cause.message
            : "No se pudo preparar la operación.",
        );
      }
      finally {
        setRunning(
          false,
        );
      }
    };

  const execute =
    async () => {
      if (
        !preview ||
        running
      ) {
        return;
      }

      setRunning(true);
      setError("");
      setMessage("");

      try {
        const response =
          await authFetch(
            "/api/records/bulk-enroll",
            {
              method:
                "POST",

              headers: {
                "Content-Type":
                  "application/json",
              },

              body:
                JSON.stringify({
                  rows:
                    preview.rows,

                  dry_run:
                    false,

                  confirm:
                    true,
                }),
            },
          );

        const data =
          await jsonOrThrow<BulkResponse>(
            response,
          );

        setResults(
          data.results ||
          [],
        );

        setMessage(
          `${
            data.ok ?? 0
          } completados · ${
            data.fail ?? 0
          } con error`,
        );

        setPreview(
          null,
        );
      }
      catch (cause) {
        setPreview(
          null,
        );

        setError(
          (
            cause instanceof Error
              ? cause.message
              : "La operación masiva falló."
          ) +
          " Consulta el reloj antes de repetir la operación.",
        );
      }
      finally {
        setRunning(
          false,
        );
      }
    };

  return (
    <div className="space-y-5 page-enter">
      <PageHeader
        kicker="Administración biométrica"
        title="Operaciones masivas"
        subtitle="Inscripción controlada de colaboradores en relojes ZKTeco."
      />

      <div className="grid gap-3 md:grid-cols-3">
        <Metric
          title="Filas"
          value={
            parsedRows.length
          }
          subtitle="Detectadas"
        />

        <Metric
          title="Destino"
          value={
            device
              ? 1
              : 0
          }
          subtitle={
            device ||
            "Sin reloj"
          }
        />

        <Metric
          title="Límite"
          value={
            500
          }
          subtitle="Por lote"
        />
      </div>

      {error && (
        <div className="flex gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
          <AlertTriangle
            size={17}
          />

          {error}
        </div>
      )}

      {message && (
        <div className="flex gap-2 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700">
          <CheckCircle2
            size={17}
          />

          {message}
        </div>
      )}

      <Panel>
        <div className="grid gap-4 lg:grid-cols-[320px_1fr]">
          <div className="space-y-3">
            <label className="grid gap-1 text-xs font-semibold text-zinc-600">
              Reloj destino

              <select
                value={
                  device
                }
                disabled={
                  loadingDevices ||
                  running
                }
                onChange={
                  event => {
                    setDevice(
                      event.target.value,
                    );

                    resetPreview();
                  }
                }
                className="rounded-xl border border-zinc-200 bg-white px-3 py-2.5 text-sm"
              >
                <option value="">
                  Selecciona un dispositivo
                </option>

                {devices.map(
                  item => (
                    <option
                      key={
                        item.name
                      }
                      value={
                        item.name
                      }
                    >
                      {item.name}
                      {
                        item.online ===
                        false
                          ? " · offline"
                          : ""
                      }
                    </option>
                  ),
                )}
              </select>
            </label>

            <div className="rounded-xl bg-zinc-50 p-3 text-xs text-zinc-500">
              Formato:
              <pre className="mt-2 overflow-auto text-[11px]">
codigo,nombre,password,card
1001,Juan Perez,1234,0
              </pre>
            </div>

            <Btn
              tone="primary"
              disabled={
                running ||
                !device ||
                parsedRows.length ===
                  0
              }
              onClick={
                () =>
                  void inspect()
              }
            >
              {running
                ? (
                  <RefreshCw
                    size={16}
                    className="animate-spin"
                  />
                )
                : (
                  <Upload
                    size={16}
                  />
                )}

              Previsualizar lote
            </Btn>
          </div>

          <textarea
            value={
              raw
            }
            disabled={
              running
            }
            onChange={
              event => {
                setRaw(
                  event.target.value,
                );

                resetPreview();
              }
            }
            className="min-h-[310px] w-full rounded-2xl border border-zinc-200 bg-zinc-950 p-4 font-mono text-sm text-zinc-100 outline-none"
          />
        </div>
      </Panel>

      {preview && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-zinc-950/40 p-4 backdrop-blur-sm">
          <div className="w-full max-w-xl rounded-2xl bg-white p-6 shadow-2xl">
            <div className="flex items-start justify-between">
              <div>
                <div className="flex items-center gap-2 font-bold">
                  <ShieldCheck
                    className="text-red-600"
                    size={20}
                  />

                  Confirmar operación masiva
                </div>

                <p className="mt-2 text-sm text-zinc-500">
                  Se escribirán hasta {preview.rows.length} usuarios en:
                </p>

                <strong className="mt-1 block">
                  {device}
                </strong>
              </div>

              <button
                type="button"
                onClick={() =>
                  setPreview(
                    null,
                  )
                }
                className="text-zinc-400 hover:text-zinc-700"
              >
                <X
                  size={20}
                />
              </button>
            </div>

            <div className="my-5 grid grid-cols-2 gap-3">
              <Metric
                title="Usuarios"
                value={
                  preview.rows
                    .length
                }
                subtitle="Preparados"
              />

              <Metric
                title="Destino"
                value={1}
                subtitle={
                  device
                }
              />
            </div>

            <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
              La previsualización no modifica el reloj.
              La siguiente acción sí realizará cambios físicos.
            </div>

            <div className="mt-5 flex justify-end gap-2">
              <Btn
                tone="ghost"
                onClick={() =>
                  setPreview(
                    null,
                  )
                }
              >
                Cancelar
              </Btn>

              <Btn
                tone="primary"
                disabled={
                  running
                }
                onClick={
                  () =>
                    void execute()
                }
              >
                <Play
                  size={16}
                />

                Confirmar ejecución
              </Btn>
            </div>
          </div>
        </div>
      )}

      {results.length >
        0 && (
        <Panel>
          <div className="mb-3 flex items-center gap-2">
            <Users
              size={18}
            />

            <h3 className="font-bold">
              Resultado
            </h3>
          </div>

          <div className="max-h-[500px] overflow-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase text-zinc-500">
                  <th className="py-2">
                    Código
                  </th>

                  <th>
                    Estado
                  </th>

                  <th>
                    Detalle
                  </th>
                </tr>
              </thead>

              <tbody>
                {results.map(
                  result => (
                    <tr
                      key={
                        result.codigo
                      }
                      className="border-b border-zinc-100"
                    >
                      <td className="py-3 font-mono text-xs">
                        {
                          result.codigo
                        }
                      </td>

                      <td>
                        <span
                          className={
                            result.ok
                              ? "rounded-full bg-emerald-50 px-2 py-1 text-xs font-semibold text-emerald-700"
                              : "rounded-full bg-rose-50 px-2 py-1 text-xs font-semibold text-rose-700"
                          }
                        >
                          {result.ok
                            ? "OK"
                            : "Error"}
                        </span>
                      </td>

                      <td className="text-xs text-zinc-500">
                        {
                          result.error ||
                          result.zk
                            ?.mode ||
                          "—"
                        }
                      </td>
                    </tr>
                  ),
                )}
              </tbody>
            </table>
          </div>
        </Panel>
      )}
    </div>
  );
}

function Metric({
  title,
  value,
  subtitle,
}: {
  title: string;
  value: number;
  subtitle: string;
}) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-zinc-500">
        {title}
      </p>

      <p className="mt-1 text-2xl font-black">
        {value}
      </p>

      <p className="mt-1 truncate text-xs text-zinc-400">
        {subtitle}
      </p>
    </div>
  );
}