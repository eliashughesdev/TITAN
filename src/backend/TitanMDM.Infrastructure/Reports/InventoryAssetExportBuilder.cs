using System.Globalization;
using System.Text;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace TitanMDM.Infrastructure.Reports;

/// <summary>Fila de la exportación de activos, con los catálogos ya resueltos a nombre.</summary>
public sealed record InventoryAssetExportRow(
    string AssetTag,
    string Name,
    string? SerialNumber,
    string Status,
    string Condition,
    string? Category,
    string? Manufacturer,
    string? Model,
    string? Branch,
    string? Department,
    string? AssignedTo,
    string? DeviceId,
    string? OsName,
    string? LastUser,
    string? PurchaseDate,
    string? WarrantyEnd,
    decimal? PurchaseCost);

/// <summary>Filtros aplicados, en texto legible para el encabezado del archivo.</summary>
public sealed record InventoryAssetExportFilters(IReadOnlyList<KeyValuePair<string, string>> Applied)
{
    public string Summary => Applied.Count == 0
        ? "Sin filtros (todos los activos)"
        : string.Join(" · ", Applied.Select(x => $"{x.Key}: {x.Value}"));
}

/// <summary>Genera los archivos XLSX y PDF del listado de activos de Inventario.</summary>
public static class InventoryAssetExportBuilder
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly string[] ExcelHeaders =
    [
        "Etiqueta", "Nombre", "Serie", "Estado", "Condición", "Categoría", "Fabricante", "Modelo",
        "Sucursal", "Departamento", "Asignado a", "Dispositivo MDM", "Sistema operativo",
        "Último usuario", "Fecha de compra", "Fin de garantía", "Costo",
    ];

    // ============================================================
    // XLSX
    // ============================================================

    public static byte[] BuildExcel(
        IReadOnlyList<InventoryAssetExportRow> rows,
        InventoryAssetExportFilters filters,
        DateTime generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(filters);

        using var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();
            stylesPart.Stylesheet.Save();

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            var assetsPart = workbookPart.AddNewPart<WorksheetPart>();
            assetsPart.Worksheet = BuildAssetsSheet(rows);
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(assetsPart),
                SheetId = 1,
                Name = "Activos",
            });

            var filtersPart = workbookPart.AddNewPart<WorksheetPart>();
            filtersPart.Worksheet = BuildFiltersSheet(rows.Count, filters, generatedAtUtc);
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(filtersPart),
                SheetId = 2,
                Name = "Filtros",
            });

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Worksheet BuildAssetsSheet(IReadOnlyList<InventoryAssetExportRow> rows)
    {
        var sheetData = new SheetData();

        var header = new Row { RowIndex = 1 };
        foreach (var title in ExcelHeaders)
        {
            header.Append(TextCell(title, StyleHeader));
        }
        sheetData.Append(header);

        uint rowIndex = 2;
        foreach (var asset in rows)
        {
            var row = new Row { RowIndex = rowIndex++ };
            row.Append(
                TextCell(asset.AssetTag),
                TextCell(asset.Name),
                TextCell(asset.SerialNumber),
                TextCell(asset.Status),
                TextCell(asset.Condition),
                TextCell(asset.Category),
                TextCell(asset.Manufacturer),
                TextCell(asset.Model),
                TextCell(asset.Branch),
                TextCell(asset.Department),
                TextCell(asset.AssignedTo),
                TextCell(asset.DeviceId),
                TextCell(asset.OsName),
                TextCell(asset.LastUser),
                TextCell(FormatDate(asset.PurchaseDate)),
                TextCell(FormatDate(asset.WarrantyEnd)),
                asset.PurchaseCost is { } cost ? NumberCell(cost) : TextCell(null));
            sheetData.Append(row);
        }

        var lastRow = Math.Max(1, rows.Count + 1);
        var lastColumn = ColumnName(ExcelHeaders.Length);

        var worksheet = new Worksheet(
            new SheetViews(new SheetView(
                new Pane
                {
                    VerticalSplit = 1,
                    TopLeftCell = "A2",
                    ActivePane = PaneValues.BottomLeft,
                    State = PaneStateValues.Frozen,
                })
            { WorkbookViewId = 0 }),
            new Columns(
                Width(1, 1, 16), Width(2, 2, 34), Width(3, 3, 22), Width(4, 5, 14),
                Width(6, 10, 20), Width(11, 11, 28), Width(12, 12, 38), Width(13, 14, 24),
                Width(15, 16, 16), Width(17, 17, 14)),
            sheetData,
            new AutoFilter { Reference = $"A1:{lastColumn}{lastRow}" });

        return worksheet;
    }

    private static Worksheet BuildFiltersSheet(
        int total,
        InventoryAssetExportFilters filters,
        DateTime generatedAtUtc)
    {
        var sheetData = new SheetData();
        uint index = 1;

        void Add(string label, string value, uint labelStyle = StyleHeader)
        {
            var row = new Row { RowIndex = index++ };
            row.Append(TextCell(label, labelStyle), TextCell(value));
            sheetData.Append(row);
        }

        Add("Reporte", "Activos de Inventario (CISA TI)");
        Add("Generado (UTC)", generatedAtUtc.ToString("yyyy-MM-dd HH:mm", Invariant));
        Add("Total de activos", total.ToString(Invariant));
        if (filters.Applied.Count == 0)
        {
            Add("Filtros", "Sin filtros (todos los activos)");
        }
        foreach (var filter in filters.Applied)
        {
            Add(filter.Key, filter.Value);
        }

        return new Worksheet(new Columns(Width(1, 1, 22), Width(2, 2, 60)), sheetData);
    }

    private const uint StyleHeader = 1;
    private const uint StyleNumber = 2;

    private static Stylesheet CreateStylesheet()
        => new(
            new Fonts(
                new Font(),
                new Font(new Bold(), new Color { Rgb = "FFFFFFFF" })),
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                new Fill(new PatternFill(
                    new ForegroundColor { Rgb = "FF1F3A5F" })
                { PatternType = PatternValues.Solid })),
            new Borders(new Border()),
            new CellStyleFormats(new CellFormat()),
            new CellFormats(
                new CellFormat(),
                new CellFormat { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true },
                new CellFormat { NumberFormatId = 4, ApplyNumberFormat = true }));

    private static Column Width(uint min, uint max, double width)
        => new() { Min = min, Max = max, Width = width, CustomWidth = true };

    private static Cell TextCell(string? value, uint style = 0)
        => new()
        {
            DataType = CellValues.InlineString,
            StyleIndex = style,
            InlineString = new InlineString(new Text(SanitizeXml(value))
            {
                Space = SpaceProcessingModeValues.Preserve,
            }),
        };

    private static Cell NumberCell(decimal value)
        => new()
        {
            DataType = CellValues.Number,
            StyleIndex = StyleNumber,
            CellValue = new CellValue(value.ToString(Invariant)),
        };

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            name = (char)('A' + remainder) + name;
            index = (index - 1) / 26;
        }
        return name;
    }

    /// <summary>XML 1.0 no admite caracteres de control: Excel rechazaría el archivo.</summary>
    private static string SanitizeXml(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20)
                builder.Append(ch);
        }
        return builder.ToString();
    }

    // ============================================================
    // PDF
    // ============================================================

    private const double PageWidth = 842;
    private const double PageHeight = 595;
    private const double Margin = 28;
    private const double RowHeight = 13;
    private const double FontSize = 7.5;

    private static readonly (string Title, double Width, Func<InventoryAssetExportRow, string?> Value)[] PdfColumns =
    [
        ("Etiqueta", 70, x => x.AssetTag),
        ("Nombre", 130, x => x.Name),
        ("Serie", 85, x => x.SerialNumber),
        ("Estado", 62, x => x.Status),
        ("Condición", 52, x => x.Condition),
        ("Categoría", 78, x => x.Category),
        ("Sucursal", 120, x => x.Branch),
        ("Asignado a", 130, x => x.AssignedTo),
        ("Costo", 59, x => x.PurchaseCost?.ToString("N2", Invariant)),
    ];

    public static byte[] BuildPdf(
        IReadOnlyList<InventoryAssetExportRow> rows,
        InventoryAssetExportFilters filters,
        DateTime generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(filters);

        const double headerBlock = 64;
        var firstRowY = PageHeight - Margin - headerBlock;
        var rowsPerPage = (int)Math.Floor((firstRowY - Margin - 18) / RowHeight);

        var pages = rows.Chunk(rowsPerPage).ToList();
        if (pages.Count == 0)
            pages.Add([]);

        var contents = new List<string>(pages.Count);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            contents.Add(BuildPdfPage(
                pages[pageIndex],
                rows.Count,
                filters,
                generatedAtUtc,
                pageIndex + 1,
                pages.Count,
                firstRowY));
        }

        return WritePdfDocument(contents);
    }

    private static string BuildPdfPage(
        IReadOnlyList<InventoryAssetExportRow> rows,
        int total,
        InventoryAssetExportFilters filters,
        DateTime generatedAtUtc,
        int pageNumber,
        int pageCount,
        double firstRowY)
    {
        var page = new StringBuilder();
        var tableWidth = PdfColumns.Sum(x => x.Width);

        // Encabezado.
        Text(page, "F2", 13, Margin, PageHeight - Margin - 12, "Activos de Inventario - CISA TI");
        Text(page, "F1", 8, Margin, PageHeight - Margin - 26,
            $"Generado (UTC): {generatedAtUtc.ToString("yyyy-MM-dd HH:mm", Invariant)}   ·   Total: {total} activo(s)");
        Text(page, "F1", 8, Margin, PageHeight - Margin - 38,
            Fit($"Filtros: {filters.Summary}", tableWidth, 8));

        // Cabecera de la tabla.
        var headerY = firstRowY + RowHeight;
        page.Append(Invariant, $"0.12 0.23 0.37 rg {Margin:0.##} {headerY - 4:0.##} {tableWidth:0.##} {RowHeight:0.##} re f\n");
        page.Append("1 1 1 rg\n");
        var x = Margin;
        foreach (var column in PdfColumns)
        {
            Text(page, "F2", FontSize, x + 3, headerY, Fit(column.Title, column.Width - 6, FontSize));
            x += column.Width;
        }
        page.Append("0 0 0 rg\n");

        // Filas.
        var y = firstRowY;
        for (var i = 0; i < rows.Count; i++)
        {
            if (i % 2 == 1)
            {
                page.Append(Invariant, $"0.94 0.95 0.97 rg {Margin:0.##} {y - 4:0.##} {tableWidth:0.##} {RowHeight:0.##} re f\n0 0 0 rg\n");
            }

            x = Margin;
            foreach (var column in PdfColumns)
            {
                Text(page, "F1", FontSize, x + 3, y, Fit(column.Value(rows[i]) ?? "-", column.Width - 6, FontSize));
                x += column.Width;
            }
            y -= RowHeight;
        }

        if (rows.Count == 0)
        {
            Text(page, "F1", 9, Margin + 3, firstRowY, "No hay activos que coincidan con los filtros seleccionados.");
        }

        // Pie.
        page.Append(Invariant, $"0.6 0.6 0.6 RG 0.5 w {Margin:0.##} {Margin + 8:0.##} m {Margin + tableWidth:0.##} {Margin + 8:0.##} l S\n");
        Text(page, "F1", 7, Margin, Margin - 2, "TitanMDM · Inventario ITAM (SQL Server cisa_ti_itam)");
        Text(page, "F1", 7, Margin + tableWidth - 60, Margin - 2, $"Página {pageNumber} de {pageCount}");

        return page.ToString();
    }

    private static void Text(StringBuilder page, string font, double size, double x, double y, string value)
        => page.Append(Invariant, $"BT /{font} {size:0.##} Tf {x:0.##} {y:0.##} Td ({EscapePdf(value)}) Tj ET\n");

    /// <summary>Recorta el texto al ancho de la columna, con elipsis si no cabe.</summary>
    private static string Fit(string value, double width, double size)
    {
        var clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (TextWidth(clean, size) <= width)
            return clean;

        var limit = width - TextWidth("…", size);
        var length = clean.Length;
        while (length > 1 && TextWidth(clean[..length], size) > limit)
            length--;

        return clean[..length].TrimEnd() + "…";
    }

    /// <summary>Ancho aproximado en Helvetica (métricas AFM promedio por tipo de carácter).</summary>
    private static double TextWidth(string value, double size)
    {
        double em = 0;
        foreach (var ch in value)
        {
            em += ch switch
            {
                ' ' => 0.278,
                'i' or 'j' or 'l' or 'I' or '.' or ',' or ':' or ';' or '\'' or '|' or '!' => 0.24,
                'f' or 't' or 'r' or '(' or ')' or '-' or '/' => 0.34,
                'm' or 'w' => 0.833,
                'M' or 'W' => 0.89,
                '…' => 1.0,
                _ when char.IsDigit(ch) => 0.556,
                _ when char.IsUpper(ch) => 0.70,
                _ => 0.556,
            };
        }
        return em * size;
    }

    private static string EscapePdf(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': builder.Append("\\\\"); break;
                case '(': builder.Append("\\("); break;
                case ')': builder.Append("\\)"); break;
                case '…': builder.Append((char)0x85); break; // WinAnsi: elipsis.
                default: builder.Append(ch <= 0xFF && ch >= 0x20 ? ch : '?'); break;
            }
        }
        return builder.ToString();
    }

    private static byte[] WritePdfDocument(IReadOnlyList<string> pageContents)
    {
        // 1 Catalog, 2 Pages, 3 Helvetica, 4 Helvetica-Bold, luego Page + Content por página.
        var bodies = new SortedDictionary<int, byte[]>();
        var latin1 = Encoding.Latin1;
        var kids = new List<int>();
        var next = 5;

        foreach (var content in pageContents)
        {
            var pageObject = next++;
            var contentObject = next++;
            kids.Add(pageObject);

            var stream = latin1.GetBytes(content);
            bodies[contentObject] = Concat(
                latin1.GetBytes($"<< /Length {stream.Length} >>\nstream\n"),
                stream,
                latin1.GetBytes("\nendstream"));

            bodies[pageObject] = latin1.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0} {PageHeight:0}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObject} 0 R >>");
        }

        bodies[1] = latin1.GetBytes("<< /Type /Catalog /Pages 2 0 R >>");
        bodies[2] = latin1.GetBytes(
            $"<< /Type /Pages /Count {kids.Count} /Kids [{string.Join(" ", kids.Select(k => $"{k} 0 R"))}] >>");
        bodies[3] = latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        bodies[4] = latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        using var output = new MemoryStream();
        void Write(string text)
        {
            var bytes = latin1.GetBytes(text);
            output.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.4\n");
        var offsets = new Dictionary<int, long>();
        foreach (var (number, body) in bodies)
        {
            offsets[number] = output.Position;
            Write($"{number} 0 obj\n");
            output.Write(body, 0, body.Length);
            Write("\nendobj\n");
        }

        var xref = output.Position;
        var size = bodies.Keys.Max() + 1;
        Write($"xref\n0 {size}\n0000000000 65535 f \n");
        for (var number = 1; number < size; number++)
        {
            Write($"{offsets[number]:D10} 00000 n \n");
        }
        Write($"trailer\n<< /Size {size} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");

        return output.ToArray();
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(x => x.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }
        return result;
    }

    private static string? FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTime.TryParse(value, Invariant, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? date.ToString("yyyy-MM-dd", Invariant)
            : value;
    }
}
