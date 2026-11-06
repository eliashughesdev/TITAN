using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;
using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities.Inventory;
using TitanMDM.Infrastructure.Reports;

namespace TitanMDM.Api.Controllers;

/// <summary>
/// Exportación del listado de Activos (SQL Server cisa_ti_itam) a Excel y PDF.
/// Recibe los mismos filtros que GET /api/itam/assets, de modo que el archivo
/// contiene exactamente lo que se ve en pantalla.
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory/assets/export")]
public sealed class InventoryAssetExportController : ControllerBase
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IInventoryService _inventory;

    public InventoryAssetExportController(IInventoryService inventory)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

    [HttpGet("excel")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.ReportsExport,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] InventoryAssetExportQuery query,
        CancellationToken cancellationToken)
    {
        var (rows, filters, now) = await LoadAsync(query, cancellationToken);
        var bytes = InventoryAssetExportBuilder.BuildExcel(rows, filters, now);
        return File(bytes, ExcelContentType, FileName(now, "xlsx"));
    }

    [HttpGet("pdf")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.ReportsExport,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] InventoryAssetExportQuery query,
        CancellationToken cancellationToken)
    {
        var (rows, filters, now) = await LoadAsync(query, cancellationToken);
        var bytes = InventoryAssetExportBuilder.BuildPdf(rows, filters, now);
        return File(bytes, "application/pdf", FileName(now, "pdf"));
    }

    private async Task<(IReadOnlyList<InventoryAssetExportRow> Rows, InventoryAssetExportFilters Filters, DateTime Now)> LoadAsync(
        InventoryAssetExportQuery query,
        CancellationToken cancellationToken)
    {
        // La búsqueda de texto se aplica abajo con la misma regla que el
        // buscador de la tabla (sin tildes, todos los términos).
        var assets = await _inventory.GetAssetsAsync(
            null,
            Clean(query.Status),
            Clean(query.CategoryId),
            Clean(query.BranchId),
            Clean(query.DepartmentId),
            Clean(query.Warranty),
            includeDeleted: false,
            cancellationToken);

        var catalogs = await _inventory.GetCatalogsAsync(cancellationToken);
        var categories = Names(catalogs.Categories);
        var branches = Names(catalogs.Branches);
        var departments = Names(catalogs.Departments);
        var manufacturers = Names(catalogs.Manufacturers);
        var models = Names(catalogs.Models);

        var search = Clean(query.Q);
        if (search is not null)
        {
            assets = assets
                .Where(asset => MatchesSearch(
                    asset,
                    search,
                    Lookup(categories, asset.CategoryId),
                    Lookup(branches, asset.BranchId)))
                .ToList();
        }

        var rows = assets
            .Select(asset => new InventoryAssetExportRow(
                asset.AssetTag,
                asset.Name,
                asset.SerialNumber,
                asset.Status,
                asset.Condition,
                Lookup(categories, asset.CategoryId),
                Lookup(manufacturers, asset.ManufacturerId) ?? asset.HardwareManufacturer,
                Lookup(models, asset.ModelId) ?? asset.HardwareModel,
                Lookup(branches, asset.BranchId),
                Lookup(departments, asset.DepartmentId),
                asset.AssignedToName,
                asset.DeviceId,
                asset.OsName,
                asset.LastUser,
                asset.PurchaseDate,
                asset.WarrantyEnd,
                asset.PurchaseCost))
            .ToList();

        var applied = new List<KeyValuePair<string, string>>();
        void AddFilter(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                applied.Add(new(label, value));
        }

        AddFilter("Búsqueda", Clean(query.Q));
        AddFilter("Estado", Clean(query.Status));
        AddFilter("Tipo de equipo", Lookup(categories, Clean(query.CategoryId)) ?? Clean(query.CategoryId));
        AddFilter("Sucursal", Lookup(branches, Clean(query.BranchId)) ?? Clean(query.BranchId));
        AddFilter("Departamento", Lookup(departments, Clean(query.DepartmentId)) ?? Clean(query.DepartmentId));
        AddFilter("Garantía", WarrantyLabel(Clean(query.Warranty)));

        return (rows, new InventoryAssetExportFilters(applied), DateTime.UtcNow);
    }

    private static readonly System.Reflection.PropertyInfo[] SearchableAssetProperties =
        typeof(Asset)
            .GetProperties()
            .Where(x => x.PropertyType == typeof(string) ||
                        x.PropertyType.IsValueType)
            .ToArray();

    /// <summary>
    /// Misma regla que InventoryDataTable: minúsculas, sin tildes y cada
    /// término debe aparecer en alguno de los valores de la fila.
    /// </summary>
    public static bool MatchesSearch(Asset asset, string search, params string?[] extraValues)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var terms = Normalize(search)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return true;

        var values = SearchableAssetProperties
            .Select(x => Convert.ToString(x.GetValue(asset), CultureInfo.InvariantCulture))
            .Concat(extraValues);
        var haystack = Normalize(string.Join(' ', values.Where(x => !string.IsNullOrEmpty(x))));

        return terms.All(term => haystack.Contains(term, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(ch));
        }
        return builder.ToString().Trim();
    }

    private static Dictionary<string, string> Names<T>(IEnumerable<T> items)
        where T : IItamCatalogEntity
        => items
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

    private static string? Lookup(Dictionary<string, string> names, string? id)
        => id is not null && names.TryGetValue(id, out var name) ? name : null;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? WarrantyLabel(string? warranty)
        => warranty?.ToLowerInvariant() switch
        {
            null => null,
            "active" or "vigente" => "Vigente",
            "expiring" or "por_vencer" => "Por vencer (90 días)",
            "expired" or "vencida" => "Vencida",
            _ => warranty,
        };

    private static string FileName(DateTime now, string extension)
        => $"activos-inventario-{now:yyyyMMdd-HHmm}.{extension}";
}

/// <summary>Filtros de la pantalla de Activos (mismos nombres que /api/itam/assets).</summary>
public sealed class InventoryAssetExportQuery
{
    public string? Q { get; init; }

    public string? Status { get; init; }

    public string? CategoryId { get; init; }

    public string? BranchId { get; init; }

    public string? DepartmentId { get; init; }

    public string? Warranty { get; init; }
}
