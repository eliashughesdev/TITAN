using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Domain.Entities.Inventory;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Inventory;

/// <summary>Entregas de Handheld con impresora Honeywell RP4 opcional.</summary>
public sealed class DeliveryPrinterTests
{
    private static ItamDbContext NewDb()
        => new(new DbContextOptionsBuilder<ItamDbContext>()
            .UseInMemoryDatabase($"itam-delivery-printer-{Guid.NewGuid()}")
            .Options);

    private static async Task<(Asset Handheld, Asset Printer)> SeedAsync(ItamDbContext db, string printerStatus = "disponible")
    {
        var rp4 = new ItamCategory { Id = "cat-rp4", Name = "RP4D" };
        var handheldCategory = new ItamCategory { Id = "cat-hh", Name = "Hand Held" };
        var handheld = new Asset { Id = "hh-1", AssetTag = "HH-001", Name = "Honeywell CK65", SerialNumber = "CK65-SN", CategoryId = handheldCategory.Id };
        var printer = new Asset { Id = "rp4-1", AssetTag = "RP4-001", Name = "Impresora portátil", SerialNumber = "RP4-SN-1", CategoryId = rp4.Id, Status = printerStatus };

        db.Categories.AddRange(rp4, handheldCategory);
        db.Assets.AddRange(handheld, printer);
        await db.SaveChangesAsync();
        return (handheld, printer);
    }

    private static Delivery NewDelivery(Asset handheld) => new()
    {
        AssetId = handheld.Id,
        AssignedToName = "Ana Pérez",
        EmployeeCode = "E-100",
    };

    [Fact]
    public async Task Sin_switch_entrega_solo_la_handheld_y_limpia_la_impresora()
    {
        await using var db = NewDb();
        var (handheld, printer) = await SeedAsync(db);
        var service = new InventoryService(db);

        var delivery = NewDelivery(handheld);
        delivery.PrinterId = printer.Id; // basura del formulario: se ignora
        var saved = await service.CreateDeliveryAsync(delivery, "tester", null);

        Assert.False(saved.IncludePrinter);
        Assert.Null(saved.PrinterId);
        Assert.Null(saved.PrinterSerialNumber);
        Assert.Equal("disponible", (await db.Assets.FindAsync(printer.Id))!.Status);
        Assert.Equal("asignado", (await db.Assets.FindAsync(handheld.Id))!.Status);
    }

    [Fact]
    public async Task Con_switch_asigna_la_rp4_y_la_libera_en_la_devolucion()
    {
        await using var db = NewDb();
        var (handheld, printer) = await SeedAsync(db);
        var service = new InventoryService(db);

        var delivery = NewDelivery(handheld);
        delivery.IncludePrinter = true;
        delivery.PrinterId = printer.Id;
        var saved = await service.CreateDeliveryAsync(delivery, "tester", null);

        Assert.True(saved.IncludePrinter);
        Assert.Equal(printer.Id, saved.PrinterId);
        Assert.Equal("RP4-SN-1", saved.PrinterSerialNumber);
        var assigned = (await db.Assets.FindAsync(printer.Id))!;
        Assert.Equal("asignado", assigned.Status);
        Assert.Equal("Ana Pérez", assigned.AssignedToName);
        Assert.Contains(db.AssetHistory, x => x.AssetId == printer.Id && x.Action == "delivery");

        await service.CreateReturnAsync(new Return { DeliveryId = saved.Id, Condition = "bueno" }, "tester");

        var released = (await db.Assets.FindAsync(printer.Id))!;
        Assert.Equal("disponible", released.Status);
        Assert.Null(released.AssignedToName);
        Assert.Contains(db.AssetHistory, x => x.AssetId == printer.Id && x.Action == "return");
    }

    [Fact]
    public async Task Con_switch_busca_la_impresora_por_serie()
    {
        await using var db = NewDb();
        var (handheld, printer) = await SeedAsync(db);
        var service = new InventoryService(db);

        var delivery = NewDelivery(handheld);
        delivery.IncludePrinter = true;
        delivery.PrinterSerialNumber = " RP4-SN-1 ";
        var saved = await service.CreateDeliveryAsync(delivery, "tester", null);

        Assert.Equal(printer.Id, saved.PrinterId);
    }

    [Fact]
    public async Task Con_switch_sin_impresora_es_rechazada()
    {
        await using var db = NewDb();
        var (handheld, _) = await SeedAsync(db);
        var service = new InventoryService(db);

        var delivery = NewDelivery(handheld);
        delivery.IncludePrinter = true;

        var error = await Assert.ThrowsAsync<InventoryValidationException>(
            () => service.CreateDeliveryAsync(delivery, "tester", null));

        Assert.Equal("INVENTORY_PRINTER_REQUIRED", error.Code);
        Assert.Empty(db.Deliveries);
        Assert.Equal("disponible", (await db.Assets.FindAsync(handheld.Id))!.Status);
    }

    [Fact]
    public async Task Rechaza_impresora_ya_asignada()
    {
        await using var db = NewDb();
        var (handheld, printer) = await SeedAsync(db, printerStatus: "asignado");
        var service = new InventoryService(db);

        var delivery = NewDelivery(handheld);
        delivery.IncludePrinter = true;
        delivery.PrinterId = printer.Id;

        var error = await Assert.ThrowsAsync<InventoryValidationException>(
            () => service.CreateDeliveryAsync(delivery, "tester", null));

        Assert.Equal("INVENTORY_PRINTER_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task Rechaza_activo_que_no_es_rp4_o_es_la_misma_handheld()
    {
        await using var db = NewDb();
        var (handheld, _) = await SeedAsync(db);
        var service = new InventoryService(db);

        var same = NewDelivery(handheld);
        same.IncludePrinter = true;
        same.PrinterId = handheld.Id;
        var sameError = await Assert.ThrowsAsync<InventoryValidationException>(
            () => service.CreateDeliveryAsync(same, "tester", null));
        Assert.Equal("INVENTORY_PRINTER_SAME_AS_ASSET", sameError.Code);

        db.Assets.Add(new Asset { Id = "lap-1", AssetTag = "LAP-001", Name = "Laptop Dell" });
        await db.SaveChangesAsync();

        var other = NewDelivery(handheld);
        other.IncludePrinter = true;
        other.PrinterId = "lap-1";
        var otherError = await Assert.ThrowsAsync<InventoryValidationException>(
            () => service.CreateDeliveryAsync(other, "tester", null));
        Assert.Equal("INVENTORY_PRINTER_NOT_RP4", otherError.Code);
    }

    [Theory]
    [InlineData("RP4D", "Impresora", null, true)]
    [InlineData(null, "Honeywell rp4 #12", null, true)]
    [InlineData(null, "Impresora", "RP4", true)]
    [InlineData("Impresora", "Zebra ZQ520", "ZQ520", false)]
    public void Identifica_impresoras_rp4(string? category, string name, string? model, bool expected)
    {
        var asset = new Asset { Name = name, HardwareModel = model };

        Assert.Equal(expected, InventoryService.IsRp4Printer(asset, category));
    }
}
