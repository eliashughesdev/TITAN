/*
 * ITAM - Exportacion real de reportes de inventario (P2).
 *
 * Excel: libro multi-hoja (Resumen, Depreciacion, Activos y
 * distribuciones) con estilo Azul Cielo, filtros y paneles
 * congelados. PDF: informe A4 horizontal con KPIs y tablas.
 * Todos los datos provienen del API (/api/itam), es decir de
 * SQL Server; no hay datos de ejemplo.
 */
import type { Asset, AssetDepreciation, Dashboard, NameCount } from './inventoryApi'

const SKY_900 = '0C4A6E'
const SKY_600 = '0284C7'
const SKY_50 = 'F0F9FF'
const WHITE = 'FFFFFF'

export type InventoryReportData = {
  dashboard: Dashboard
  assets: Asset[]
  depreciation: AssetDepreciation[]
}

type Table = {
  name: string
  headers: string[]
  rows: Array<Array<string | number>>
  moneyColumns?: number[]
}

const money = (value: number | null | undefined) =>
  `RD$ ${Number(value ?? 0).toLocaleString('es-DO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

const today = () => new Date().toISOString().slice(0, 10)

function summaryRows(d: Dashboard): Array<[string, string | number]> {
  return [
    ['Activos totales', d.totalAssets],
    ['Asignados', d.assignedAssets],
    ['Disponibles', d.availableAssets],
    ['Hand Held', d.handHeldAssets],
    ['Dañados', d.damagedAssets],
    ['Dados de baja', d.retiredAssets],
    ['Activos en riesgo', d.atRiskAssets],
    ['Colaboradores', d.totalEmployees],
    ['Entregas activas', d.activeDeliveries],
    ['Devoluciones', d.returnedDeliveries],
    ['Recepciones pendientes', d.pendingReceptions],
    ['Garantías por vencer (90 días)', d.warrantiesExpiringSoon],
    ['Tasa de rotación (%)', d.rotationRate],
    ['Costo total', money(d.totalPurchaseCost)],
    ['Depreciación acumulada', money(d.accumulatedDepreciation)],
    ['Valor en libros', money(d.bookValue)],
  ]
}

const distribution = (name: string, rows: NameCount[]): Table => ({
  name,
  headers: ['Grupo', 'Cantidad'],
  rows: rows.map(row => [row.name || 'Sin definir', row.count]),
})

export function buildInventoryTables(data: InventoryReportData): Table[] {
  return [
    {
      name: 'Depreciación',
      headers: ['Tag', 'Activo', 'Costo', 'Vida útil (años)', 'Años transcurridos', 'Dep. anual', 'Dep. acumulada', 'Valor en libros'],
      moneyColumns: [2, 5, 6, 7],
      rows: data.depreciation.map(row => [
        row.assetTag,
        row.name,
        row.purchaseCost,
        row.depreciationYears ?? 'N/D',
        Number(row.yearsElapsed.toFixed(2)),
        row.annualDepreciation,
        row.accumulatedDepreciation,
        row.bookValue,
      ]),
    },
    {
      name: 'Activos',
      headers: ['Tag', 'Nombre', 'Serie', 'Estado', 'Condición', 'Costo', 'Fin garantía', 'Asignado a'],
      moneyColumns: [5],
      rows: data.assets.map(asset => [
        asset.assetTag,
        asset.name,
        asset.serialNumber ?? '',
        asset.status,
        asset.condition,
        asset.purchaseCost ?? 0,
        asset.warrantyEnd ?? '',
        asset.assignedToName ?? '',
      ]),
    },
    distribution('Por estado', data.dashboard.byStatus),
    distribution('Por condición', data.dashboard.byCondition),
    distribution('Por categoría', data.dashboard.byCategory),
    distribution('Por sucursal', data.dashboard.byBranch),
    distribution('Por departamento', data.dashboard.byDepartment),
  ]
}

function triggerDownload(bytes: BlobPart, mime: string, filename: string) {
  const url = URL.createObjectURL(new Blob([bytes], { type: mime }))
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export async function exportInventoryExcel(data: InventoryReportData) {
  const module = await import('exceljs')
  const ExcelJS = module.default ?? module

  const book = new ExcelJS.Workbook()
  book.creator = 'TitanMDM'
  book.subject = 'Informe de inventario ITAM'
  book.created = new Date()

  const summary = book.addWorksheet('Resumen', { views: [{ state: 'frozen', ySplit: 4 }] })
  summary.mergeCells('A1:D2')
  const title = summary.getCell('A1')
  title.value = 'TITANMDM | INVENTARIO ITAM'
  title.font = { size: 20, bold: true, color: { argb: WHITE } }
  title.alignment = { vertical: 'middle', indent: 1 }
  for (let col = 1; col <= 4; col++) {
    for (let row = 1; row <= 2; row++) {
      summary.getCell(row, col).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_900 } }
    }
  }
  summary.getCell('A3').value = `Generado: ${new Date().toLocaleString('es-DO')}`
  summary.getCell('A3').font = { italic: true, color: { argb: '64748B' } }

  const head = summary.getRow(4)
  head.values = ['Indicador', 'Valor']
  head.eachCell(cell => {
    cell.font = { bold: true, color: { argb: WHITE } }
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_600 } }
  })

  summaryRows(data.dashboard).forEach(([label, value], index) => {
    const row = summary.getRow(index + 5)
    row.values = [label, value]
    row.getCell(2).font = { bold: true, color: { argb: SKY_900 } }
    if (index % 2 === 0) {
      row.eachCell(cell => {
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_50 } }
      })
    }
  })
  summary.getColumn(1).width = 36
  summary.getColumn(2).width = 24

  for (const table of buildInventoryTables(data)) {
    const sheet = book.addWorksheet(table.name, { views: [{ state: 'frozen', ySplit: 1 }] })
    const header = sheet.getRow(1)
    header.values = table.headers
    header.height = 22
    header.eachCell(cell => {
      cell.font = { bold: true, color: { argb: WHITE } }
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_600 } }
      cell.alignment = { vertical: 'middle' }
    })

    const rows = table.rows.length > 0 ? table.rows : [['Sin datos disponibles']]
    rows.forEach((values, index) => {
      const row = sheet.getRow(index + 2)
      row.values = values
      if (index % 2 === 0) {
        row.eachCell(cell => {
          cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_50 } }
        })
      }
    })

    for (const column of table.moneyColumns ?? []) {
      sheet.getColumn(column + 1).numFmt = '"RD$" #,##0.00'
    }

    table.headers.forEach((label, index) => {
      const longest = Math.max(
        label.length,
        ...rows.slice(0, 200).map(row => String(row[index] ?? '').length),
      )
      sheet.getColumn(index + 1).width = Math.min(48, Math.max(12, longest + 2))
    })

    sheet.autoFilter = { from: { row: 1, column: 1 }, to: { row: 1, column: table.headers.length } }
  }

  const buffer = await book.xlsx.writeBuffer()
  triggerDownload(
    buffer as ArrayBuffer,
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    `TitanMDM-Inventario-${today()}.xlsx`,
  )
}

export async function exportInventoryPdf(data: InventoryReportData) {
  const [{ jsPDF }, { default: autoTable }] = await Promise.all([
    import('jspdf'),
    import('jspdf-autotable'),
  ])

  const pdf = new jsPDF({ unit: 'mm', format: 'a4', orientation: 'landscape' })
  const pageWidth = pdf.internal.pageSize.getWidth()

  pdf.setFillColor(12, 74, 110)
  pdf.rect(0, 0, pageWidth, 24, 'F')
  pdf.setTextColor(255, 255, 255)
  pdf.setFont('helvetica', 'bold')
  pdf.setFontSize(17)
  pdf.text('TitanMDM · Informe de inventario ITAM', 12, 15)
  pdf.setFont('helvetica', 'normal')
  pdf.setFontSize(9)
  pdf.text(`Generado: ${new Date().toLocaleString('es-DO')}`, pageWidth - 12, 15, { align: 'right' })

  const kpis = summaryRows(data.dashboard)
  autoTable(pdf, {
    startY: 30,
    head: [['Indicador', 'Valor', 'Indicador', 'Valor']],
    body: Array.from({ length: Math.ceil(kpis.length / 2) }, (_, i) => [
      kpis[i * 2]?.[0] ?? '',
      String(kpis[i * 2]?.[1] ?? ''),
      kpis[i * 2 + 1]?.[0] ?? '',
      String(kpis[i * 2 + 1]?.[1] ?? ''),
    ]),
    theme: 'grid',
    headStyles: { fillColor: [2, 132, 199], textColor: 255 },
    alternateRowStyles: { fillColor: [240, 249, 255] },
    styles: { fontSize: 9 },
  })

  for (const table of buildInventoryTables(data)) {
    pdf.addPage()
    pdf.setTextColor(12, 74, 110)
    pdf.setFont('helvetica', 'bold')
    pdf.setFontSize(13)
    pdf.text(table.name, 12, 14)

    const moneyColumns = new Set(table.moneyColumns ?? [])
    autoTable(pdf, {
      startY: 19,
      head: [table.headers],
      body: (table.rows.length ? table.rows : [['Sin datos disponibles']]).map(row =>
        row.map((value, index) =>
          moneyColumns.has(index) && typeof value === 'number' ? money(value) : String(value ?? ''),
        ),
      ),
      theme: 'striped',
      headStyles: { fillColor: [2, 132, 199], textColor: 255 },
      alternateRowStyles: { fillColor: [240, 249, 255] },
      styles: { fontSize: 8, cellPadding: 1.6 },
      didDrawPage: () => {
        pdf.setFontSize(8)
        pdf.setTextColor(100, 116, 139)
        pdf.text(
          `TitanMDM ITAM · página ${pdf.getNumberOfPages()}`,
          pageWidth - 12,
          pdf.internal.pageSize.getHeight() - 6,
          { align: 'right' },
        )
      },
    })
  }

  triggerDownload(pdf.output('arraybuffer'), 'application/pdf', `TitanMDM-Inventario-${today()}.pdf`)
}

// ============================================================
// REPORTES OPERATIVOS (entregas del dia, devoluciones pendientes)
// ============================================================

export type OperationalReport = {
  /** Titulo del informe y nombre de la hoja. */
  title: string
  /** Base del nombre de archivo, sin extension. */
  fileBase: string
  /** Descripcion del filtro aplicado (p.ej. "Entregas del 2026-11-04"). */
  scope: string
  headers: string[]
  rows: Array<Array<string | number>>
}

const safeSheetName = (value: string) => value.replace(/[\/?*[\]:]/g, ' ').slice(0, 31) || 'Reporte'

export async function exportOperationalExcel(report: OperationalReport) {
  const module = await import('exceljs')
  const ExcelJS = module.default ?? module

  const book = new ExcelJS.Workbook()
  book.creator = 'TitanMDM'
  book.subject = report.title
  book.created = new Date()

  const sheet = book.addWorksheet(safeSheetName(report.title), { views: [{ state: 'frozen', ySplit: 4 }] })
  const span = Math.max(report.headers.length, 2)

  sheet.mergeCells(1, 1, 1, span)
  const title = sheet.getCell(1, 1)
  title.value = `TITANMDM · ${report.title.toUpperCase()}`
  title.font = { size: 16, bold: true, color: { argb: WHITE } }
  title.alignment = { vertical: 'middle', indent: 1 }
  sheet.getRow(1).height = 28
  for (let col = 1; col <= span; col++) {
    sheet.getCell(1, col).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_900 } }
  }

  sheet.mergeCells(2, 1, 2, span)
  sheet.getCell(2, 1).value = `${report.scope} · ${report.rows.length} registro(s) · generado ${new Date().toLocaleString('es-DO')}`
  sheet.getCell(2, 1).font = { italic: true, color: { argb: '64748B' } }

  const header = sheet.getRow(4)
  header.values = report.headers
  header.eachCell(cell => {
    cell.font = { bold: true, color: { argb: WHITE } }
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_600 } }
  })

  const rows = report.rows.length > 0 ? report.rows : [['Sin registros para el filtro seleccionado']]
  rows.forEach((values, index) => {
    const row = sheet.getRow(index + 5)
    row.values = values
    if (index % 2 === 0) {
      row.eachCell(cell => {
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: SKY_50 } }
      })
    }
  })

  report.headers.forEach((label, index) => {
    const longest = Math.max(label.length, ...rows.slice(0, 200).map(row => String(row[index] ?? '').length))
    sheet.getColumn(index + 1).width = Math.min(48, Math.max(12, longest + 2))
  })

  sheet.autoFilter = { from: { row: 4, column: 1 }, to: { row: 4, column: report.headers.length } }

  const buffer = await book.xlsx.writeBuffer()
  triggerDownload(
    buffer as ArrayBuffer,
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    `${report.fileBase}-${today()}.xlsx`,
  )
}

export async function exportOperationalPdf(report: OperationalReport) {
  const [{ jsPDF }, { default: autoTable }] = await Promise.all([
    import('jspdf'),
    import('jspdf-autotable'),
  ])

  const pdf = new jsPDF({ unit: 'mm', format: 'a4', orientation: 'landscape' })
  const pageWidth = pdf.internal.pageSize.getWidth()

  pdf.setFillColor(12, 74, 110)
  pdf.rect(0, 0, pageWidth, 22, 'F')
  pdf.setTextColor(255, 255, 255)
  pdf.setFont('helvetica', 'bold')
  pdf.setFontSize(15)
  pdf.text(`TitanMDM · ${report.title}`, 12, 14)
  pdf.setFont('helvetica', 'normal')
  pdf.setFontSize(9)
  pdf.text(new Date().toLocaleString('es-DO'), pageWidth - 12, 14, { align: 'right' })

  pdf.setTextColor(71, 85, 105)
  pdf.text(`${report.scope} · ${report.rows.length} registro(s)`, 12, 29)

  autoTable(pdf, {
    startY: 33,
    head: [report.headers],
    body: report.rows.length
      ? report.rows.map(row => row.map(value => String(value ?? '')))
      : [['Sin registros para el filtro seleccionado']],
    theme: 'striped',
    headStyles: { fillColor: [2, 132, 199], textColor: 255 },
    alternateRowStyles: { fillColor: [240, 249, 255] },
    styles: { fontSize: 8, cellPadding: 1.6 },
  })

  triggerDownload(pdf.output('arraybuffer'), 'application/pdf', `${report.fileBase}-${today()}.pdf`)
}
