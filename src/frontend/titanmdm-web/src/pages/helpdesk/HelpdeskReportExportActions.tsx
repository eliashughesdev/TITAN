
import { useState } from 'react'
import { Download, FileSpreadsheet, FileText } from 'lucide-react'
import { jsPDF } from 'jspdf'
import * as XLSX from 'xlsx'

type Count = {
  label: string
  count: number
}

type SlaMetric = {
  evaluated: number
  met: number
  breached: number
  compliancePercent: number
}

type ReportSummary = {
  total: number
  resolved: number
  unresolved: number
  currentBacklog: number
  unassigned: number
  pendingUser: number
  overdueFirstResponse: number
  overdueResolution: number
  firstResponseSla: SlaMetric
  resolutionSla: SlaMetric
  byStatus: Count[]
  byPriority: Count[]
  byCategory: Count[]
  bySource: Count[]
  bySite: Array<Count & { resolved: number }>
  byAgent: Array<Count & {
    resolved: number
    active: number
    resolutionRate: number
    slaBreached: number
  }>
  backlogAging: Count[]
  daily: Array<{
    date: string
    created: number
    resolved: number
  }>
  generatedAtUtc: string
}

type Props = {
  summary: ReportSummary | null
  from: string
  to: string
}

const statusLabels: Record<string, string> = {
  new: 'Nuevo',
  open: 'Abierto',
  inprogress: 'En proceso',
  pendinguser: 'En espera por usuario',
  resolved: 'Resuelto',
  closed: 'Cerrado',
}

function labelStatus(value: string): string {
  return statusLabels[value.toLowerCase()] ?? value
}

function reportRows(summary: ReportSummary) {
  return [
    ['Total de tickets', summary.total],
    ['Tickets resueltos', summary.resolved],
    ['Tickets pendientes', summary.unresolved],
    ['Backlog actual', summary.currentBacklog],
    ['Sin asignar', summary.unassigned],
    ['En espera por usuario', summary.pendingUser],
    ['Primera respuesta vencida', summary.overdueFirstResponse],
    ['Resolución vencida', summary.overdueResolution],
    [
      'Cumplimiento SLA primera respuesta',
      `${summary.firstResponseSla.compliancePercent}%`,
    ],
    [
      'Cumplimiento SLA resolución',
      `${summary.resolutionSla.compliancePercent}%`,
    ],
  ] as Array<[string, string | number]>
}

function createExcel(
  summary: ReportSummary,
  from: string,
  to: string,
) {
  const book = XLSX.utils.book_new()

  const overview = [
    ['TitanMDM - Mesa de Ayuda'],
    ['Fecha inicial', from],
    ['Fecha final', to],
    ['Generado UTC', summary.generatedAtUtc],
    [],
    ['Indicador', 'Valor'],
    ...reportRows(summary),
  ]

  XLSX.utils.book_append_sheet(
    book,
    XLSX.utils.aoa_to_sheet(overview),
    'Resumen',
  )

  const sheets: Array<{
    name: string
    rows: Record<string, string | number>[]
  }> = [
    {
      name: 'Estados',
      rows: summary.byStatus.map(x => ({
        Estado: labelStatus(x.label),
        Cantidad: x.count,
      })),
    },
    {
      name: 'Prioridades',
      rows: summary.byPriority.map(x => ({
        Prioridad: x.label,
        Cantidad: x.count,
      })),
    },
    {
      name: 'Categorias',
      rows: summary.byCategory.map(x => ({
        Categoria: x.label,
        Cantidad: x.count,
      })),
    },
    {
      name: 'Canales',
      rows: summary.bySource.map(x => ({
        Canal: x.label,
        Cantidad: x.count,
      })),
    },
    {
      name: 'Localidades',
      rows: summary.bySite.map(x => ({
        Localidad: x.label,
        Total: x.count,
        Resueltos: x.resolved,
      })),
    },
    {
      name: 'Agentes',
      rows: summary.byAgent.map(x => ({
        Agente: x.label,
        Total: x.count,
        Resueltos: x.resolved,
        Activos: x.active,
        PorcentajeResolucion: x.resolutionRate,
        SlaVencidos: x.slaBreached,
      })),
    },
    {
      name: 'Antiguedad',
      rows: summary.backlogAging.map(x => ({
        Periodo: x.label,
        Cantidad: x.count,
      })),
    },
    {
      name: 'Tendencia',
      rows: summary.daily.map(x => ({
        Fecha: x.date,
        Creados: x.created,
        Resueltos: x.resolved,
      })),
    },
  ]

  for (const sheet of sheets) {
    XLSX.utils.book_append_sheet(
      book,
      sheet.rows.length
        ? XLSX.utils.json_to_sheet(sheet.rows)
        : XLSX.utils.aoa_to_sheet([['Sin datos']]),
      sheet.name,
    )
  }

  XLSX.writeFile(
    book,
    `TitanMDM-Helpdesk-${from}-${to}.xlsx`,
  )
}

