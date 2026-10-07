import { useEffect, useState } from "react";
import {
  Area, AreaChart, Bar, BarChart, CartesianGrid, Cell,
  Legend, Pie, PieChart, ResponsiveContainer,
  Tooltip, XAxis, YAxis,
} from "recharts";
import { authFetch } from "../lib/api";

type Point = {
  name?: string;
  day?: string;
  hour?: number;
  entries: number;
  exits: number;
  rows?: number;
};

type Summary = {
  rows: number;
  employees: number;
  devices: number;
  entries: number;
  exits: number;
  complete: number;
  entry_only: number;
  exit_only: number;
  empty: number;
};

type Analytics = {
  summary: Summary;
  devices: { name: string }[];
  by_device: Point[];
  by_day: Point[];
  by_hour: Point[];
  period: {
    start: string | null;
    end: string | null;
    device: string;
    granularity: "day" | "month";
    global: boolean;
  };
};

const colors = ["#10b981", "#f59e0b", "#6366f1", "#94a3b8"];
const number = (value: number) => value.toLocaleString("es-DO");
const inputClass =
  "rounded-xl border border-rose-200 bg-white px-3 py-2 text-sm";

export default function SqlHistory() {
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [device, setDevice] = useState("");
  const [data, setData] = useState<Analytics | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function load(from: string, to: string, clock: string) {
    setBusy(true);
    setError("");
    setData(null);

    try {
      const params = new URLSearchParams({ device: clock });

      if (from) params.set("start", from);
      if (to) params.set("end", to);

      const response = await authFetch(
        `/api/schema/analytics?${params}`,
      );
      const result = await response.json();

      if (!response.ok) {
        throw new Error(
          typeof result.detail === "string"
            ? result.detail
            : "No se pudo consultar el historial SQL",
        );
      }

      setData(result as Analytics);
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "No se pudo conectar con el servidor",
      );
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    void load("", "", "");
  }, []);

  const s = data?.summary;

  const quality = s
    ? [
        { name: "Entrada y salida", value: s.complete },
        { name: "Solo entrada", value: s.entry_only },
        { name: "Solo salida", value: s.exit_only },
        { name: "Sin marcas", value: s.empty },
      ]
    : [];

  const clockData = (data?.by_device ?? []).map((point) => ({
    ...point,
    punches: point.entries + point.exits,
  }));

  const days = data?.by_day ?? [];

  const hours = Array.from({ length: 24 }, (_, hour) => ({
    ...(data?.by_hour.find((point) => point.hour === hour) ?? {
      entries: 0,
      exits: 0,
    }),
    label: `${String(hour).padStart(2, "0")}:00`,
  }));

  const deviceOptions = Array.from(
    new Set([
      ...(data?.devices.map((item) => item.name) ?? []),
      ...(device ? [device] : []),
    ]),
  );

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-xl font-bold text-zinc-900">
          Historial SQL · Analítica de relojes
        </h2>
        <p className="text-sm text-zinc-500">
          BioTimeDB · Solo lectura · Deja las fechas vacías
          para consultar todo el histórico
        </p>
      </div>

      <form
        className="flex flex-wrap items-end gap-3 rounded-2xl border border-rose-100 bg-white p-4"
        onSubmit={(event) => {
          event.preventDefault();
          void load(start, end, device);
        }}
      >
        <label className="grid gap-1 text-sm">
          Desde
          <input
            type="date"
            value={start}
            disabled={busy}
            max={end || undefined}
            onChange={(event) => setStart(event.target.value)}
            className={inputClass}
          />
        </label>

        <label className="grid gap-1 text-sm">
          Hasta
          <input
            type="date"
            value={end}
            disabled={busy}
            min={start || undefined}
            onChange={(event) => setEnd(event.target.value)}
            className={inputClass}
          />
        </label>

        <label className="grid gap-1 text-sm">
          Reloj
          <select
            value={device}
            disabled={busy}
            onChange={(event) => setDevice(event.target.value)}
            className={`${inputClass} max-w-sm`}
          >
            <option value="">Todos los relojes con datos SQL</option>
            {deviceOptions.map((name) => (
              <option key={name} value={name}>{name}</option>
            ))}
          </select>
        </label>

        <button
          type="button"
          disabled={busy}
          onClick={() => {
            setStart("");
            setEnd("");
            setDevice("");
            void load("", "", "");
          }}
          className="rounded-xl border border-rose-200 px-4 py-2 text-sm"
        >
          Ver todo el histórico
        </button>

        <button
          type="submit"
          disabled={busy}
          className="rounded-xl bg-rose-700 px-5 py-2 text-sm font-semibold text-white disabled:opacity-50"
        >
          {busy ? "Consultando…" : "Aplicar filtros"}
        </button>
      </form>

      {error && (
        <div
          role="alert"
          className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-rose-700"
        >
          {error}
        </div>
      )}

      {busy && (
        <p role="status" className="p-6 text-center text-zinc-500">
          Calculando indicadores en SQL…
        </p>
      )}

      {data && s && (
        <>
          <p className="text-sm text-zinc-500">
            {data.period.global
              ? "Histórico global"
              : "Período filtrado"}
            {" · "}
            {data.period.start || "Sin registros"} al{" "}
            {data.period.end || "Sin registros"}
            {" · "}
            {data.period.device || "Todos los relojes"}
          </p>

          <div className="grid grid-cols-2 gap-3 xl:grid-cols-6">
            {[
              ["Ponches almacenados", s.entries + s.exits],
              ["Entradas", s.entries],
              ["Salidas", s.exits],
              ["Colaboradores únicos", s.employees],
              ["Relojes con registros", s.devices],
              ["Filas diarias", s.rows],
            ].map(([label, value]) => (
              <div
                key={String(label)}
                className="rounded-2xl border border-rose-100 bg-white p-4"
              >
                <p className="text-xs text-zinc-500">{label}</p>
                <p className="mt-2 text-2xl font-bold text-rose-700">
                  {number(Number(value))}
                </p>
              </div>
            ))}
          </div>

          {!s.rows ? (
            <div className="rounded-2xl bg-white p-10 text-center text-zinc-500">
              No hay registros SQL para estos filtros.
              Esto no indica el estado de conexión del reloj.
            </div>
          ) : (
            <>
              <section className="rounded-2xl border border-rose-100 bg-white p-5">
                <h3 className="font-semibold">
                  Entradas y salidas por{" "}
                  {data.period.granularity === "month" ? "mes" : "día"}
                </h3>
                <div className="mt-4 h-72">
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={days}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis
                        dataKey="day"
                        tickFormatter={(value) =>
                          data.period.granularity === "month"
                            ? String(value)
                            : String(value).slice(5)
                        }
                        minTickGap={25}
                      />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Legend />
                      <Area
                        type="monotone"
                        dataKey="entries"
                        name="Entradas"
                        stroke="#be123c"
                        fill="#ffe4e6"
                      />
                      <Area
                        type="monotone"
                        dataKey="exits"
                        name="Salidas"
                        stroke="#0284c7"
                        fill="#e0f2fe"
                      />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
              </section>

              <div className="grid gap-5 xl:grid-cols-2">
                <section className="rounded-2xl border border-rose-100 bg-white p-5">
                  <h3 className="font-semibold">Ponches por reloj</h3>
                  <p className="text-xs text-zinc-500">
                    Entrada + salida. Desplázate para ver todos los relojes.
                  </p>
                  <div className="mt-4 max-h-[520px] overflow-y-auto">
                    <div
                      style={{
                        height: Math.max(280, clockData.length * 44),
                      }}
                    >
                      <ResponsiveContainer width="100%" height="100%">
                        <BarChart
                          layout="vertical"
                          data={clockData}
                          margin={{ left: 8, right: 25 }}
                        >
                          <CartesianGrid strokeDasharray="3 3" />
                          <XAxis type="number" allowDecimals={false} />
                          <YAxis
                            type="category"
                            dataKey="name"
                            width={160}
                            tick={{ fontSize: 10 }}
                          />
                          <Tooltip />
                          <Bar
                            dataKey="punches"
                            name="Ponches"
                            fill="#be123c"
                            radius={[0, 5, 5, 0]}
                          />
                        </BarChart>
                      </ResponsiveContainer>
                    </div>
                  </div>
                </section>

                <section className="rounded-2xl border border-rose-100 bg-white p-5">
                  <h3 className="font-semibold">
                    Completitud de las filas diarias
                  </h3>
                  <div className="mt-4 h-72">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie
                          data={quality}
                          dataKey="value"
                          nameKey="name"
                          innerRadius={65}
                          outerRadius={100}
                          paddingAngle={2}
                        >
                          {quality.map((point, index) => (
                            <Cell
                              key={point.name}
                              fill={colors[index]}
                            />
                          ))}
                        </Pie>
                        <Tooltip />
                        <Legend />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                  <p className="text-xs text-zinc-500">
                    Una fila sin salida puede corresponder a una
                    jornada abierta; no se clasifica automáticamente
                    como error.
                  </p>
                </section>
              </div>

              <section className="rounded-2xl border border-rose-100 bg-white p-5">
                <h3 className="font-semibold">
                  Distribución de ponches por hora
                </h3>
                <div className="mt-4 h-72">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={hours}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="label" minTickGap={15} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Legend />
                      <Bar
                        dataKey="entries"
                        name="Entradas"
                        fill="#be123c"
                        radius={[4, 4, 0, 0]}
                      />
                      <Bar
                        dataKey="exits"
                        name="Salidas"
                        fill="#0284c7"
                        radius={[4, 4, 0, 0]}
                      />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </section>
            </>
          )}

          <p className="text-xs text-zinc-500">
            Los indicadores reflejan las entradas y salidas
            conservadas en dbo.punches. No representan todas
            las marcas brutas del reloj ni su disponibilidad en línea.
          </p>
        </>
      )}
    </div>
  );
}