using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Domain.Entities.Inventory;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Inventory;

public sealed class InventoryCatalogServiceTests
{
    private static ItamDbContext NewDb()
        => new(new DbContextOptionsBuilder<ItamDbContext>()
            .UseInMemoryDatabase($"itam-catalogs-{Guid.NewGuid()}")
            .Options);

    [Theory]
    [InlineData(InventoryCatalogKinds.Branches)]
    [InlineData(InventoryCatalogKinds.Departments)]
    [InlineData(InventoryCatalogKinds.Locations)]
    [InlineData(InventoryCatalogKinds.Manufacturers)]
    [InlineData(InventoryCatalogKinds.Models)]
    [InlineData(InventoryCatalogKinds.Categories)]
    [InlineData(InventoryCatalogKinds.Suppliers)]
    public async Task Crea_y_lista_cada_catalogo(string kind)
    {
        await using var db = NewDb();
        var service = new InventoryService(db);

        var result = await service.SaveCatalogItemAsync(kind, null, new InventoryCatalogItemInput("  Honeywell  "), "tester");

        Assert.Equal(InventoryCatalogMutationStatus.Created, result.Status);
        var catalogs = await service.GetCatalogsAsync();
        var names = kind switch
        {
            InventoryCatalogKinds.Branches => catalogs.Branches.Select(x => x.Name),
            InventoryCatalogKinds.Departments => catalogs.Departments.Select(x => x.Name),
            InventoryCatalogKinds.Locations => catalogs.Locations.Select(x => x.Name),
            InventoryCatalogKinds.Manufacturers => catalogs.Manufacturers.Select(x => x.Name),
            InventoryCatalogKinds.Models => catalogs.Models.Select(x => x.Name),
            InventoryCatalogKinds.Categories => catalogs.Categories.Select(x => x.Name),
            _ => catalogs.Suppliers.Select(x => x.Name),
        };
        Assert.Equal(["Honeywell"], names);
    }

    [Fact]
    public async Task Rechaza_nombre_vacio_y_duplicado_sin_importar_mayusculas()
    {
        await using var db = NewDb();
        var service = new InventoryService(db);
        await service.SaveCatalogItemAsync(InventoryCatalogKinds.Manufacturers, null, new("Honeywell"), null);

        var empty = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Manufacturers, null, new(" "), null);
        var duplicate = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Manufacturers, null, new("HONEYWELL "), null);

        Assert.Equal(InventoryCatalogMutationStatus.Invalid, empty.Status);
        Assert.Equal(InventoryCatalogMutationStatus.Conflict, duplicate.Status);
    }

    [Fact]
    public async Task Modelo_valida_fabricante_y_permite_mismo_nombre_en_otro_fabricante()
    {
        await using var db = NewDb();
        db.Manufacturers.AddRange(new ItamManufacturer { Id = "m1", Name = "Honeywell" }, new ItamManufacturer { Id = "m2", Name = "Zebra" });
        await db.SaveChangesAsync();
        var service = new InventoryService(db);

        var unknown = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Models, null, new("CT40", ManufacturerId: "nope"), null);
        var first = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Models, null, new("CT40", ManufacturerId: "m1"), null);
        var other = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Models, null, new("CT40", ManufacturerId: "m2"), null);
        var repeated = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Models, null, new("ct40", ManufacturerId: "m1"), null);

        Assert.Equal(InventoryCatalogMutationStatus.Invalid, unknown.Status);
        Assert.Equal(InventoryCatalogMutationStatus.Created, first.Status);
        Assert.Equal(InventoryCatalogMutationStatus.Created, other.Status);
        Assert.Equal(InventoryCatalogMutationStatus.Conflict, repeated.Status);
    }

    [Fact]
    public async Task Edita_y_valida_depreciacion()
    {
        await using var db = NewDb();
        db.Categories.Add(new ItamCategory { Id = "c1", Name = "Laptop" });
        await db.SaveChangesAsync();
        var service = new InventoryService(db);

        var invalid = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Categories, "c1", new("Laptop", DepreciationYears: 99), null);
        var updated = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Categories, "c1", new("Laptops", Type: "hardware", DepreciationYears: 4), null);
        var missing = await service.SaveCatalogItemAsync(InventoryCatalogKinds.Categories, "zz", new("X"), null);

        Assert.Equal(InventoryCatalogMutationStatus.Invalid, invalid.Status);
        Assert.Equal(InventoryCatalogMutationStatus.Ok, updated.Status);
        Assert.Equal(InventoryCatalogMutationStatus.NotFound, missing.Status);
        var category = await db.Categories.SingleAsync();
        Assert.Equal("Laptops", category.Name);
        Assert.Equal(4, category.DepreciationYears);
        Assert.NotNull(category.UpdatedAt);
    }

    [Fact]
    public async Task No_elimina_catalogo_en_uso_y_hace_baja_logica_si_esta_libre()
    {
        await using var db = NewDb();
        db.Suppliers.AddRange(new ItamSupplier { Id = "s1", Name = "Usado" }, new ItamSupplier { Id = "s2", Name = "Libre" });
        db.Assets.Add(new Asset { Id = "a1", AssetTag = "TI-1", Name = "CT40", SupplierId = "s1" });
        await db.SaveChangesAsync();
        var service = new InventoryService(db);

        var inUse = await service.DeleteCatalogItemAsync(InventoryCatalogKinds.Suppliers, "s1", null);
        var free = await service.DeleteCatalogItemAsync(InventoryCatalogKinds.Suppliers, "s2", null);

        Assert.Equal(InventoryCatalogMutationStatus.Conflict, inUse.Status);
        Assert.Contains("1 activo", inUse.Error);
        Assert.Equal(InventoryCatalogMutationStatus.Ok, free.Status);
        Assert.True((await db.Suppliers.FindAsync("s2"))!.Deleted);
        Assert.Equal(["Usado"], (await service.GetCatalogsAsync()).Suppliers.Select(x => x.Name));
    }

    [Fact]
    public async Task Tipo_desconocido_es_invalido()
    {
        await using var db = NewDb();
        var service = new InventoryService(db);

        var result = await service.SaveCatalogItemAsync("planetas", null, new("Marte"), null);

        Assert.Equal(InventoryCatalogMutationStatus.Invalid, result.Status);
    }
}