function createPdf(
  summary: ReportSummary,
  from: string,
  to: string,
) {
  const pdf = new jsPDF({
    orientation: 'portrait',
    unit: 'mm',
    format: 'a4',
  })

  const margin = 17
  const pageHeight = pdf.internal.pageSize.getHeight()

  let y = 20

  function ensureSpace(height: number) {
    if (y + height > pageHeight - 18) {
      pdf.addPage()
      y = 20
    }
  }

  function heading(title: string) {
    ensureSpace(18)
    pdf.setFontSize(13)
    pdf.setTextColor(30, 64, 124)
    pdf.text(title, margin, y)
    y += 9
    pdf.setTextColor(30, 41, 59)
  }

  function line(label: string, value: string | number) {
    const text = `${label}: ${value}`
    pdf.setFontSize(9)

    const lines = pdf.splitTextToSize(
      text,
      pdf.internal.pageSize.getWidth() - margin * 2,
    ) as string[]

    ensureSpace(lines.length * 5 + 3)
    pdf.text(lines, margin, y)
    y += lines.length * 5 + 3
  }

  pdf.setFontSize(19)
  pdf.setTextColor(30, 64, 124)
  pdf.text('TitanMDM', margin, y)
  y += 9

  pdf.setFontSize(12)
  pdf.text('Informe ejecutivo de Mesa de Ayuda', margin, y)
  y += 10

  pdf.setFontSize(9)
  pdf.setTextColor(71, 85, 105)
  pdf.text(`Periodo: ${from} - ${to}`, margin, y)
  y += 6
  pdf.text(
    `Generado UTC: ${summary.generatedAtUtc}`,
    margin,
    y,
  )
  y += 12

  heading('Indicadores generales')

  for (const [label, value] of reportRows(summary)) {
    line(label, value)
  }

  y += 5
  heading('Distribucion por estado')

  for (const row of summary.byStatus) {
    line(labelStatus(row.label), row.count)
  }

  y += 5
  heading('Distribucion por localidad')

  for (const row of summary.bySite) {
    line(
      row.label,
      `${row.count} tickets / ${row.resolved} resueltos`,
    )
  }

  y += 5
  heading('Desempeno por agente')

  for (const row of summary.byAgent) {
    line(
      row.label,
      `${row.resolved} resueltos / ${row.active} activos / ` +
      `${row.slaBreached} vencidos`,
    )
  }

  const pages = pdf.getNumberOfPages()

  for (let page = 1; page <= pages; page++) {
    pdf.setPage(page)
    pdf.setFontSize(8)
    pdf.setTextColor(100, 116, 139)
    pdf.text(
      `TitanMDM | Pagina ${page} de ${pages}`,
      margin,
      pageHeight - 10,
    )
  }

  pdf.save(`TitanMDM-Helpdesk-${from}-${to}.pdf`)
}

export function HelpdeskReportExportActions({
  summary,
  from,
  to,
}: Props) {
  const [busy, setBusy] = useState<'pdf' | 'xlsx' | null>(
    null,
  )

  const [error, setError] = useState('')

  const enabled = Boolean(
    summary && from && to && from <= to,
  )

  function execute(type: 'pdf' | 'xlsx') {
    if (!summary || !enabled || busy) return

    setBusy(type)
    setError('')

    try {
      if (type === 'pdf') {
        createPdf(summary, from, to)
      } else {
        createExcel(summary, from, to)
      }
    } catch {
      setError(
        `No fue posible generar el archivo ${type.toUpperCase()}.`,
      )
    } finally {
      setBusy(null)
    }
  }

  return (
    <>
      <button
        type="button"
        className="helpdesk-ui-button helpdesk-ui-button--secondary"
        disabled={!enabled || busy !== null}
        onClick={() => execute('xlsx')}
        title="Exportar indicadores y tablas a Excel"
      >
        <FileSpreadsheet size={16} />
        {busy === 'xlsx' ? 'Exportando...' : 'Excel'}
      </button>

      <button
        type="button"
        className="helpdesk-ui-button helpdesk-ui-button--secondary"
        disabled={!enabled || busy !== null}
        onClick={() => execute('pdf')}
        title="Generar informe ejecutivo PDF"
      >
        <FileText size={16} />
        {busy === 'pdf' ? 'Exportando...' : 'PDF'}
      </button>

      {error && (
        <span role="alert" className="helpdesk-inbox__error">
          <Download size={14} aria-hidden="true" />
          {error}
        </span>
      )}
    </>
  )
}
