import {
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react'
import axios from 'axios'

import { reportsApi } from '../../api/reportsApi'
import type {
  ReportsOverview,
  ReportBreakdown,
} from '../../types/reports'

import './ReportsPage.css'

type Row = [string, number | string]

function sections(
  data: ReportsOverview,
): { title: string; rows: Row[] }[] {
  return [
    {
      title: 'Flota',
      rows: [
        ['Dispositivos', data.fleet.totalDevices],
        ['En línea', data.fleet.onlineDevices],
        ['Fuera de línea', data.fleet.offlineDevices],
        ['Windows', data.fleet.windowsDevices],
        ['Android', data.fleet.androidDevices],
        ['Administrados', data.fleet.managedDevices],
        ['No administrados', data.fleet.unmanagedDevices],
        ['Conformes', data.fleet.compliantDevices],
        ['No conformes', data.fleet.nonCompliantDevices],
        ['Cuarentena', data.fleet.quarantinedDevices],
      ],
    },
    {
      title: 'Comandos últimos 30 días',
      rows: [
        ['Total', data.commands.totalLast30Days],
        ['Exitosos', data.commands.successful],
        ['Fallidos', data.commands.failed],
        ['Tiempo agotado', data.commands.timeout],
        ['Cancelados', data.commands.cancelled],
        ['Activos', data.commands.active],
        ['Tasa de éxito', `${data.commands.successRate}%`],
      ],
    },
    {
      title: 'Seguridad',
      rows: [
        ['Evaluados', data.security.evaluatedDevices],
        [
          'Score promedio',
          `${data.security.averageComplianceScore}%`,
        ],
        ['Riesgo crítico', data.security.criticalRiskDevices],
        ['Riesgo alto', data.security.highRiskDevices],
      ],
    },
    {
      title: 'Sistemas operativos',
      rows: data.operatingSystems.map(x => [x.label, x.value]),
    },
    {
      title: 'Departamentos',
      rows: data.departments.map(x => [x.label, x.value]),
    },
    {
      title: 'Tipos de comando',
      rows: data.commandTypes.map(x => [x.label, x.value]),
    },
  ]
}

function failure(error: unknown) {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status

    return status
      ? `El backend respondió HTTP ${status}. Revisa el registro de /api/reports/overview.`
      : 'No se pudo conectar con el backend.'
  }

  return 'No fue posible completar la operación.'
}

