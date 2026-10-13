
import { useState } from 'react'
import { Download, FileSpreadsheet, FileText } from 'lucide-react'
import { jsPDF } from 'jspdf'
import autoTable from 'jspdf-autotable'
import type ExcelJS from 'exceljs'

type Count = {
  label: string
  count: number
}

type SiteCount = Count & {
  resolved: number
}

type AgentCount = Count & {
  resolved: number
  active: number
  resolutionRate: number
  slaBreached: number
}

type SlaMetric = {
  evaluated: number
  met: number
  breached: number
  compliancePercent: number
}

type AutomationMetrics = {
  autoAssigned: number
  autoHandovers: number
  classifications: number
  classificationReviews: number
  escalations: number
  reminders: number
  overdueAlerts: number
  reopened: number
}

type Summary = {
  total: number
  resolved: number
  unresolved: number
  currentBacklog: number
  unassigned: number
  pendingUser: number
  overdueFirstResponse: number
  overdueResolution: number
  averageFirstResponseHours: number | null
  medianFirstResponseHours: number | null
  averageResolutionHours: number | null
  medianResolutionHours: number | null
  firstResponseSla: SlaMetric
  resolutionSla: SlaMetric
  byStatus: Count[]
  byPriority: Count[]
  byCategory: Count[]
  bySource: Count[]
  bySite: SiteCount[]
  byAgent: AgentCount[]
  backlogAging: Count[]
  daily: Array<{
    date: string
    created: number
    resolved: number
  }>
  automation: AutomationMetrics
  generatedAtUtc: string
}

interface Props {
  summary: Summary | null
  from: string
  to: string
}

const NAVY = '172B55'
const BLUE = '5268E9'
const WHITE = 'FFFFFF'

const palette = [
  '#5268e9',
  '#28b9dd',
  '#25a47d',
  '#f1a43a',
  '#9a71e2',
  '#ed6872',
  '#7184a6',
]

function friendlyStatus(value: string) {
  const labels: Record<string, string> = {
    new: 'Nuevo',
    open: 'Abierto',
    inprogress: 'En proceso',
    pendinguser: 'Espera por usuario',
    resolved: 'Resuelto',
    closed: 'Cerrado',
  }

  return labels[value.toLowerCase()] ?? value
}

function summaryMetrics(s: Summary) {
  return [
    ['Tickets totales', s.total],
    ['Resueltos', s.resolved],
    ['Pendientes', s.unresolved],
    ['Backlog actual', s.currentBacklog],
    ['Sin asignar', s.unassigned],
    ['En espera de usuario', s.pendingUser],
    ['Primera respuesta vencida', s.overdueFirstResponse],
    ['Resolución vencida', s.overdueResolution],
    [
      'SLA primera respuesta (%)',
      s.firstResponseSla.compliancePercent,
    ],
    [
      'SLA resolución (%)',
      s.resolutionSla.compliancePercent,
    ],
    [
      'Media primera respuesta (h)',
      s.averageFirstResponseHours ?? 'N/D',
    ],
    [
      'Mediana primera respuesta (h)',
      s.medianFirstResponseHours ?? 'N/D',
    ],
    [
      'Media resolución (h)',
      s.averageResolutionHours ?? 'N/D',
    ],
    [
      'Mediana resolución (h)',
      s.medianResolutionHours ?? 'N/D',
    ],
  ] as Array<[string, string | number]>
}

function automationRows(s: Summary) {
  return [
    ['Asignaciones automáticas', s.automation.autoAssigned],
    ['Transferencias automáticas', s.automation.autoHandovers],
    ['Clasificaciones', s.automation.classifications],
    ['Revisiones de clasificación', s.automation.classificationReviews],
    ['Escalamientos', s.automation.escalations],
    ['Recordatorios', s.automation.reminders],
    ['Alertas por vencimiento', s.automation.overdueAlerts],
    ['Reaperturas', s.automation.reopened],
  ] as Array<[string, number]>
}

