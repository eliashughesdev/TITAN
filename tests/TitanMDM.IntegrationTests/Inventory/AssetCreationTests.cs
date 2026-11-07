using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Api.Controllers;
using TitanMDM.Domain.Entities.Inventory;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Inventory;

/// <summary>POST /api/itam/assets: alta de activo sin error 500.</summary>
public sealed class AssetCreationTests
{
    private static ItamDbContext NewDb()
        => new(new DbContextOptionsBuilder<ItamDbContext>()
            .UseInMemoryDatabase($"itam-assets-{Guid.NewGuid()}")
            .Options);

    private static ItamController NewController(ItamDbContext db)
        => new(new InventoryService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "tester")], "test")),
                },
            },
        };

    [Theory]
    [InlineData(typeof(AssetHistory), nameof(AssetHistory.AssetId), "FK_hist_asset")]
    [InlineData(typeof(Delivery), nameof(Delivery.AssetId), "FK_deliv_asset")]
    [InlineData(typeof(Delivery), nameof(Delivery.Rp4dId), "FK_deliv_rp4d")]
    public void Modelo_declara_las_fk_reales_hacia_assets(Type entity, string property, string constraint)
    {
        using var db = NewDb();

        // Con la FK declarada, EF inserta el activo antes que su historial
        // (sin ella, SQL Server rechazaba el INSERT por FK_hist_asset → 500).
        var foreignKey = Assert.Single(
            db.Model.FindEntityType(entity)!.GetForeignKeys(),
            x => x.Properties.Single().Name == property);

        Assert.Equal(typeof(Asset), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(constraint, foreignKey.GetConstraintName());
    }

    [Fact]
    public async Task Alta_con_campos_opcionales_vacios_responde_201()
    {
        await using var db = NewDb();
        var controller = NewController(db);

        var result = await controller.CreateAsset(
            new Asset
            {
                AssetTag = " hh-900 ",
                Name = " CK65 nuevo ",
                PurchaseDate = null,
                WarrantyEnd = null,
                PurchaseCost = null,
                DepreciationYears = null,
                Hostname = null,
                MacAddress = null,
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var asset = Assert.IsType<Asset>(created.Value);
        Assert.Equal("HH-900", asset.AssetTag);
        Assert.Equal("CK65 nuevo", asset.Name);
        Assert.Equal("disponible", asset.Status);
        Assert.Contains(db.AssetHistory, x => x.AssetId == asset.Id && x.Action == "created");
    }

    [Fact]
    public void Etiqueta_vacia_pasa_validacion_del_modelo()
    {
        var asset = new Asset { Name = "Laptop" };
        var results = new List<ValidationResult>();

        Assert.True(
            Validator.TryValidateObject(asset, new ValidationContext(asset), results, validateAllProperties: true),
            string.Join("; ", results.Select(result => result.ErrorMessage)));
    }

    [Fact]
    public async Task Etiqueta_vacia_se_genera()
    {
        await using var db = NewDb();

        var result = await NewController(db).CreateAsset(new Asset { Name = "Laptop" }, CancellationToken.None);

        var asset = Assert.IsType<Asset>(Assert.IsType<CreatedAtActionResult>(result).Value);
        Assert.False(string.IsNullOrWhiteSpace(asset.AssetTag));
    }

    [Fact]
    public async Task Etiqueta_duplicada_responde_409_y_nombre_vacio_400()
    {
        await using var db = NewDb();
        var controller = NewController(db);
        await controller.CreateAsset(new Asset { AssetTag = "HH-901", Name = "CK65" }, CancellationToken.None);

        var duplicate = await controller.CreateAsset(new Asset { AssetTag = "hh-901", Name = "Otro" }, CancellationToken.None);
        var noName = await controller.CreateAsset(new Asset { AssetTag = "HH-902", Name = "  " }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(duplicate);
        Assert.IsType<BadRequestObjectResult>(noName);
        Assert.Single(db.Assets);
    }
}