export function ReportsPage() {
  const [data, setData] =
    useState<ReportsOverview | null>(null)
  const [loading, setLoading] = useState(false)
  const [exporting, setExporting] = useState('')
  const [error, setError] = useState('')

  const generation = useRef(0)

  const load = useCallback(async () => {
    const current = ++generation.current

    setLoading(true)
    setError('')

    try {
      const result = await reportsApi.getOverview()

      if (current === generation.current) {
        setData(result)
      }
    } catch (error) {
      if (current === generation.current) {
        setError(failure(error))
      }
    } finally {
      if (current === generation.current) {
        setLoading(false)
      }
    }
  }, [])

  useEffect(() => {
    void load()

    return () => {
      generation.current++
    }
  }, [load])

  async function download(
    format: 'csv' | 'pdf' | 'excel',
  ) {
    if (!data || exporting) return

    setExporting(format)
    setError('')

    const name =
      `TitanMDM-reportes-${new Date().toISOString().slice(0, 10)}`

    try {
      if (format === 'csv') {
        await reportsApi.exportDevicesCsv()
        return
      }

      const groups = sections(data)

      if (format === 'excel') {
        const XLSX = await import('xlsx')
        const workbook = XLSX.utils.book_new()

        XLSX.utils.book_append_sheet(
          workbook,
          XLSX.utils.aoa_to_sheet([
            ['TitanMDM', 'Reporte global: Windows y Android'],
            [
              'Generado',
              new Date(data.generatedAtUtc).toLocaleString(),
            ],
          ]),
          'Información',
        )

        for (const group of groups) {
          const sheet = XLSX.utils.aoa_to_sheet([
            ['Indicador', 'Valor'],
            ...group.rows,
          ])

          sheet['!cols'] = [
            { wch: 45 },
            { wch: 24 },
          ]

          XLSX.utils.book_append_sheet(
            workbook,
            sheet,
            group.title.slice(0, 31),
          )
        }

        XLSX.writeFile(workbook, `${name}.xlsx`)
      } else {
        const [
          { jsPDF },
          { default: autoTable },
        ] = await Promise.all([
          import('jspdf'),
          import('jspdf-autotable'),
        ])

        const doc = new jsPDF()

        doc.setFontSize(18)
        doc.text(
          'TitanMDM - Reportes operativos',
          14,
          18,
        )

        doc.setFontSize(10)
        doc.text(
          'Alcance global: Windows y Android',
          14,
          26,
        )
        doc.text(
          `Generado: ${new Date(data.generatedAtUtc).toLocaleString()}`,
          14,
          33,
        )

        let y = 42

        for (const group of groups) {
          if (y > 240) {
            doc.addPage()
            y = 18
          }

          doc.setFontSize(12)
          doc.text(group.title, 14, y)

          autoTable(doc, {
            startY: y + 4,
            head: [['Indicador', 'Valor']],
            body: group.rows,
            theme: 'striped',
            styles: { fontSize: 9 },
            headStyles: {
              fillColor: [37, 99, 235],
            },
            margin: {
              top: 18,
              bottom: 18,
            },
          })

          const table = (
            doc as unknown as {
              lastAutoTable?: { finalY: number }
            }
          ).lastAutoTable

          y = (table?.finalY ?? y + 20) + 14
        }

        doc.save(`${name}.pdf`)
      }
    } catch (error) {
      setError(
        format === 'csv'
          ? failure(error)
          : 'No fue posible generar la exportación.',
      )
    } finally {
      setExporting('')
    }
  }

  return (
    <div className="reports-page">
      <header className="reports-header">
        <div>
          <span>TITANMDM REPORTING</span>
          <h1>Reportes operativos</h1>
          <p>
            Flota, comandos, seguridad y cumplimiento
            de toda la organización.
          </p>
        </div>

        <div className="reports-actions">
          <button
            type="button"
            disabled={loading}
            onClick={() => void load()}
          >
            {loading ? 'Actualizando…' : 'Actualizar'}
          </button>

          <button
            type="button"
            disabled={!data || !!exporting}
            onClick={() => void download('pdf')}
          >
            PDF
          </button>

          <button
            type="button"
            disabled={!data || !!exporting}
            onClick={() => void download('excel')}
          >
            Excel
          </button>

          <button
            type="button"
            className="primary"
            disabled={!data || !!exporting}
            onClick={() => void download('csv')}
          >
            CSV de dispositivos
          </button>
        </div>
      </header>

      {error && (
        <div className="reports-error" role="alert">
          {error}
        </div>
      )}

      {exporting && (
        <p role="status">
          Preparando exportación {exporting}…
        </p>
      )}

      {!data ? (
        <article className="reports-panel">
          <p role="status">
            {loading
              ? 'Cargando información del backend…'
              : 'No hay un reporte disponible. Reintenta con Actualizar.'}
          </p>
        </article>
      ) : (
        <>
          <p>
            Alcance: Windows y Android. Los comandos
            corresponden a los últimos 30 días.
          </p>

          <section className="reports-stats">
            {[
              ['Dispositivos', data.fleet.totalDevices],
              ['En línea', data.fleet.onlineDevices],
              ['Fuera de línea', data.fleet.offlineDevices],
              ['Conformes', data.fleet.compliantDevices],
              [
                'Score de seguridad',
                `${data.security.averageComplianceScore}%`,
              ],
            ].map(([label, value]) => (
              <article
                key={label}
                className="reports-stat"
              >
                <div>
                  <span>{label}</span>
                  <strong>{value}</strong>
                </div>
              </article>
            ))}
          </section>

          <div className="reports-grid">
            {sections(data).slice(0, 3).map(group => (
              <article
                key={group.title}
                className="reports-panel"
              >
                <header>
                  <strong>{group.title}</strong>
                </header>

                <div className="reports-metrics">
                  {group.rows.map(([label, value]) => (
                    <div
                      key={label}
                      className="reports-metric"
                    >
                      <span>{label}</span>
                      <strong>{value}</strong>
                    </div>
                  ))}
                </div>
              </article>
            ))}
          </div>

          <div className="reports-grid reports-grid-three">
            <Breakdown
              title="Sistemas operativos"
              items={data.operatingSystems}
            />
            <Breakdown
              title="Departamentos"
              items={data.departments}
            />
            <Breakdown
              title="Tipos de comando"
              items={data.commandTypes}
            />
          </div>

          <footer className="reports-generated">
            Generado:{' '}
            {new Date(data.generatedAtUtc).toLocaleString()}.
            PDF y Excel contienen el resumen y los desgloses;
            CSV contiene los dispositivos.
          </footer>
        </>
      )}
    </div>
  )
}

function Breakdown({
  title,
  items,
}: {
  title: string
  items: ReportBreakdown[]
}) {
  const maximum = Math.max(
    1,
    ...items.map(item => item.value),
  )

  return (
    <article className="reports-panel">
      <header>
        <strong>{title}</strong>
      </header>

      <div className="reports-breakdown">
        {!items.length ? (
          <p className="reports-empty">Sin datos.</p>
        ) : (
          items.map(item => (
            <div key={item.label}>
              <div>
                <span>{item.label}</span>
                <strong>{item.value}</strong>
              </div>

              <div className="reports-bar">
                <span
                  style={{
                    width:
                      `${(item.value / maximum) * 100}%`,
                  }}
                />
              </div>
            </div>
          ))
        )}
      </div>
    </article>
  )
}