function makeChart(
  kind: 'bar' | 'line',
  labels: string[],
  primary: number[],
  secondary?: number[],
  legend = 'Tickets',
): string {
  const canvas = document.createElement('canvas')

  canvas.width = 1200
  canvas.height = 540

  const ctx = canvas.getContext('2d')

  if (!ctx) throw new Error('Canvas no disponible.')

  const W = canvas.width
  const H = canvas.height
  const left = 85
  const top = 70
  const plotW = 1050
  const plotH = 340

  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, W, H)

  ctx.fillStyle = '#172b55'
  ctx.font = 'bold 25px Arial'
  ctx.fillText(legend, 45, 40)

  const series = [...primary, ...(secondary ?? [])]
  const max = Math.max(1, ...series)
  const scaleMax = Math.ceil(max * 1.12)

  ctx.font = '17px Arial'
  ctx.strokeStyle = '#e6eaf3'
  ctx.fillStyle = '#66748e'

  for (let i = 0; i <= 4; i++) {
    const y = top + (plotH * i) / 4
    ctx.beginPath()
    ctx.moveTo(left, y)
    ctx.lineTo(left + plotW, y)
    ctx.stroke()

    ctx.fillText(
      String(Math.round(scaleMax * (1 - i / 4))),
      18,
      y + 6,
    )
  }

  const count = Math.max(1, labels.length)
  const step = plotW / count

  if (kind === 'bar') {
    labels.forEach((_, i) => {
      const x = left + i * step + step * 0.2
      const barW = Math.max(5, step * (secondary ? 0.26 : 0.55))

      const firstH =
        (primary[i] / scaleMax) * plotH

      ctx.fillStyle = palette[i % palette.length]
      ctx.fillRect(
        x,
        top + plotH - firstH,
        barW,
        firstH,
      )

      if (secondary) {
        const nextH =
          (secondary[i] / scaleMax) * plotH

        ctx.fillStyle = '#25a47d'
        ctx.fillRect(
          x + barW + 3,
          top + plotH - nextH,
          barW,
          nextH,
        )
      }
    })
  } else {
    const drawLine = (
      values: number[],
      color: string,
    ) => {
      ctx.strokeStyle = color
      ctx.lineWidth = 4
      ctx.beginPath()

      values.forEach((value, i) => {
        const x = left + step * (i + 0.5)
        const y =
          top + plotH - (value / scaleMax) * plotH

        if (i === 0) ctx.moveTo(x, y)
        else ctx.lineTo(x, y)
      })

      ctx.stroke()
    }

    drawLine(primary, '#5268e9')

    if (secondary) {
      drawLine(secondary, '#25a47d')
    }
  }

  // Reducir etiquetas visibles en tendencias extensas.
  const every = Math.max(
    1,
    Math.ceil(labels.length / 9),
  )

  ctx.fillStyle = '#526078'
  ctx.textAlign = 'center'
  ctx.font = '15px Arial'

  labels.forEach((label, i) => {
    if (i % every !== 0 && i !== labels.length - 1) {
      return
    }

    const text =
      label.length > 15
        ? `${label.slice(0, 13)}…`
        : label

    ctx.fillText(
      text,
      left + step * (i + 0.5),
      top + plotH + 32,
      Math.max(60, step * every - 8),
    )
  })

  ctx.textAlign = 'left'
  ctx.font = 'bold 17px Arial'

  ctx.fillStyle = '#5268e9'
  ctx.fillRect(70, 480, 17, 17)
  ctx.fillStyle = '#32425c'
  ctx.fillText(
    secondary ? 'Creados / Total' : 'Tickets',
    96,
    494,
  )

  if (secondary) {
    ctx.fillStyle = '#25a47d'
    ctx.fillRect(360, 480, 17, 17)
    ctx.fillStyle = '#32425c'
    ctx.fillText('Resueltos', 386, 494)
  }

  return canvas.toDataURL('image/png')
}

function chartImages(s: Summary) {
  const status = s.byStatus
  const priorities = s.byPriority

  return {
    status: makeChart(
      'bar',
      status.map(x => friendlyStatus(x.label)),
      status.map(x => x.count),
      undefined,
      'Distribución por estado',
    ),
    priority: makeChart(
      'bar',
      priorities.map(x => x.label),
      priorities.map(x => x.count),
      undefined,
      'Distribución por prioridad',
    ),
    trend: makeChart(
      'line',
      s.daily.map(x => x.date),
      s.daily.map(x => x.created),
      s.daily.map(x => x.resolved),
      'Tendencia de tickets',
    ),
  }
}

