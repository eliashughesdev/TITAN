using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Domain.Entities.Inventory;

namespace TitanMDM.Infrastructure.Persistence;

public sealed partial class InventoryService : IInventoryService
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly ItamDbContext _db;

    private readonly ItamDateRules _dates;

    public InventoryService(ItamDbContext db, ItamDateRules? dateRules = null)
    {
        _db = db;
        _dates = dateRules ?? new ItamDateRules();
    }

    // ============================================================
    // ASSETS
    // ============================================================

    public async Task<List<Asset>> GetAssetsAsync(
        string? query = null,
        string? status = null,
        string? categoryId = null,
        string? branchId = null,
        string? departmentId = null,
        string? warranty = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var assets = _db.Assets.AsNoTracking().AsQueryable();

        if (!includeDeleted)
            assets = assets.Where(x => x.Deleted != true);

        if (!string.IsNullOrWhiteSpace(status))
            assets = assets.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(categoryId))
            assets = assets.Where(x => x.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(branchId))
            assets = assets.Where(x => x.BranchId == branchId);

        if (!string.IsNullOrWhiteSpace(departmentId))
            assets = assets.Where(x => x.DepartmentId == departmentId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            assets = assets.Where(x =>
                (x.AssetTag != null && x.AssetTag.Contains(q)) ||
                (x.Name != null && x.Name.Contains(q)) ||
                (x.SerialNumber != null && x.SerialNumber.Contains(q)) ||
                (x.AssignedToName != null && x.AssignedToName.Contains(q)) ||
                (x.MacAddress != null && x.MacAddress.Contains(q)) ||
                (x.Hostname != null && x.Hostname.Contains(q)));
        }

        var result = await assets
            .OrderBy(x => x.AssetTag)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(warranty))
        {
            var today = DateTime.UtcNow.Date;
            var limit = today.AddDays(90);
            var mode = warranty.Trim().ToLowerInvariant();

            result = result.Where(asset =>
            {
                var hasDate = TryParseDate(asset.WarrantyEnd, out var end);
                var date = end.Date;

                return mode switch
                {
                    "expired" or "vencida" => hasDate && date < today,
                    "expiring" or "por_vencer" => hasDate && date >= today && date <= limit,
                    "active" or "vigente" => !hasDate || date > limit,
                    _ => true
                };
            }).ToList();
        }

        return result;
    }

    public Task<Asset?> GetAssetAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Asset?> GetAssetByTagAsync(
        string assetTag,
        CancellationToken cancellationToken = default)
        => _db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AssetTag == assetTag, cancellationToken);

    public async Task<List<Asset>> SearchAssetsAsync(
        string? query,
        int limit = 15,
        CancellationToken cancellationToken = default)
        => await GetAssetsAsync(
            query: query,
            cancellationToken: cancellationToken)
            .ContinueWith(
                task => task.Result.Take(Math.Clamp(limit, 1, 100)).ToList(),
                cancellationToken);

    public async Task<Asset> CreateAssetAsync(
        Asset asset,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (string.IsNullOrWhiteSpace(asset.Id))
            asset.Id = Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(asset.AssetTag))
            asset.AssetTag = await GenerateAssetTagAsync(cancellationToken);

        var now = Now();
        asset.AssetTag = asset.AssetTag.Trim().ToUpperInvariant();
        asset.Name = (asset.Name ?? string.Empty).Trim();
        asset.Status = string.IsNullOrWhiteSpace(asset.Status) ? "disponible" : asset.Status.Trim().ToLowerInvariant();
        asset.Condition = string.IsNullOrWhiteSpace(asset.Condition) ? "nuevo" : asset.Condition.Trim().ToLowerInvariant();
        asset.CreatedAt = string.IsNullOrWhiteSpace(asset.CreatedAt) ? now : asset.CreatedAt;
        asset.CreatedBy ??= userName;
        asset.UpdatedAt = now;
        asset.UpdatedBy = userName;

        _db.Assets.Add(asset);
        await AddHistoryAsync(
            asset.Id,
            "created",
            $"Activo {asset.AssetTag} registrado.",
            userName,
            null,
            asset,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return asset;
    }

    public async Task<Asset?> UpdateAssetAsync(
        Asset asset,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var current = await _db.Assets
            .FirstOrDefaultAsync(x => x.Id == asset.Id, cancellationToken);

        if (current is null)
            return null;

        var before = JsonSerializer.Serialize(current);

        current.AssetTag = string.IsNullOrWhiteSpace(asset.AssetTag)
            ? current.AssetTag
            : asset.AssetTag.Trim().ToUpperInvariant();
        current.Name = asset.Name?.Trim() ?? current.Name;
        current.SerialNumber = asset.SerialNumber;
        current.CategoryId = asset.CategoryId;
        current.ManufacturerId = asset.ManufacturerId;
        current.ModelId = asset.ModelId;
        current.BranchId = asset.BranchId;
        current.DepartmentId = asset.DepartmentId;
        current.LocationId = asset.LocationId;
        current.SupplierId = asset.SupplierId;
        current.Status = string.IsNullOrWhiteSpace(asset.Status)
            ? current.Status
            : asset.Status.Trim().ToLowerInvariant();
        current.Condition = string.IsNullOrWhiteSpace(asset.Condition)
            ? current.Condition
            : asset.Condition.Trim().ToLowerInvariant();
        current.PurchaseDate = asset.PurchaseDate;
        current.PurchaseCost = asset.PurchaseCost;
        current.WarrantyEnd = asset.WarrantyEnd;
        current.DepreciationYears = asset.DepreciationYears;
        current.MacAddress = asset.MacAddress;
        current.IpAddress = asset.IpAddress;
        current.Hostname = asset.Hostname;
        current.Specs = asset.Specs;
        current.Notes = asset.Notes;
        current.Photos = asset.Photos;
        current.Documents = asset.Documents;
        if (asset.DeviceId is not null)
            current.DeviceId = string.IsNullOrWhiteSpace(asset.DeviceId) ? null : asset.DeviceId.Trim();
        current.AssignedTo = asset.AssignedTo;
        current.AssignedToName = asset.AssignedToName;
        current.UpdatedAt = Now();
        current.UpdatedBy = userName;

        await AddHistoryAsync(
            current.Id,
            "updated",
            $"Activo {current.AssetTag} actualizado.",
            userName,
            before,
            current,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return current;
    }

    public async Task<bool> DeleteAssetAsync(
        string id,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (asset is null)
            return false;

        asset.Deleted = true;
        asset.DeletedAt = Now();
        asset.UpdatedAt = Now();
        asset.UpdatedBy = userName;

        await AddHistoryAsync(
            asset.Id,
            "deleted",
            $"Activo {asset.AssetTag} dado de baja.",
            userName,
            null,
            asset,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Asset?> LinkDeviceAsync(
        string id,
        string? deviceId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (asset is null)
            return null;

        var before = JsonSerializer.Serialize(asset);
        asset.DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        asset.UpdatedAt = Now();
        asset.UpdatedBy = userName;

        await AddHistoryAsync(
            asset.Id,
            "device_linked",
            asset.DeviceId is null
                ? $"Activo {asset.AssetTag} desvinculado del dispositivo MDM."
                : $"Activo {asset.AssetTag} vinculado al dispositivo MDM {asset.DeviceId}.",
            userName,
            before,
            asset,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return asset;
    }

    // ============================================================
    // EMPLOYEES
    // ============================================================

    public async Task<List<Employee>> GetEmployeesAsync(
        string? query = null,
        string? departmentId = null,
        int page = 1,
        int pageSize = 200,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var employees = _db.Employees.AsNoTracking().AsQueryable();

        if (!includeInactive)
            employees = employees.Where(x => x.Deleted != true && x.IsActive != false);

        if (!string.IsNullOrWhiteSpace(departmentId))
            employees = employees.Where(x => x.DepartmentId == departmentId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            employees = employees.Where(x =>
                (x.Name != null && x.Name.Contains(q)) ||
                (x.Code != null && x.Code.Contains(q)) ||
                (x.Email != null && x.Email.Contains(q)) ||
                (x.Position != null && x.Position.Contains(q)));
        }

        var skip = Math.Max(0, (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 500));

        return await employees
            .OrderBy(x => x.Name)
            .Skip(skip)
            .Take(Math.Clamp(pageSize, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public Task<Employee?> GetEmployeeAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Employee> CreateEmployeeAsync(
        Employee employee,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employee);

        if (string.IsNullOrWhiteSpace(employee.Id))
            employee.Id = Guid.NewGuid().ToString();

        var now = Now();
        employee.Name = (employee.Name ?? string.Empty).Trim();
        employee.CreatedAt = string.IsNullOrWhiteSpace(employee.CreatedAt) ? now : employee.CreatedAt;
        employee.CreatedBy ??= userName;
        employee.UpdatedAt = now;
        employee.UpdatedBy = userName;
        employee.IsActive = true;

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);
        return employee;
    }

    public async Task<Employee?> UpdateEmployeeAsync(
        Employee employee,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var current = await _db.Employees
            .FirstOrDefaultAsync(x => x.Id == employee.Id, cancellationToken);

        if (current is null)
            return null;

        current.Code = employee.Code;
        current.Name = employee.Name?.Trim() ?? current.Name;
        current.DepartmentId = employee.DepartmentId;
        current.Supervisor = employee.Supervisor;
        current.Email = employee.Email;
        current.Phone = employee.Phone;
        current.Position = employee.Position;
        current.BranchId = employee.BranchId;
        current.Notes = employee.Notes;
        current.IsActive = employee.IsActive;
        current.UpdatedAt = Now();
        current.UpdatedBy = userName;

        await _db.SaveChangesAsync(cancellationToken);
        return current;
    }

    public async Task<bool> DeleteEmployeeAsync(
        string id,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (employee is null)
            return false;

        employee.Deleted = true;
        employee.DeletedAt = Now();
        employee.IsActive = false;
        employee.UpdatedAt = Now();
        employee.UpdatedBy = userName;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ============================================================
    // DELIVERIES
    // ============================================================

    public async Task<List<Delivery>> GetDeliveriesAsync(
        string? query = null,
        bool? returned = null,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        var deliveries = _db.Deliveries
            .AsNoTracking()
            .Where(x => x.Deleted != true);

        if (returned.HasValue)
            deliveries = deliveries.Where(x => x.Returned == returned.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            deliveries = deliveries.Where(x =>
                (x.AssetTag != null && x.AssetTag.Contains(q)) ||
                (x.AssetName != null && x.AssetName.Contains(q)) ||
                (x.AssignedToName != null && x.AssignedToName.Contains(q)) ||
                (x.EmployeeCode != null && x.EmployeeCode.Contains(q)));
        }

        var rows = await deliveries.ToListAsync(cancellationToken);

        // Fechas en texto: el filtro por dia y el orden se resuelven con
        // ItamDateRules (zona del negocio, instante real), no por cadena.
        var today = _dates.Today();

        IEnumerable<Delivery> filtered = (period ?? ItamPeriods.All).Trim().ToLowerInvariant() switch
        {
            ItamPeriods.Today => rows.Where(x => _dates.ToBusinessDate(x.DeliveryDate) == today),
            ItamPeriods.Pending => rows.Where(x => x.Returned != true),
            ItamPeriods.Overdue => rows.Where(x =>
                x.Returned != true &&
                _dates.ToBusinessDate(x.ExpectedReturn) is { } expected &&
                expected < today),
            _ => rows,
        };

        return filtered
            .OrderByDescending(x => ItamDateRules.SortKey(x.DeliveryDate))
            .ToList();
    }

    public Task<Delivery?> GetDeliveryAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _db.Deliveries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Delivery> CreateDeliveryAsync(
        Delivery delivery,
        string? userName,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var asset = await ResolveAssetAsync(
            delivery.AssetId,
            delivery.AssetTag,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(delivery.Id))
            delivery.Id = Guid.NewGuid().ToString();

        delivery.DeliveryDate = string.IsNullOrWhiteSpace(delivery.DeliveryDate)
            ? Now()
            : delivery.DeliveryDate;
        delivery.Returned = false;
        delivery.ReturnDate = null;
        delivery.Deleted = false;
        delivery.CreatedAt = Now();
        delivery.DeliveredBy ??= userName;
        delivery.DeliveredByEmail ??= userEmail;

        if (asset is not null)
        {
            delivery.AssetId = asset.Id;
            delivery.AssetTag = asset.AssetTag;
            delivery.AssetName ??= asset.Name;
            delivery.SerialNumber ??= asset.SerialNumber;
            delivery.DepartmentId ??= asset.DepartmentId;
            delivery.BranchId ??= asset.BranchId;
            delivery.Condition ??= asset.Condition;
        }

        _db.Deliveries.Add(delivery);

        if (asset is not null)
        {
            asset.Status = "asignado";
            asset.AssignedTo = delivery.EmployeeCode;
            asset.AssignedToName = delivery.AssignedToName;
            asset.UpdatedAt = Now();
            asset.UpdatedBy = userName;

            await AddHistoryAsync(
                asset.Id,
                "delivery",
                $"Entregado a {delivery.AssignedToName} ({delivery.EmployeeCode}).",
                userName,
                null,
                delivery,
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return delivery;
    }

    // ============================================================
    // RETURNS
    // ============================================================

    public async Task<List<Return>> GetReturnsAsync(
        string? query = null,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        var returns = _db.Returns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            returns = returns.Where(x =>
                (x.AssetTag != null && x.AssetTag.Contains(q)) ||
                (x.AssetName != null && x.AssetName.Contains(q)) ||
                (x.ReturnedBy != null && x.ReturnedBy.Contains(q)) ||
                (x.EmployeeCode != null && x.EmployeeCode.Contains(q)));
        }

        var rows = await returns.ToListAsync(cancellationToken);
        var today = _dates.Today();

        IEnumerable<Return> filtered =
            string.Equals(period?.Trim(), ItamPeriods.Today, StringComparison.OrdinalIgnoreCase)
                ? rows.Where(x => _dates.ToBusinessDate(x.ReturnDate) == today)
                : rows;

        return filtered
            .OrderByDescending(x => ItamDateRules.SortKey(x.ReturnDate))
            .ToList();
    }

    public Task<Return?> GetReturnAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _db.Returns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Return> CreateReturnAsync(
        Return item,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var delivery = await _db.Deliveries
            .FirstOrDefaultAsync(x => x.Id == item.DeliveryId, cancellationToken);

        var asset = await ResolveAssetAsync(
            item.AssetId ?? delivery?.AssetId,
            item.AssetTag ?? delivery?.AssetTag,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(item.Id))
            item.Id = Guid.NewGuid().ToString();

        item.ReturnDate = string.IsNullOrWhiteSpace(item.ReturnDate)
            ? Now()
            : item.ReturnDate;
        item.CreatedAt = Now();
        item.ReceivedBy ??= userName;

        if (delivery is not null)
        {
            item.DeliveryId ??= delivery.Id;
            item.AssetId ??= delivery.AssetId;
            item.AssetTag ??= delivery.AssetTag;
            item.AssetName ??= delivery.AssetName;
            item.EmployeeCode ??= delivery.EmployeeCode;
            item.ReturnedBy ??= delivery.AssignedToName;

            delivery.Returned = true;
            delivery.ReturnDate = item.ReturnDate;
        }

        if (asset is not null)
        {
            item.AssetId = asset.Id;
            item.AssetTag = asset.AssetTag;
            item.AssetName ??= asset.Name;

            asset.Status = "disponible";
            asset.AssignedTo = null;
            asset.AssignedToName = null;
            asset.Condition = string.IsNullOrWhiteSpace(item.Condition)
                ? asset.Condition
                : item.Condition.Trim().ToLowerInvariant();
            asset.UpdatedAt = Now();
            asset.UpdatedBy = userName;

            await AddHistoryAsync(
                asset.Id,
                "return",
                $"Devuelto por {item.ReturnedBy} ({item.EmployeeCode}). Condición: {item.Condition}.",
                userName,
                null,
                item,
                cancellationToken);
        }

        _db.Returns.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return item;
    }

    // ============================================================
    // RECEPTIONS
    // ============================================================

    public async Task<List<Reception>> GetReceptionsAsync(
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var receptions = _db.Receptions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            receptions = receptions.Where(x =>
                (x.Description != null && x.Description.Contains(q)) ||
                (x.SerialNumber != null && x.SerialNumber.Contains(q)) ||
                (x.DeliveredBy != null && x.DeliveredBy.Contains(q)) ||
                (x.EmployeeCode != null && x.EmployeeCode.Contains(q)));
        }

        return await receptions
            .OrderByDescending(x => x.ReceptionDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Reception?> GetReceptionAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _db.Receptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Reception> CreateReceptionAsync(
        Reception reception,
        string? userName,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reception);

        if (string.IsNullOrWhiteSpace(reception.Id))
            reception.Id = Guid.NewGuid().ToString();

        reception.ReceptionDate = string.IsNullOrWhiteSpace(reception.ReceptionDate)
            ? Now()
            : reception.ReceptionDate;
        reception.CreatedAt = Now();
        reception.ReceivedBy ??= userName;
        reception.ReceivedByEmail ??= userEmail;
        reception.Condition = string.IsNullOrWhiteSpace(reception.Condition)
            ? "nuevo"
            : reception.Condition.Trim().ToLowerInvariant();

        var asset = await ResolveAssetAsync(
            reception.AssetId,
            null,
            cancellationToken);

        if (asset is null && !string.IsNullOrWhiteSpace(reception.SerialNumber))
        {
            asset = await _db.Assets
                .FirstOrDefaultAsync(
                    x => x.SerialNumber == reception.SerialNumber,
                    cancellationToken);
        }

        if (asset is null)
        {
            asset = new Asset
            {
                Id = Guid.NewGuid().ToString(),
                AssetTag = await GenerateAssetTagAsync(cancellationToken),
                Name = string.IsNullOrWhiteSpace(reception.Description)
                    ? "Activo recibido"
                    : reception.Description.Trim(),
                SerialNumber = reception.SerialNumber,
                CategoryId = reception.CategoryId,
                Status = "disponible",
                Condition = reception.Condition,
                CreatedAt = Now(),
                CreatedBy = userName
            };

            _db.Assets.Add(asset);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            asset.SerialNumber ??= reception.SerialNumber;
            asset.CategoryId ??= reception.CategoryId;
            if (!string.IsNullOrWhiteSpace(reception.Condition))
                asset.Condition = reception.Condition;
            asset.UpdatedAt = Now();
            asset.UpdatedBy = userName;
        }

        reception.AssetId = asset.Id;

        _db.Receptions.Add(reception);

        await AddHistoryAsync(
            asset.Id,
            "reception",
            $"Recibido por {reception.ReceivedBy} de {reception.DeliveredBy}. Condición: {reception.Condition}.",
            userName,
            null,
            reception,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return reception;
    }

    // ============================================================
    // INVENTORY SESSIONS / COUNTS
    // ============================================================

    public Task<List<InventorySession>> GetSessionsAsync(
        CancellationToken cancellationToken = default)
        => _db.InventorySessions
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<InventorySession?> GetSessionAsync(
        string id,
        CancellationToken cancellationToken = default)
        => _db.InventorySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<InventorySession> CreateSessionAsync(
        InventorySession session,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(session.Id))
            session.Id = Guid.NewGuid().ToString();

        session.Name = string.IsNullOrWhiteSpace(session.Name)
            ? $"Conteo {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
            : session.Name.Trim();
        session.Status = string.IsNullOrWhiteSpace(session.Status) ? "open" : session.Status;
        session.CreatedAt = Now();
        session.CreatedBy ??= userName;
        session.Counted = 0;

        _db.InventorySessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<InventorySession?> CloseSessionAsync(
        string id,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.InventorySessions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
            return null;

        var reaction = await ReconcileAsync(id, cancellationToken);
        session.Status = "closed";
        session.Counted = reaction.CountedCount;
        session.Report = JsonSerializer.Serialize(new
        {
            system = reaction.SystemCount,
            counted = reaction.CountedCount,
            found = reaction.Found.Count,
            missing = reaction.Missing.Count,
            extra = reaction.Extra.Count,
            closedBy = userName,
            closedAt = Now()
        });

        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public Task<List<InventoryCount>> GetCountsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => _db.InventoryCounts
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.AssetTag)
            .ToListAsync(cancellationToken);

    public async Task<InventoryCount> SaveCountAsync(
        InventoryCount count,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(count);

        if (string.IsNullOrWhiteSpace(count.Id))
            count.Id = Guid.NewGuid().ToString();

        count.CountedAt = string.IsNullOrWhiteSpace(count.CountedAt) ? Now() : count.CountedAt;
        count.CountedBy ??= userName;

        if (!string.IsNullOrWhiteSpace(count.AssetTag))
        {
            var asset = await _db.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.AssetTag == count.AssetTag,
                    cancellationToken);

            if (asset is not null)
            {
                count.AssetId = asset.Id;
                count.AssetName ??= asset.Name;
                count.SystemStatus ??= asset.Status;
                count.Condition ??= asset.Condition;
            }
        }

        var existing = await _db.InventoryCounts
            .FirstOrDefaultAsync(
                x => x.Id == count.Id,
                cancellationToken);

        if (existing is null)
        {
            _db.InventoryCounts.Add(count);
        }
        else
        {
            existing.SessionId = count.SessionId;
            existing.AssetTag = count.AssetTag;
            existing.AssetId = count.AssetId;
            existing.AssetName = count.AssetName;
            existing.Found = count.Found;
            existing.SystemStatus = count.SystemStatus;
            existing.Condition = count.Condition;
            existing.LocationNote = count.LocationNote;
            existing.CountedBy = count.CountedBy;
            existing.CountedAt = count.CountedAt;
            count = existing;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(count.SessionId))
        {
            var session = await _db.InventorySessions
                .FirstOrDefaultAsync(
                    x => x.Id == count.SessionId,
                    cancellationToken);

            if (session is not null)
            {
                session.Counted = await _db.InventoryCounts
                    .CountAsync(
                        x => x.SessionId == count.SessionId,
                        cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return count;
    }

    public async Task<InventoryReconciliationDto> ReconcileAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.InventorySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        var system = _db.Assets
            .AsNoTracking()
            .Where(x => x.Deleted != true);

        if (session is not null)
        {
            if (!string.IsNullOrWhiteSpace(session.BranchId))
                system = system.Where(x => x.BranchId == session.BranchId);

            if (!string.IsNullOrWhiteSpace(session.DepartmentId))
                system = system.Where(x => x.DepartmentId == session.DepartmentId);
        }

        var systemAssets = await system.ToListAsync(cancellationToken);
        var counts = await _db.InventoryCounts
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        var systemByTag = systemAssets
            .Where(x => !string.IsNullOrWhiteSpace(x.AssetTag))
            .GroupBy(x => x.AssetTag!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var countedByTag = counts
            .Where(x => !string.IsNullOrWhiteSpace(x.AssetTag))
            .GroupBy(x => x.AssetTag!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var found = new List<Asset>();
        var missing = new List<Asset>();

        foreach (var asset in systemAssets)
        {
            if (asset.AssetTag is not null &&
                countedByTag.TryGetValue(asset.AssetTag, out var counted) &&
                counted.Found == true)
            {
                found.Add(asset);
            }
            else
            {
                missing.Add(asset);
            }
        }

        var extra = counts
            .Where(x =>
                x.Found == true &&
                !string.IsNullOrWhiteSpace(x.AssetTag) &&
                !systemByTag.ContainsKey(x.AssetTag!))
            .ToList();

        return new InventoryReconciliationDto(
            sessionId,
            systemAssets.Count,
            counts.Count(x => x.Found == true),
            found,
            missing,
            extra);
    }

    public async Task<List<InventoryAssetDepreciationDto>> GetDepreciationReportAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.Date;
        var assets = await _db.Assets
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .ToListAsync(cancellationToken);

        // Vida util por categoria cuando el activo no define la suya.
        var categoryYears = await _db.Categories
            .AsNoTracking()
            .Where(x => x.DepreciationYears != null && x.DepreciationYears > 0)
            .ToDictionaryAsync(x => x.Id, x => x.DepreciationYears, cancellationToken);

        var report = new List<InventoryAssetDepreciationDto>(assets.Count);

        foreach (var asset in assets)
        {
            var cost = asset.PurchaseCost ?? 0m;
            var years = asset.DepreciationYears is > 0
                ? asset.DepreciationYears
                : asset.CategoryId is not null &&
                  categoryYears.TryGetValue(asset.CategoryId, out var fromCategory)
                    ? fromCategory
                    : null;
            if (cost <= 0m || years is null or <= 0)
            {
                report.Add(new InventoryAssetDepreciationDto(
                    asset.Id,
                    asset.AssetTag ?? string.Empty,
                    asset.Name,
                    cost,
                    years,
                    0,
                    0m,
                    0m,
                    cost));
                continue;
            }

            var yearsElapsed = 0.0;
            if (TryParseDate(asset.PurchaseDate, out var purchased))
            {
                yearsElapsed = Math.Max(0, (now - purchased.Date).TotalDays / 365.25);
            }

            var annual = cost / years.Value;
            var accumulated = Math.Min(cost, (decimal)yearsElapsed * annual);
            var bookValue = Math.Max(0m, cost - accumulated);

            report.Add(new InventoryAssetDepreciationDto(
                asset.Id,
                asset.AssetTag ?? string.Empty,
                asset.Name,
                cost,
                years,
                yearsElapsed,
                annual,
                accumulated,
                bookValue));
        }

        return report
            .OrderBy(x => x.AssetTag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ============================================================
    // HISTORY
    // ============================================================

    public async Task<List<AssetHistory>> GetHistoryAsync(
        string? assetId = null,
        string? query = null,
        int limit = 300,
        CancellationToken cancellationToken = default)
    {
        var history = _db.AssetHistory.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(assetId))
            history = history.Where(x => x.AssetId == assetId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            history = history.Where(x =>
                (x.Action != null && x.Action.Contains(q)) ||
                (x.Detail != null && x.Detail.Contains(q)) ||
                (x.UserName != null && x.UserName.Contains(q)) ||
                (x.UserEmail != null && x.UserEmail.Contains(q)));
        }

        return await history
            .OrderByDescending(x => x.Timestamp)
            .Take(Math.Clamp(limit, 1, 2000))
            .ToListAsync(cancellationToken);
    }

    // ============================================================
    // DASHBOARD / CATALOGS
    // ============================================================

    public async Task<InventoryDashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var assets = await _db.Assets
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .ToListAsync(cancellationToken);

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .ToListAsync(cancellationToken);

        var branches = await _db.Branches
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .ToListAsync(cancellationToken);

        var categoryNames = categories
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var branchNames = branches
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var manufacturers = await _db.Manufacturers
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .ToListAsync(cancellationToken);

        var departments = await _db.Departments
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .ToListAsync(cancellationToken);

        var manufacturerNames = manufacturers
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var departmentNames = departments
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var employeeCount = await _db.Employees
            .AsNoTracking()
            .CountAsync(x => x.Deleted != true && x.IsActive != false, cancellationToken);

        var deliveries = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.Deleted != true)
            .ToListAsync(cancellationToken);

        var receptionCount = await _db.Receptions
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var handHeldCategoryIds = categories
            .Where(x =>
                Contains(x.Name, "hand held") ||
                Contains(x.Name, "handheld") ||
                Contains(x.Name, "ck65") ||
                Contains(x.Type, "hand"))
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var handHeld = assets.Count(x =>
            (!string.IsNullOrWhiteSpace(x.CategoryId) && handHeldCategoryIds.Contains(x.CategoryId)) ||
            Contains(x.Name, "ck65") ||
            Contains(x.Name, "hand held") ||
            Contains(x.Name, "handheld"));

        decimal totalCost = 0m;
        decimal accumulated = 0m;
        var warrantyLimit = DateTime.UtcNow.Date.AddDays(90);
        var expiring = 0;

        foreach (var asset in assets)
        {
            var cost = asset.PurchaseCost ?? 0m;
            totalCost += cost;

            if (TryParseDate(asset.WarrantyEnd, out var warranty) &&
                warranty.Date >= DateTime.UtcNow.Date &&
                warranty.Date <= warrantyLimit)
            {
                expiring++;
            }

            // Misma regla que GetDepreciationReportAsync: vida util del
            // activo o, en su defecto, la de su categoria.
            var lifeYears = asset.DepreciationYears is > 0
                ? asset.DepreciationYears
                : categories
                    .FirstOrDefault(c => c.Id == asset.CategoryId)?
                    .DepreciationYears;

            if (cost > 0 && lifeYears is > 0)
            {
                var annual = cost / lifeYears.Value;
                var years = TryParseDate(asset.PurchaseDate, out var purchased)
                    ? Math.Max(0, (DateTime.UtcNow - purchased).TotalDays / 365.25)
                    : 0;
                accumulated += Math.Min(cost, (decimal)years * annual);
            }
        }

        var today = DateTime.UtcNow.Date;
        var recentlyDelivered = deliveries.Count(delivery =>
            TryParseDate(delivery.DeliveryDate, out var deliveredAt) &&
            deliveredAt >= DateTime.UtcNow.AddDays(-365));

        var atRisk = assets.Count(asset =>
            EqualsState(asset.Status, "danado") ||
            EqualsState(asset.Condition, "malo") ||
            (TryParseDate(asset.WarrantyEnd, out var warrantyEnd) && warrantyEnd.Date < today));

        var rotationRate = Math.Round(
            (double)recentlyDelivered / Math.Max(1, assets.Count),
            2);

        // Ultimos 12 meses calendario en la zona del negocio. La clave
        // yyyy-MM la formatea el frontend con Intl, sin meses fijos.
        var businessToday = _dates.Today();
        var currentMonth = new DateOnly(businessToday.Year, businessToday.Month, 1);

        var monthlyAssignments = Enumerable
            .Range(0, 12)
            .Select(offset => currentMonth.AddMonths(-offset))
            .OrderBy(month => month)
            .Select(month => new InventoryNameCount(
                month.ToString("yyyy-MM", Invariant),
                deliveries.Count(delivery =>
                    _dates.ToBusinessDate(delivery.DeliveryDate) is { } deliveredAt &&
                    deliveredAt.Year == month.Year &&
                    deliveredAt.Month == month.Month)))
            .ToList();

        return new InventoryDashboardDto(
            TotalAssets: assets.Count,
            AssignedAssets: assets.Count(x => EqualsState(x.Status, "asignado")),
            AvailableAssets: assets.Count(x => EqualsState(x.Status, "disponible")),
            HandHeldAssets: handHeld,
            DamagedAssets: assets.Count(x => EqualsState(x.Status, "danado")),
            RetiredAssets: assets.Count(x => EqualsState(x.Status, "baja")),
            TotalEmployees: employeeCount,
            ActiveDeliveries: deliveries.Count(x => x.Returned != true),
            ReturnedDeliveries: deliveries.Count(x => x.Returned == true),
            PendingReceptions: receptionCount,
            WarrantiesExpiringSoon: expiring,
            TotalPurchaseCost: decimal.Round(totalCost, 2),
            AccumulatedDepreciation: decimal.Round(accumulated, 2),
            BookValue: decimal.Round(Math.Max(0m, totalCost - accumulated), 2),
            AtRiskAssets: atRisk,
            TotalBranches: branches.Count,
            RotationRate: rotationRate,
            ByStatus: assets
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Status) ? "sin estado" : x.Status!)
                .Select(g => new InventoryNameCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList(),
            ByCondition: assets
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Condition) ? "sin condición" : x.Condition!)
                .Select(g => new InventoryNameCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList(),
            ByCategory: assets
                .GroupBy(x => x.CategoryId is not null && categoryNames.TryGetValue(x.CategoryId, out var name)
                    ? name
                    : "Sin categoría")
                .Select(g => new InventoryNameCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList(),
            ByBranch: assets
                .GroupBy(x => x.BranchId is not null && branchNames.TryGetValue(x.BranchId, out var name)
                    ? name
                    : "Sin sucursal")
                .Select(g => new InventoryNameCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList(),
            ByManufacturer: assets
                .GroupBy(x => x.ManufacturerId is not null && manufacturerNames.TryGetValue(x.ManufacturerId, out var name)
                    ? name
                    : "Sin fabricante")
                .Select(g => new InventoryNameCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList(),
            ByDepartment: assets
                .GroupBy(x => x.DepartmentId is not null && departmentNames.TryGetValue(x.DepartmentId, out var name)
                    ? name
                    : "Sin departamento")
                .Select(g => new InventoryNameCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList(),
            MonthlyAssignments: monthlyAssignments);
    }

    public async Task<InventoryCatalogDto> GetCatalogsAsync(
        CancellationToken cancellationToken = default)
        => new(
            await _db.Categories.AsNoTracking().Where(x => x.Deleted != true).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await _db.Branches.AsNoTracking().Where(x => x.Deleted != true).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await _db.Departments.AsNoTracking().Where(x => x.Deleted != true).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await _db.Manufacturers.AsNoTracking().Where(x => x.Deleted != true).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await _db.Suppliers.AsNoTracking().Where(x => x.Deleted != true).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await _db.AssetModels.AsNoTracking().Where(x => x.Deleted != true).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await _db.Locations.AsNoTracking().Where(x => x.Deleted != true).OrderBy(x => x.Name).ToListAsync(cancellationToken));

    // ============================================================
    // HELPERS
    // ============================================================

    private async Task<Asset?> ResolveAssetAsync(
        string? assetId,
        string? assetTag,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(assetId))
        {
            var byId = await _db.Assets
                .FirstOrDefaultAsync(x => x.Id == assetId, cancellationToken);
            if (byId is not null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(assetTag))
        {
            return await _db.Assets
                .FirstOrDefaultAsync(x => x.AssetTag == assetTag, cancellationToken);
        }

        return null;
    }

    private async Task<string> GenerateAssetTagAsync(
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 25; i++)
        {
            var tag = Guid.NewGuid()
                .ToString("N")
                .Substring(0, 10)
                .ToUpperInvariant();

            var exists = await _db.Assets
                .AsNoTracking()
                .AnyAsync(x => x.AssetTag == tag, cancellationToken);

            if (!exists)
                return tag;
        }

        return Guid.NewGuid()
            .ToString("N")
            .Substring(0, 12)
            .ToUpperInvariant();
    }

    private async Task AddHistoryAsync(
        string assetId,
        string action,
        string detail,
        string? userName,
        string? beforeJson,
        object? after,
        CancellationToken cancellationToken)
    {
        _db.AssetHistory.Add(new AssetHistory
        {
            Id = Guid.NewGuid().ToString(),
            AssetId = assetId,
            Action = action,
            Detail = detail,
            BeforeJson = beforeJson,
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            UserName = userName,
            Timestamp = Now()
        });

        await Task.CompletedTask;
    }

    private static string Now()
        => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", Invariant);

    private static bool Contains(string? value, string token)
        => value is not null && value.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsState(string? value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateTime.TryParse(value, Invariant, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out result))
            return true;

        return DateTime.TryParse(value, out result);
    }
}
