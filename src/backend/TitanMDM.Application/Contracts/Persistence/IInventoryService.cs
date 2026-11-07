using TitanMDM.Domain.Entities.Inventory;

namespace TitanMDM.Application.Contracts.Persistence;

public sealed record InventoryNameCount(string Name, int Count);

public sealed record InventoryDashboardDto(
    int TotalAssets,
    int AssignedAssets,
    int AvailableAssets,
    int HandHeldAssets,
    int DamagedAssets,
    int RetiredAssets,
    int TotalEmployees,
    int ActiveDeliveries,
    int ReturnedDeliveries,
    int PendingReceptions,
    int WarrantiesExpiringSoon,
    decimal TotalPurchaseCost,
    decimal AccumulatedDepreciation,
    decimal BookValue,
    int AtRiskAssets,
    int TotalBranches,
    double RotationRate,
    IReadOnlyList<InventoryNameCount> ByStatus,
    IReadOnlyList<InventoryNameCount> ByCondition,
    IReadOnlyList<InventoryNameCount> ByCategory,
    IReadOnlyList<InventoryNameCount> ByBranch,
    IReadOnlyList<InventoryNameCount> ByManufacturer,
    IReadOnlyList<InventoryNameCount> ByDepartment,
    IReadOnlyList<InventoryNameCount> MonthlyAssignments);

public sealed record InventoryReconciliationDto(
    string SessionId,
    int SystemCount,
    int CountedCount,
    IReadOnlyList<Asset> Found,
    IReadOnlyList<Asset> Missing,
    IReadOnlyList<InventoryCount> Extra);

public sealed record InventoryAssetDepreciationDto(
    string AssetId,
    string AssetTag,
    string Name,
    decimal PurchaseCost,
    int? DepreciationYears,
    double YearsElapsed,
    decimal AnnualDepreciation,
    decimal AccumulatedDepreciation,
    decimal BookValue);

public sealed record InventoryCatalogDto(
    IReadOnlyList<ItamCategory> Categories,
    IReadOnlyList<ItamBranch> Branches,
    IReadOnlyList<ItamDepartment> Departments,
    IReadOnlyList<ItamManufacturer> Manufacturers,
    IReadOnlyList<ItamSupplier> Suppliers,
    IReadOnlyList<ItamAssetModel> Models,
    IReadOnlyList<ItamLocation> Locations);