function tableData(s: Summary) {
  return [
    {
      name: 'Estados',
      headers: ['Estado', 'Cantidad'],
      rows: s.byStatus.map(x => [
        friendlyStatus(x.label),
        x.count,
      ]),
    },
    {
      name: 'Prioridades',
      headers: ['Prioridad', 'Cantidad'],
      rows: s.byPriority.map(x => [
        x.label,
        x.count,
      ]),
    },
    {
      name: 'Categorias',
      headers: ['Categoría', 'Cantidad'],
      rows: s.byCategory.map(x => [
        x.label,
        x.count,
      ]),
    },
    {
      name: 'Canales',
      headers: ['Origen', 'Cantidad'],
      rows: s.bySource.map(x => [
        x.label,
        x.count,
      ]),
    },
    {
      name: 'Localidades',
      headers: ['Localidad', 'Total', 'Resueltos'],
      rows: s.bySite.map(x => [
        x.label,
        x.count,
        x.resolved,
      ]),
    },
    {
      name: 'Tecnicos',
      headers: [
        'Técnico',
        'Total',
        'Resueltos',
        'Activos',
        'Resolución %',
        'SLA vencidos',
      ],
      rows: s.byAgent.map(x => [
        x.label,
        x.count,
        x.resolved,
        x.active,
        x.resolutionRate,
        x.slaBreached,
      ]),
    },
    {
      name: 'Antiguedad',
      headers: ['Antigüedad', 'Tickets'],
      rows: s.backlogAging.map(x => [
        x.label,
        x.count,
      ]),
    },
    {
      name: 'Tendencia',
      headers: ['Fecha', 'Creados', 'Resueltos'],
      rows: s.daily.map(x => [
        x.date,
        x.created,
        x.resolved,
      ]),
    },
    {
      name: 'Automatizaciones',
      headers: ['Operación', 'Cantidad'],
      rows: automationRows(s),
    },
    {
      name: 'SLA',
      headers: [
        'Tipo',
        'Evaluados',
        'Cumplidos',
        'Vencidos',
        'Cumplimiento %',
      ],
      rows: [
        [
          'Primera respuesta',
          s.firstResponseSla.evaluated,
          s.firstResponseSla.met,
          s.firstResponseSla.breached,
          s.firstResponseSla.compliancePercent,
        ],
        [
          'Resolución',
          s.resolutionSla.evaluated,
          s.resolutionSla.met,
          s.resolutionSla.breached,
          s.resolutionSla.compliancePercent,
        ],
      ],
    },
  ] satisfies Array<{
    name: string
    headers: string[]
    rows: Array<Array<string | number>>
  }>
}

function triggerDownload(
  bytes: BlobPart,
  mime: string,
  filename: string,
) {
  const url = URL.createObjectURL(
    new Blob([bytes], { type: mime }),
  )

  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()

  window.setTimeout(
    () => URL.revokeObjectURL(url),
    1000,
  )
}

