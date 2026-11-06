using System.Text;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Api.Controllers;
using TitanMDM.Domain.Entities.Inventory;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Inventory;

/// <summary>Endpoints /api/inventory/assets/export/{excel|pdf} con filtros reales.</summary>
public sealed class InventoryAssetExportControllerTests
{
    private static async Task<InventoryAssetExportController> NewControllerAsync()
    {
        var db = new ItamDbContext(new DbContextOptionsBuilder<ItamDbContext>()
            .UseInMemoryDatabase($"itam-export-{Guid.NewGuid()}")
            .Options);

        db.Categories.AddRange(
            new ItamCategory { Id = "cat-hh", Name = "Hand Held" },
            new ItamCategory { Id = "cat-rp4", Name = "RP4D" });
        db.Branches.AddRange(
            new ItamBranch { Id = "br-sd", Name = "Santo Domingo" },
            new ItamBranch { Id = "br-sti", Name = "Santiago" });
        db.Assets.AddRange(
            new Asset { Id = "1", AssetTag = "HH-001", Name = "CK65 almacén", CategoryId = "cat-hh", BranchId = "br-sd", Status = "disponible" },
            new Asset { Id = "2", AssetTag = "HH-002", Name = "CK65 patio", CategoryId = "cat-hh", BranchId = "br-sti", Status = "disponible" },
            new Asset { Id = "3", AssetTag = "HH-003", Name = "CK65 oficina", CategoryId = "cat-hh", BranchId = "br-sd", Status = "asignado" },
            new Asset { Id = "4", AssetTag = "RP4-001", Name = "Impresora", CategoryId = "cat-rp4", BranchId = "br-sd", Status = "disponible" },
            new Asset { Id = "5", AssetTag = "HH-005", Name = "CK65 baja", CategoryId = "cat-hh", BranchId = "br-sd", Status = "disponible", Deleted = true });
        await db.SaveChangesAsync();

        return new InventoryAssetExportController(new InventoryService(db));
    }

    private static List<string> ExcelTags(byte[] bytes)
    {
        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var workbook = document.WorkbookPart!;
        var sheet = workbook.Workbook.Sheets!.Elements<Sheet>().First();
        var worksheet = ((WorksheetPart)workbook.GetPartById(sheet.Id!)).Worksheet;
        return worksheet.Descendants<Row>().Skip(1).Select(r => r.Elements<Cell>().First().InnerText).ToList();
    }

    [Fact]
    public async Task Excel_respeta_tipo_sucursal_estado_y_busqueda()
    {
        var controller = await NewControllerAsync();

        var result = await controller.ExportExcel(
            new InventoryAssetExportQuery { CategoryId = "cat-hh", BranchId = "br-sd", Status = "disponible", Q = "almacen" },
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Matches(@"^activos-inventario-\d{8}-\d{4}\.xlsx$", file.FileDownloadName);
        Assert.Equal(["HH-001"], ExcelTags(file.FileContents));
    }

    [Fact]
    public async Task Excel_sin_filtros_excluye_activos_dados_de_baja()
    {
        var controller = await NewControllerAsync();

        var file = Assert.IsType<FileContentResult>(
            await controller.ExportExcel(new InventoryAssetExportQuery(), CancellationToken.None));

        Assert.Equal(["HH-001", "HH-002", "HH-003", "RP4-001"], ExcelTags(file.FileContents));
    }

    [Fact]
    public async Task Pdf_respeta_filtros_y_los_muestra_por_nombre()
    {
        var controller = await NewControllerAsync();

        var result = await controller.ExportPdf(
            new InventoryAssetExportQuery { CategoryId = "cat-hh", BranchId = "br-sd" },
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        var text = Encoding.Latin1.GetString(file.FileContents);
        Assert.Contains(@"Total: 2 activo\(s\)", text); // paréntesis escapados en PDF
        Assert.Contains("Tipo de equipo: Hand Held", text);
        Assert.Contains("Sucursal: Santo Domingo", text);
        Assert.Contains("(HH-001)", text);
        Assert.Contains("(HH-003)", text);
        Assert.DoesNotContain("(HH-002)", text);
        Assert.DoesNotContain("(RP4-001)", text);
    }

    [Fact]
    public void Rutas_publicadas_bajo_api_inventory_assets_export()
    {
        var type = typeof(InventoryAssetExportController);
        var route = Assert.Single(type.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>());
        Assert.Equal("api/inventory/assets/export", route.Template);

        string Get(string method) => Assert.Single(
            type.GetMethod(method)!.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>()).Template!;

        Assert.Equal("excel", Get(nameof(InventoryAssetExportController.ExportExcel)));
        Assert.Equal("pdf", Get(nameof(InventoryAssetExportController.ExportPdf)));
    }
}
