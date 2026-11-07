using System.Globalization;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities.Inventory;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Inventory;

/// <summary>
/// GET /api/itam/deliveries: filtros superiores de Entregas y registros
/// legados (solo "returned") frente a returned_handheld / returned_rp4d.
/// </summary>
public sealed class DeliveryReturnFilterTests
{
    private static readonly string Now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    private static readonly string Old = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    private static readonly string PastDue = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static readonly string Future = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<InventoryService> SeedAsync()
    {
        var db = new ItamDbContext(new DbContextOptionsBuilder<ItamDbContext>()
            .UseInMemoryDatabase($"itam-delivery-filters-{Guid.NewGuid()}")
            .Options);

        db.Deliveries.AddRange(
            // Legadas cerradas: solo "returned" = 1, aunque el retorno ya venció.
            new Delivery { Id = "legacy", DeliveryDate = Old, ExpectedReturn = PastDue, Returned = true },
            new Delivery { Id = "legacy-rp4d", DeliveryDate = Old, ExpectedReturn = PastDue, Returned = true, IncludeRp4d = true },
            // Abiertas.
            new Delivery { Id = "today-open", DeliveryDate = Now, ExpectedReturn = Future, Returned = false },
            new Delivery { Id = "overdue-open", DeliveryDate = Old, ExpectedReturn = PastDue, Returned = false },
            new Delivery { Id = "rp4d-pending", DeliveryDate = Old, ExpectedReturn = PastDue, Returned = false, ReturnedHandheld = true, IncludeRp4d = true },
            // Nuevas cerradas.
            new Delivery { Id = "closed", DeliveryDate = Now, ExpectedReturn = PastDue, Returned = true, ReturnedHandheld = true, IncludeRp4d = true, ReturnedRp4d = true },
            new Delivery { Id = "deleted", DeliveryDate = Now, ExpectedReturn = PastDue, Returned = false, Deleted = true });
        await db.SaveChangesAsync();

        return new InventoryService(db);
    }

    private static async Task<string[]> IdsAsync(InventoryService service, string? period, bool? returned = null)
        => (await service.GetDeliveriesAsync(null, returned, period))
            .Select(x => x.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public async Task Legadas_con_returned_se_muestran_devueltas()
    {
        var service = await SeedAsync();

        var rows = await service.GetDeliveriesAsync();

        var legacy = Assert.Single(rows, x => x.Id == "legacy");
        Assert.True(legacy.ReturnedHandheld);
        Assert.False(legacy.ReturnedRp4d);
        var legacyRp4d = Assert.Single(rows, x => x.Id == "legacy-rp4d");
        Assert.True(legacyRp4d.ReturnedHandheld);
        Assert.True(legacyRp4d.ReturnedRp4d);
        Assert.True((await service.GetDeliveryAsync("legacy-rp4d"))!.ReturnedRp4d);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("all")]
    public async Task Todas_excluye_eliminadas(string? period)
        => Assert.Equal(
            ["closed", "legacy", "legacy-rp4d", "overdue-open", "rp4d-pending", "today-open"],
            await IdsAsync(await SeedAsync(), period));

    [Fact]
    public async Task Hoy_filtra_por_fecha_de_entrega()
        => Assert.Equal(["closed", "today-open"], await IdsAsync(await SeedAsync(), "today"));

    [Theory]
    [InlineData("pending-returns")]
    [InlineData("pending")]
    public async Task Pendientes_hand_held_o_rp4d_sin_devolver(string period)
        => Assert.Equal(["overdue-open", "rp4d-pending", "today-open"], await IdsAsync(await SeedAsync(), period));

    [Theory]
    [InlineData("expired")]
    [InlineData("overdue")]
    public async Task Vencidas_solo_con_pendientes_y_retorno_pasado(string period)
        => Assert.Equal(["overdue-open", "rp4d-pending"], await IdsAsync(await SeedAsync(), period));

    [Fact]
    public async Task Parametro_returned_usa_el_estado_individual()
    {
        var service = await SeedAsync();

        Assert.Equal(["overdue-open", "rp4d-pending", "today-open"], await IdsAsync(service, null, returned: false));
        Assert.Equal(["closed", "legacy", "legacy-rp4d"], await IdsAsync(service, null, returned: true));
    }

    [Fact]
    public async Task Devolver_hand_held_de_entrega_legada_cerrada_es_rechazado()
    {
        var service = await SeedAsync();

        var error = await Assert.ThrowsAsync<TitanMDM.Application.Contracts.Persistence.InventoryValidationException>(
            () => service.ReturnHandheldAsync("legacy", null, "tester"));

        Assert.Equal("INVENTORY_HANDHELD_ALREADY_RETURNED", error.Code);
    }
}