async function exportExcel(
  s: Summary,
  from: string,
  to: string,
) {
const ExcelJSModule = await import('exceljs')

const ExcelJS =
  ExcelJSModule.default ?? ExcelJSModule

if (!ExcelJS?.Workbook) {
  throw new Error(
    'No fue posible cargar ExcelJS.Workbook en el navegador.',
  )
}

const book = new ExcelJS.Workbook()
  book.creator = 'TitanMDM'
  book.subject = 'Informe empresarial de Mesa de Ayuda'
  book.created = new Date()

  const metrics = summaryMetrics(s)
  const charts = chartImages(s)

  const cover = book.addWorksheet('Resumen')
  cover.mergeCells('A1:H2')

for (let row = 1; row <= 2; row++) {
  for (let col = 1; col <= 8; col++) {
    const cell = cover.getCell(row, col)

    cell.fill = {
      type: 'pattern',
      pattern: 'solid',
      fgColor: { argb: NAVY },
    }
  }
}

cover.getRow(1).height = 32
cover.getRow(2).height = 18

const title = cover.getCell('A1')

title.value = 'TITANMDM | MESA DE AYUDA'

title.font = {
  name: 'Aptos Display',
  size: 21,
  bold: true,
  color: { argb: WHITE },
}

title.alignment = {
  vertical: 'middle',
  horizontal: 'left',
  indent: 1,
}

  cover.getCell('A4').value = 'DESDE'
  cover.getCell('B4').value = from
  cover.getCell('D4').value = 'HASTA'
  cover.getCell('E4').value = to
  cover.getCell('A5').value = 'GENERADO UTC'
  cover.getCell('B5').value = s.generatedAtUtc

  cover.getColumn('A').width = 38
  cover.getColumn('B').width = 22

  cover.getCell('A7').value = 'INDICADOR'
  cover.getCell('B7').value = 'VALOR'

 // ============================================================
// HD-F6 — ENCABEZADO EXCEL
// Corrección de iteración de filas ExcelJS
// ============================================================

const metricsHeader = cover.getRow(7)

metricsHeader.height = 27

metricsHeader.getCell(1).value = 'INDICADOR'
metricsHeader.getCell(2).value = 'VALOR'

// Aplicar formato al rango completo, incluyendo celdas
// que no tienen datos, sin depender de row.cells.
for (let column = 1; column <= 8; column++) {
  const cell = metricsHeader.getCell(column)

  cell.fill = {
    type: 'pattern',
    pattern: 'solid',
    fgColor: { argb: BLUE },
  }

  cell.font = {
    name: 'Aptos',
    bold: true,
    size: 11,
    color: { argb: WHITE },
  }

  cell.alignment = {
    vertical: 'middle',
    horizontal: column === 2 ? 'right' : 'left',
    indent: 1,
  }

  cell.border = {
    bottom: {
      style: 'thin',
      color: { argb: '314AB9' },
    },
  }
}

  metrics.forEach(([label, value], index) => {
    const row = cover.getRow(index + 8)

    row.values = [label, value]

    if (index % 2 === 0) {
      row.eachCell(cell => {
        cell.fill = {
          type: 'pattern',
          pattern: 'solid',
          fgColor: { argb: 'F2F5FC' },
        }
      })
    }

    row.getCell(2).font = {
      bold: true,
      color: { argb: NAVY },
    }
  })

  // Las imágenes de gráficos se incrustan en el Excel.
  const addExcelImage = (
    sheet: ExcelJS.Worksheet,
    image: string,
    col: number,
    row: number,
  ) => {
    const imageId = book.addImage({
      base64: image,
      extension: 'png',
    })

    sheet.addImage(imageId, {
      tl: { col, row },
      ext: { width: 570, height: 256 },
    })
  }

  addExcelImage(cover, charts.status, 3, 7)
  addExcelImage(cover, charts.priority, 3, 23)
  addExcelImage(cover, charts.trend, 3, 39)

  cover.views = [
    { state: 'frozen', ySplit: 6 },
  ]

  for (const entry of tableData(s)) {
    const sheet = book.addWorksheet(entry.name)

    sheet.mergeCells(
      1, 1, 2, entry.headers.length,
    )

    const heading = sheet.getCell(1, 1)
    heading.value = `TITANMDM · ${entry.name.toUpperCase()}`
    heading.font = {
      bold: true,
      size: 17,
      color: { argb: WHITE },
    }
    heading.fill = {
      type: 'pattern',
      pattern: 'solid',
      fgColor: { argb: NAVY },
    }
    heading.alignment = {
      vertical: 'middle',
      indent: 1,
    }

    const header = sheet.getRow(4)
    header.values = entry.headers

    header.eachCell(cell => {
      cell.fill = {
        type: 'pattern',
        pattern: 'solid',
        fgColor: { argb: BLUE },
      }
      cell.font = {
        bold: true,
        color: { argb: WHITE },
      }
    })

   const safeRows =
  entry.rows.length > 0
    ? entry.rows
    : [['Sin datos disponibles']]

safeRows.forEach((values, index) => {
  const row = sheet.getRow(index + 5)

  row.values = values

  row.alignment = {
    vertical: 'middle',
  }

  row.height = 22

  row.eachCell(cell => {
    cell.border = {
      bottom: {
        style: 'hair',
        color: { argb: 'E1E8F4' },
      },
    }

    if (index % 2 === 0) {
      cell.fill = {
        type: 'pattern',
        pattern: 'solid',
        fgColor: { argb: 'F2F5FC' },
      }
    }
  })
})

sheet.autoFilter = {
  from: {
    row: 4,
    column: 1,
  },
  to: {
    row: 4 + safeRows.length,
    column: entry.headers.length,
  },
}

    sheet.views = [
      { state: 'frozen', ySplit: 4 },
    ]
  }

  const buffer = await book.xlsx.writeBuffer()

  triggerDownload(
    buffer as ArrayBuffer,
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    `TitanMDM-Helpdesk-${from}-${to}.xlsx`,
  )
}