public interface IInventoryService
{
    Task<List<Asset>> GetAssetsAsync(
        string? query = null,
        string? status = null,
        string? categoryId = null,
        string? branchId = null,
        string? departmentId = null,
        string? warranty = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    Task<Asset?> GetAssetAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<Asset?> GetAssetByTagAsync(
        string assetTag,
        CancellationToken cancellationToken = default);

    Task<List<Asset>> SearchAssetsAsync(
        string? query,
        int limit = 15,
        CancellationToken cancellationToken = default);

    Task<Asset> CreateAssetAsync(
        Asset asset,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Asset?> UpdateAssetAsync(
        Asset asset,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Asset?> LinkDeviceAsync(
        string id,
        string? deviceId,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAssetAsync(
        string id,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<List<Employee>> GetEmployeesAsync(
        string? query = null,
        string? departmentId = null,
        int page = 1,
        int pageSize = 200,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Employee?> GetEmployeeAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<Employee> CreateEmployeeAsync(
        Employee employee,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Employee?> UpdateEmployeeAsync(
        Employee employee,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteEmployeeAsync(
        string id,
        string? userName,
        CancellationToken cancellationToken = default);

    /// <param name="period">
    /// all | today | pending (pending-returns) | overdue (expired), en la zona
    /// horaria del negocio. Pendiente = Hand Held sin devolver o RP4D incluida
    /// sin devolver; los registros legados con returned = 1 cuentan como devueltos.
    /// </param>
    Task<List<Delivery>> GetDeliveriesAsync(
        string? query = null,
        bool? returned = null,
        string? period = null,
        CancellationToken cancellationToken = default);

    Task<Delivery?> GetDeliveryAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<Delivery> CreateDeliveryAsync(
        Delivery delivery,
        string? userName,
        string? userEmail,
        CancellationToken cancellationToken = default);

    /// <param name="period">all | today (zona horaria del negocio).</param>
    Task<List<Return>> GetReturnsAsync(
        string? query = null,
        string? period = null,
        CancellationToken cancellationToken = default);

    Task<Return?> GetReturnAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<Return> CreateReturnAsync(
        Return item,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Delivery> ReturnHandheldAsync(
        string deliveryId,
        Return? item,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Delivery> ReturnRp4dAsync(
        string deliveryId,
        Return? item,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<List<Reception>> GetReceptionsAsync(
        string? query = null,
        CancellationToken cancellationToken = default);

    Task<Reception?> GetReceptionAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<Reception> CreateReceptionAsync(
        Reception reception,
        string? userName,
        string? userEmail,
        CancellationToken cancellationToken = default);

    Task<List<InventorySession>> GetSessionsAsync(
        CancellationToken cancellationToken = default);

    Task<InventorySession?> GetSessionAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<InventorySession> CreateSessionAsync(
        InventorySession session,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<InventorySession?> CloseSessionAsync(
        string id,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<List<InventoryCount>> GetCountsAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<InventoryCount> SaveCountAsync(
        InventoryCount count,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<InventoryReconciliationDto> ReconcileAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<List<InventoryAssetDepreciationDto>> GetDepreciationReportAsync(
        CancellationToken cancellationToken = default);

    Task<List<AssetHistory>> GetHistoryAsync(
        string? assetId = null,
        string? query = null,
        int limit = 300,
        CancellationToken cancellationToken = default);

    Task<InventoryDashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<InventoryCatalogDto> GetCatalogsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea (id nulo) o actualiza un elemento de catalogo ITAM.
    /// <paramref name="kind"/>: categories, branches, departments,
    /// manufacturers, suppliers, models, locations.
    /// </summary>
    Task<InventoryCatalogMutationResult> SaveCatalogItemAsync(
        string kind,
        string? id,
        InventoryCatalogItemInput input,
        string? userName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja logica. Se rechaza (Conflict) si el elemento sigue en uso.
    /// </summary>
    Task<InventoryCatalogMutationResult> DeleteCatalogItemAsync(
        string kind,
        string id,
        string? userName,
        CancellationToken cancellationToken = default);
}

public static class InventoryCatalogKinds
{
    public const string Categories = "categories";
    public const string Branches = "branches";
    public const string Departments = "departments";
    public const string Manufacturers = "manufacturers";
    public const string Suppliers = "suppliers";
    public const string Models = "models";
    public const string Locations = "locations";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Categories, Branches, Departments, Manufacturers, Suppliers, Models, Locations,
    };
}

/// <summary>
/// Campos editables de cualquier catalogo; cada tipo usa solo los suyos.
/// </summary>
public sealed record InventoryCatalogItemInput(
    string? Name,
    string? Code = null,
    string? City = null,
    string? Address = null,
    string? Phone = null,
    string? Manager = null,
    string? Floor = null,
    string? Area = null,
    string? BranchId = null,
    string? Website = null,
    string? SupportPhone = null,
    string? ModelNumber = null,
    string? ManufacturerId = null,
    string? CategoryId = null,
    string? Type = null,
    int? DepreciationYears = null,
    string? Rnc = null,
    string? ContactName = null,
    string? Email = null);

public enum InventoryCatalogMutationStatus
{
    Ok,
    Created,
    NotFound,
    Invalid,
    Conflict,
}

public sealed record InventoryCatalogMutationResult(
    InventoryCatalogMutationStatus Status,
    object? Item = null,
    string? Error = null);
