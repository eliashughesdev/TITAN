using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.Security;
using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities.Inventory;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/itam")]
public sealed class ItamController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public ItamController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    // ============================================================
    // DASHBOARD
    // ============================================================

    [HttpGet("dashboard")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.DashboardView,
        PermissionCodes.Inventory.ReportsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetDashboard(
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetDashboardAsync(cancellationToken));

    /*
     * Los catalogos alimentan los selectores de activos, colaboradores,
     * recepciones y sesiones: cualquier permiso que abra esas pantallas
     * debe poder leerlos.
     */
    [HttpGet("catalogs")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.CatalogsView,
        PermissionCodes.Inventory.CatalogsManage,
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.AssetsManage,
        PermissionCodes.Inventory.EmployeesView,
        PermissionCodes.Inventory.ReceptionsView,
        PermissionCodes.Inventory.SessionsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetCatalogs(
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetCatalogsAsync(cancellationToken));

    [HttpPost("catalogs/{kind}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.CatalogsManage,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> CreateCatalogItem(
        string kind,
        [FromBody] InventoryCatalogItemInput input,
        CancellationToken cancellationToken)
        => CatalogResult(await _inventory.SaveCatalogItemAsync(
            kind, null, input, UserName(), cancellationToken));

    [HttpPut("catalogs/{kind}/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.CatalogsManage,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> UpdateCatalogItem(
        string kind,
        string id,
        [FromBody] InventoryCatalogItemInput input,
        CancellationToken cancellationToken)
        => CatalogResult(await _inventory.SaveCatalogItemAsync(
            kind, id, input, UserName(), cancellationToken));

    [HttpDelete("catalogs/{kind}/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.CatalogsManage,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> DeleteCatalogItem(
        string kind,
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _inventory.DeleteCatalogItemAsync(
            kind, id, UserName(), cancellationToken);
        return result.Status == InventoryCatalogMutationStatus.Ok
            ? NoContent()
            : CatalogResult(result);
    }

    private IActionResult CatalogResult(InventoryCatalogMutationResult result)
        => result.Status switch
        {
            InventoryCatalogMutationStatus.Created => StatusCode(StatusCodes.Status201Created, result.Item),
            InventoryCatalogMutationStatus.Ok => Ok(result.Item),
            InventoryCatalogMutationStatus.NotFound => NotFound(new { message = result.Error }),
            InventoryCatalogMutationStatus.Conflict => Conflict(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error }),
        };

    // ============================================================
    // ASSETS
    // ============================================================

    [HttpGet("assets")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.ReportsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetAssets(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? categoryId,
        [FromQuery] string? branchId,
        [FromQuery] string? departmentId,
        [FromQuery] string? warranty,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetAssetsAsync(
            q,
            status,
            categoryId,
            branchId,
            departmentId,
            warranty,
            includeDeleted,
            cancellationToken));

    [HttpGet("assets/search")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> SearchAssets(
        [FromQuery] string? q,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
        => Ok(await _inventory.SearchAssetsAsync(
            q,
            limit <= 0 ? 15 : limit,
            cancellationToken));

    /*
     * Motor de depreciacion (linea recta): costo, depreciacion
     * anual/acumulada y valor en libros por activo. Lo consume
     * la exportacion PDF/Excel de reportes ITAM.
     */
    [HttpGet("assets/depreciation")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.ReportsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetDepreciation(
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetDepreciationReportAsync(
            cancellationToken));

    [HttpGet("assets/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetAsset(
        string id,
        CancellationToken cancellationToken)
    {
        var asset = await _inventory.GetAssetAsync(id, cancellationToken);
        return asset is null ? NotFound() : Ok(asset);
    }

    [HttpGet("assets/by-tag/{assetTag}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetAssetByTag(
        string assetTag,
        CancellationToken cancellationToken)
    {
        var asset = await _inventory.GetAssetByTagAsync(assetTag, cancellationToken);
        return asset is null ? NotFound() : Ok(asset);
    }

    [HttpGet("assets/{id}/qr")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetAssetQr(
        string id,
        CancellationToken cancellationToken)
    {
        var asset = await _inventory.GetAssetAsync(id, cancellationToken);

        if (asset is null)
            return NotFound();

        return Ok(new
        {
            assetId = asset.Id,
            assetTag = asset.AssetTag,
            payload = $"CISA-TI|{asset.AssetTag}|{asset.Id}"
        });
    }

    [HttpPost("assets")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsManage,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> CreateAsset(
        [FromBody] Asset asset,
        CancellationToken cancellationToken)
    {
        Asset created;
        try
        {
            created = await _inventory.CreateAssetAsync(
                asset,
                UserName(),
                cancellationToken);
        }
        catch (InventoryValidationException ex)
        {
            var body = new { code = ex.Code, message = ex.Message };
            return ex.Code == InventoryValidationCodes.AssetTagDuplicate
                ? Conflict(body)
                : BadRequest(body);
        }

        return CreatedAtAction(
            nameof(GetAsset),
            new { id = created.Id },
            created);
    }

    [HttpPut("assets/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsManage,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> UpdateAsset(
        string id,
        [FromBody] Asset asset,
        CancellationToken cancellationToken)
    {
        asset.Id = id;
        var updated = await _inventory.UpdateAssetAsync(
            asset,
            UserName(),
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("assets/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsManage,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> DeleteAsset(
        string id,
        CancellationToken cancellationToken)
        => await _inventory.DeleteAssetAsync(id, UserName(), cancellationToken)
            ? NoContent()
            : NotFound();

    [HttpPost("assets/{id}/link-device")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.AssetsManage,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> LinkDevice(
        string id,
        [FromBody] LinkDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _inventory.LinkDeviceAsync(
            id,
            request.DeviceId,
            UserName(),
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    public sealed record LinkDeviceRequest(string? DeviceId);

    // ============================================================
    // EMPLOYEES
    // ============================================================

    [HttpGet("employees")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.EmployeesView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] string? q,
        [FromQuery] string? departmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => Ok(await _inventory.GetEmployeesAsync(
            q,
            departmentId,
            page,
            pageSize,
            includeInactive,
            cancellationToken));

    [HttpGet("employees/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.EmployeesView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetEmployee(
        string id,
        CancellationToken cancellationToken)
    {
        var employee = await _inventory.GetEmployeeAsync(id, cancellationToken);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost("employees")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] Employee employee,
        CancellationToken cancellationToken)
        => Ok(await _inventory.CreateEmployeeAsync(
            employee,
            UserName(),
            cancellationToken));

    [HttpPut("employees/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> UpdateEmployee(
        string id,
        [FromBody] Employee employee,
        CancellationToken cancellationToken)
    {
        employee.Id = id;
        var updated = await _inventory.UpdateEmployeeAsync(
            employee,
            UserName(),
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("employees/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> DeleteEmployee(
        string id,
        CancellationToken cancellationToken)
        => await _inventory.DeleteEmployeeAsync(id, UserName(), cancellationToken)
            ? NoContent()
            : NotFound();

    // ============================================================
    // DELIVERIES
    // ============================================================

    [HttpGet("deliveries")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.DeliveriesView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetDeliveries(
        [FromQuery] string? q,
        [FromQuery] bool? returned,
        [FromQuery] string? period,
        [FromQuery] string? filter,
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetDeliveriesAsync(q, returned, period ?? filter, cancellationToken));

    [HttpGet("deliveries/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.DeliveriesView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetDelivery(
        string id,
        CancellationToken cancellationToken)
    {
        var delivery = await _inventory.GetDeliveryAsync(id, cancellationToken);
        return delivery is null ? NotFound() : Ok(delivery);
    }

    [HttpPost("deliveries")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> CreateDelivery(
        [FromBody] Delivery delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _inventory.CreateDeliveryAsync(
                delivery,
                UserName(),
                UserEmail(),
                cancellationToken));
        }
        catch (InventoryValidationException ex)
        {
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("deliveries/{id}/return-handheld")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> ReturnHandheld(
        string id,
        [FromBody] Return? item,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _inventory.ReturnHandheldAsync(
                id,
                item,
                UserName(),
                cancellationToken));
        }
        catch (InventoryValidationException ex)
        {
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("deliveries/{id}/return-rp4d")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> ReturnRp4d(
        string id,
        [FromBody] Return? item,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _inventory.ReturnRp4dAsync(
                id,
                item,
                UserName(),
                cancellationToken));
        }
        catch (InventoryValidationException ex)
        {
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    // ============================================================
    // RETURNS
    // ============================================================

    [HttpGet("returns")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.ReturnsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetReturns(
        [FromQuery] string? q,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetReturnsAsync(q, period, cancellationToken));

    [HttpGet("returns/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.ReturnsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetReturn(
        string id,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.GetReturnAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("returns")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> CreateReturn(
        [FromBody] Return item,
        CancellationToken cancellationToken)
        => Ok(await _inventory.CreateReturnAsync(
            item,
            UserName(),
            cancellationToken));

    // ============================================================
    // RECEPTIONS
    // ============================================================

    [HttpGet("receptions")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.ReceptionsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetReceptions(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetReceptionsAsync(q, cancellationToken));

    [HttpGet("receptions/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.ReceptionsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetReception(
        string id,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.GetReceptionAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("receptions")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage,
        PermissionCodes.Inventory.AssetsManage)]
    public async Task<IActionResult> CreateReception(
        [FromBody] Reception reception,
        CancellationToken cancellationToken)
        => Ok(await _inventory.CreateReceptionAsync(
            reception,
            UserName(),
            UserEmail(),
            cancellationToken));

    // ============================================================
    // INVENTORY SESSIONS / COUNTS
    // ============================================================

    [HttpGet("sessions")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.SessionsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetSessions(
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetSessionsAsync(cancellationToken));

    [HttpGet("sessions/{id}")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.SessionsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetSession(
        string id,
        CancellationToken cancellationToken)
    {
        var session = await _inventory.GetSessionAsync(id, cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost("sessions")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> CreateSession(
        [FromBody] InventorySession session,
        CancellationToken cancellationToken)
        => Ok(await _inventory.CreateSessionAsync(
            session,
            UserName(),
            cancellationToken));

    [HttpPost("sessions/{id}/close")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> CloseSession(
        string id,
        CancellationToken cancellationToken)
    {
        var session = await _inventory.CloseSessionAsync(
            id,
            UserName(),
            cancellationToken);

        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("sessions/{id}/counts")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.CountsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetCounts(
        string id,
        CancellationToken cancellationToken)
        => Ok(await _inventory.GetCountsAsync(id, cancellationToken));

    [HttpPost("sessions/{id}/counts")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> SaveCount(
        string id,
        [FromBody] InventoryCount count,
        CancellationToken cancellationToken)
    {
        count.SessionId = id;
        return Ok(await _inventory.SaveCountAsync(
            count,
            UserName(),
            cancellationToken));
    }

    [HttpGet("sessions/{id}/reconcile")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.CountsView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> Reconcile(
        string id,
        CancellationToken cancellationToken)
        => Ok(await _inventory.ReconcileAsync(id, cancellationToken));

    // ============================================================
    // HISTORY
    // ============================================================

    [HttpGet("history")]
    [RequireAnyPermission(
        PermissionCodes.Inventory.HistoryView,
        PermissionCodes.Inventory.View,
        PermissionCodes.Inventory.Manage)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? assetId,
        [FromQuery] string? q,
        [FromQuery] int limit = 300,
        CancellationToken cancellationToken = default)
        => Ok(await _inventory.GetHistoryAsync(
            assetId,
            q,
            limit,
            cancellationToken));

    // ============================================================
    // HELPERS
    // ============================================================

    private string? UserName()
        => User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name
            ?? User.FindFirstValue("name");

    private string? UserEmail()
        => User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
}