async function exportPdf(
  s: Summary,
  from: string,
  to: string,
) {
  const pdf = new jsPDF({
    unit: 'mm',
    format: 'a4',
  })

  const charts = chartImages(s)

  const pageW = pdf.internal.pageSize.getWidth()
  const pageH = pdf.internal.pageSize.getHeight()

  // Portada
  pdf.setFillColor(17, 31, 66)
  pdf.rect(0, 0, pageW, 93, 'F')

  pdf.setFillColor(82, 104, 233)
  pdf.circle(187, 12, 58, 'F')

  pdf.setTextColor(255, 255, 255)
  pdf.setFont('helvetica', 'bold')
  pdf.setFontSize(26)
  pdf.text('TITANMDM', 18, 34)

  pdf.setFontSize(17)
  pdf.text('INFORME DE MESA DE AYUDA', 18, 49)

  pdf.setFontSize(10)
  pdf.text(
    `PERIODO ${from}  -  ${to}`,
    18,
    66,
  )

  pdf.setTextColor(36, 51, 81)
  pdf.setFontSize(12)
  pdf.text('Resumen ejecutivo', 18, 111)

  autoTable(pdf, {
    startY: 120,
    head: [['INDICADOR', 'VALOR']],
    body: summaryMetrics(s).map(([k, v]) => [
      k,
      String(v),
    ]),
    theme: 'striped',
    headStyles: {
      fillColor: [82, 104, 233],
    },
    styles: {
      fontSize: 9,
      cellPadding: 3,
    },
    margin: { left: 18, right: 18 },
  })

  const images = [
    charts.status,
    charts.priority,
    charts.trend,
  ]

  const titles = [
    'Distribución por estado',
    'Distribución por prioridad',
    'Tendencia de creación y resolución',
  ]

  images.forEach((img, index) => {
    pdf.addPage()
    pdf.setTextColor(23, 43, 85)
    pdf.setFont('helvetica', 'bold')
    pdf.setFontSize(16)
    pdf.text(titles[index], 17, 23)

    pdf.addImage(
      img,
      'PNG',
      17,
      37,
      176,
      83,
    )
  })

  for (const entry of tableData(s)) {
    pdf.addPage()

    pdf.setTextColor(23, 43, 85)
    pdf.setFont('helvetica', 'bold')
    pdf.setFontSize(15)
    pdf.text(entry.name, 16, 22)

    autoTable(pdf, {
      startY: 30,
      head: [entry.headers],
      body: entry.rows.map(row =>
        row.map(value => String(value)),
      ),
      theme: 'striped',
      headStyles: {
        fillColor: [23, 43, 85],
      },
      alternateRowStyles: {
        fillColor: [244, 247, 253],
      },
      styles: {
        fontSize: entry.headers.length > 4 ? 7 : 9,
        cellPadding: 2.4,
        overflow: 'linebreak',
      },
      margin: {
        left: 13,
        right: 13,
        bottom: 18,
      },
    })
  }

  const totalPages = pdf.getNumberOfPages()

  for (let page = 1; page <= totalPages; page++) {
    pdf.setPage(page)

    pdf.setDrawColor(222, 230, 243)
    pdf.line(
      15,
      pageH - 18,
      pageW - 15,
      pageH - 18,
    )

    pdf.setFontSize(8)
    pdf.setTextColor(105, 120, 151)

    pdf.text(
      'TitanMDM · Service Intelligence',
      15,
      pageH - 12,
    )

    pdf.text(
      `${page} / ${totalPages}`,
      pageW - 18,
      pageH - 12,
      { align: 'right' },
    )
  }

  pdf.save(
    `TitanMDM-Helpdesk-${from}-${to}.pdf`,
  )
}

export function HelpdeskReportExportActions({
  summary,
  from,
  to,
}: Props) {
  const [busy, setBusy] =
    useState<'pdf' | 'excel' | null>(null)

  const [error, setError] = useState('')

  const enabled =
    Boolean(summary) &&
    Boolean(from) &&
    Boolean(to) &&
    from <= to

  async function execute(
    format: 'pdf' | 'excel',
  ) {
    if (!summary || busy || !enabled) {
      return
    }

    setBusy(format)
    setError('')

    try {
      if (format === 'pdf') {
        await exportPdf(summary, from, to)
      } else {
        await exportExcel(summary, from, to)
      }
   } catch (err) {
  const detail =
    err instanceof Error
      ? err.message
      : String(err)

  console.error(
    `[TitanMDM] Error exportando ${format}:`,
    err,
  )

  setError(
    `No fue posible exportar ${format.toUpperCase()}: ${detail}`,
  )
} finally {
  setBusy(null)
}}
  return (
    <>
      <button
        type="button"
        className="helpdesk-ui-button helpdesk-ui-button--secondary"
        disabled={!enabled || busy !== null}
        onClick={() => void execute('excel')}
      >
        <FileSpreadsheet size={16} />
        {busy === 'excel'
          ? 'Generando Excel…'
          : 'Excel profesional'}
      </button>

      <button
        type="button"
        className="helpdesk-ui-button helpdesk-ui-button--secondary"
        disabled={!enabled || busy !== null}
        onClick={() => void execute('pdf')}
      >
        <FileText size={16} />
        {busy === 'pdf'
          ? 'Generando PDF…'
          : 'PDF profesional'}
      </button>

      {error && (
        <span role="alert" className="helpdesk-inbox__error">
          <Download size={15} />
          {error}
        </span>
      )}
    </>
  )
}
