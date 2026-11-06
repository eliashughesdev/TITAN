using System.Text;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using TitanMDM.Api.Controllers;
using TitanMDM.Domain.Entities.Inventory;
using TitanMDM.Infrastructure.Reports;

namespace TitanMDM.IntegrationTests.Inventory;

public sealed class InventoryAssetExportTests
{
    private static readonly DateTime Generated = new(2026, 11, 6, 15, 30, 0, DateTimeKind.Utc);

    private static InventoryAssetExportRow Row(string tag, string name, decimal? cost = null)
        => new(tag, name, "SN-" + tag, "disponible", "bueno", "Hand Held", "Honeywell", "CK65",
            "Santo Domingo", "TI", "Ana Pérez", null, "Android 11", null, "2025-01-15T00:00:00Z", null, cost);

    private static InventoryAssetExportFilters Filters(params (string Key, string Value)[] items)
        => new(items.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).ToList());

    [Fact]
    public void Excel_contiene_encabezado_filas_y_hoja_de_filtros()
    {
        var bytes = InventoryAssetExportBuilder.BuildExcel(
            [Row("HH-001", "CK65 almacén", 1500m), Row("HH-002", "CK65 patio")],
            Filters(("Estado", "disponible"), ("Sucursal", "Santo Domingo")),
            Generated);

        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var workbook = document.WorkbookPart!;
        var sheets = workbook.Workbook.Sheets!.Elements<Sheet>().ToList();
        Assert.Equal(["Activos", "Filtros"], sheets.Select(x => x.Name!.Value));

        var assets = ((WorksheetPart)workbook.GetPartById(sheets[0].Id!)).Worksheet;
        var rows = assets.Descendants<Row>().ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal("Etiqueta", rows[0].Elements<Cell>().First().InnerText);
        Assert.Equal("HH-001", rows[1].Elements<Cell>().First().InnerText);
        Assert.Equal("CK65 almacén", rows[1].Elements<Cell>().ElementAt(1).InnerText);
        Assert.Equal("1500", rows[1].Elements<Cell>().Last().CellValue!.Text);
        Assert.Equal("A1:Q3", assets.Descendants<AutoFilter>().Single().Reference!.Value);

        var filters = ((WorksheetPart)workbook.GetPartById(sheets[1].Id!)).Worksheet;
        var text = string.Join("|", filters.Descendants<Cell>().Select(x => x.InnerText));
        Assert.Contains("Estado|disponible", text);
        Assert.Contains("Sucursal|Santo Domingo", text);
        Assert.Contains("Total de activos|2", text);
    }

    [Fact]
    public void Pdf_es_valido_y_pagina_los_resultados()
    {
        var rows = Enumerable.Range(1, 80).Select(i => Row($"HH-{i:000}", $"Equipo (n.º {i})")).ToList();

        var bytes = InventoryAssetExportBuilder.BuildPdf(rows, Filters(("Búsqueda", "ck65")), Generated);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF", text);
        Assert.Contains("/Count 3", text); // 80 filas, 35 por página
        Assert.Contains("Filtros: Búsqueda: ck65", text);
        Assert.Contains("Equipo \\(n.º 1\\)", text); // paréntesis escapados
        Assert.Contains("Página 3 de 3", text);

        // xref coherente: cada desplazamiento apunta a "n 0 obj".
        var xref = text.LastIndexOf("xref\n", StringComparison.Ordinal);
        var entries = text[xref..].Split('\n').Skip(3).TakeWhile(x => x.EndsWith(" n ", StringComparison.Ordinal)).ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i][..10], System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith($"{i + 1} 0 obj", text[offset..]);
        }
    }

    [Fact]
    public void Pdf_sin_resultados_indica_que_no_hay_activos()
    {
        var text = Encoding.Latin1.GetString(
            InventoryAssetExportBuilder.BuildPdf([], Filters(), Generated));

        Assert.Contains("/Count 1", text);
        Assert.Contains("No hay activos que coincidan", text);
        Assert.Contains("Sin filtros", text);
    }

    [Theory]
    [InlineData("ck65 ana", true)]
    [InlineData("almacen", true)]   // sin tilde encuentra "almacén"
    [InlineData("SANTO domingo", true)] // nombre de sucursal resuelto
    [InlineData("ck65 pedro", false)]
    [InlineData("   ", true)]
    public void Busqueda_igual_que_la_tabla(string search, bool expected)
    {
        var asset = new Asset
        {
            AssetTag = "HH-001",
            Name = "CK65 almacén",
            AssignedToName = "Ana Pérez",
        };

        Assert.Equal(expected, InventoryAssetExportController.MatchesSearch(asset, search, "Hand Held", "Santo Domingo"));
    }
}
