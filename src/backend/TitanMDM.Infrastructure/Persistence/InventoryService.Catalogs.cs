using System.Net.Mail;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Domain.Entities.Inventory;

namespace TitanMDM.Infrastructure.Persistence;

/*
 * Alta, edicion y baja logica de catalogos ITAM (cisa_ti_itam).
 * Los catalogos son pequenos (decenas de filas): las validaciones de
 * duplicados se hacen en memoria con comparacion sin mayusculas/espacios.
 */
public sealed partial class InventoryService
{
    public async Task<InventoryCatalogMutationResult> SaveCatalogItemAsync(
        string kind,
        string? id,
        InventoryCatalogItemInput input,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var name = Clean(input.Name);
        if (name is null)
            return Invalid("El nombre es obligatorio.");

        switch (kind.ToLowerInvariant())
        {
            case InventoryCatalogKinds.Categories:
                if (input.DepreciationYears is < 0 or > 50)
                    return Invalid("Los años de depreciación deben estar entre 0 y 50.");
                return await SaveAsync(_db.Categories, id, name, 160, null, item =>
                {
                    item.Type = Limit(input.Type, 60);
                    item.DepreciationYears = input.DepreciationYears;
                }, cancellationToken);

            case InventoryCatalogKinds.Branches:
                return await SaveAsync(_db.Branches, id, name, 160, null, item =>
                {
                    item.Code = Limit(input.Code, 40);
                    item.City = Limit(input.City, 120);
                    item.Address = Limit(input.Address, 300);
                    item.Phone = Limit(input.Phone, 40);
                }, cancellationToken);

            case InventoryCatalogKinds.Departments:
                return await SaveAsync(_db.Departments, id, name, 160, null, item =>
                {
                    item.Code = Limit(input.Code, 40);
                    item.Manager = Limit(input.Manager, 160);
                }, cancellationToken);

            case InventoryCatalogKinds.Manufacturers:
                return await SaveAsync(_db.Manufacturers, id, name, 160, null, item =>
                {
                    item.Website = Limit(input.Website, 200);
                    item.SupportPhone = Limit(input.SupportPhone, 40);
                }, cancellationToken);

            case InventoryCatalogKinds.Suppliers:
            {
                var email = Clean(input.Email);
                if (email is not null && !MailAddress.TryCreate(email, out _))
                    return Invalid("El correo del proveedor no es válido.");
                return await SaveAsync(_db.Suppliers, id, name, 200, null, item =>
                {
                    item.Rnc = Limit(input.Rnc, 40);
                    item.ContactName = Limit(input.ContactName, 160);
                    item.Phone = Limit(input.Phone, 40);
                    item.Email = Limit(email, 160);
                    item.Address = Limit(input.Address, 300);
                }, cancellationToken);
            }

            case InventoryCatalogKinds.Models:
            {
                var manufacturerId = Clean(input.ManufacturerId);
                var categoryId = Clean(input.CategoryId);
                if (manufacturerId is not null &&
                    !await _db.Manufacturers.AnyAsync(x => x.Id == manufacturerId && x.Deleted != true, cancellationToken))
                    return Invalid("El fabricante seleccionado no existe.");
                if (categoryId is not null &&
                    !await _db.Categories.AnyAsync(x => x.Id == categoryId && x.Deleted != true, cancellationToken))
                    return Invalid("La categoría seleccionada no existe.");

                // El mismo nombre de modelo puede existir en fabricantes distintos.
                return await SaveAsync(_db.AssetModels, id, name, 160,
                    other => SameText(other.ManufacturerId, manufacturerId),
                    item =>
                    {
                        item.ModelNumber = Limit(input.ModelNumber, 120);
                        item.ManufacturerId = manufacturerId;
                        item.CategoryId = categoryId;
                    }, cancellationToken);
            }

            case InventoryCatalogKinds.Locations:
            {
                var branchId = Clean(input.BranchId);
                if (branchId is not null &&
                    !await _db.Branches.AnyAsync(x => x.Id == branchId && x.Deleted != true, cancellationToken))
                    return Invalid("La sucursal seleccionada no existe.");

                // "Almacen" puede repetirse en cada sucursal.
                return await SaveAsync(_db.Locations, id, name, 160,
                    other => SameText(other.BranchId, branchId),
                    item =>
                    {
                        item.BranchId = branchId;
                        item.Floor = Limit(input.Floor, 60);
                        item.Area = Limit(input.Area, 120);
                    }, cancellationToken);
            }

            default:
                return Invalid($"Catálogo desconocido: {kind}.");
        }
    }

    public async Task<InventoryCatalogMutationResult> DeleteCatalogItemAsync(
        string kind,
        string id,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var activeAssets = _db.Assets.Where(x => x.Deleted != true);
        var activeEmployees = _db.Employees.Where(x => x.Deleted != true);

        switch (kind.ToLowerInvariant())
        {
            case InventoryCatalogKinds.Categories:
                return await SoftDeleteAsync(_db.Categories, id, async () =>
                    Usage(await activeAssets.CountAsync(x => x.CategoryId == id, cancellationToken), "activo(s)")
                    ?? Usage(await _db.AssetModels.CountAsync(x => x.CategoryId == id && x.Deleted != true, cancellationToken), "modelo(s)"),
                    cancellationToken);

            case InventoryCatalogKinds.Branches:
                return await SoftDeleteAsync(_db.Branches, id, async () =>
                    Usage(await activeAssets.CountAsync(x => x.BranchId == id, cancellationToken), "activo(s)")
                    ?? Usage(await activeEmployees.CountAsync(x => x.BranchId == id, cancellationToken), "colaborador(es)")
                    ?? Usage(await _db.Locations.CountAsync(x => x.BranchId == id && x.Deleted != true, cancellationToken), "ubicación(es)"),
                    cancellationToken);

            case InventoryCatalogKinds.Departments:
                return await SoftDeleteAsync(_db.Departments, id, async () =>
                    Usage(await activeAssets.CountAsync(x => x.DepartmentId == id, cancellationToken), "activo(s)")
                    ?? Usage(await activeEmployees.CountAsync(x => x.DepartmentId == id, cancellationToken), "colaborador(es)"),
                    cancellationToken);

            case InventoryCatalogKinds.Manufacturers:
                return await SoftDeleteAsync(_db.Manufacturers, id, async () =>
                    Usage(await activeAssets.CountAsync(x => x.ManufacturerId == id, cancellationToken), "activo(s)")
                    ?? Usage(await _db.AssetModels.CountAsync(x => x.ManufacturerId == id && x.Deleted != true, cancellationToken), "modelo(s)"),
                    cancellationToken);

            case InventoryCatalogKinds.Suppliers:
                return await SoftDeleteAsync(_db.Suppliers, id, async () =>
                    Usage(await activeAssets.CountAsync(x => x.SupplierId == id, cancellationToken), "activo(s)"),
                    cancellationToken);

            case InventoryCatalogKinds.Models:
                return await SoftDeleteAsync(_db.AssetModels, id, async () =>
                    Usage(await activeAssets.CountAsync(x => x.ModelId == id, cancellationToken), "activo(s)"),
                    cancellationToken);

            case InventoryCatalogKinds.Locations:
                return await SoftDeleteAsync(_db.Locations, id, async () =>
                    Usage(await activeAssets.CountAsync(x => x.LocationId == id, cancellationToken), "activo(s)"),
                    cancellationToken);

            default:
                return Invalid($"Catálogo desconocido: {kind}.");
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private async Task<InventoryCatalogMutationResult> SaveAsync<T>(
        DbSet<T> set,
        string? id,
        string name,
        int nameMaxLength,
        Func<T, bool>? sameScope,
        Action<T> apply,
        CancellationToken cancellationToken)
        where T : class, IItamCatalogEntity, new()
    {
        if (name.Length > nameMaxLength)
            return Invalid($"El nombre admite como máximo {nameMaxLength} caracteres.");

        var active = await set.Where(x => x.Deleted != true).ToListAsync(cancellationToken);

        T? item = null;
        if (!string.IsNullOrWhiteSpace(id))
        {
            item = active.FirstOrDefault(x => x.Id == id);
            if (item is null)
                return new InventoryCatalogMutationResult(InventoryCatalogMutationStatus.NotFound, Error: "El registro no existe o fue eliminado.");
        }

        var creating = item is null;
        item ??= new T();
        var previousName = item.Name;
        item.Name = name;
        apply(item);

        var duplicate = active.Any(other =>
            other.Id != item.Id &&
            SameText(other.Name, name) &&
            (sameScope is null || sameScope(other)));

        if (duplicate)
        {
            if (!creating)
                item.Name = previousName;
            return new InventoryCatalogMutationResult(InventoryCatalogMutationStatus.Conflict, Error: $"Ya existe un registro llamado \"{name}\".");
        }

        if (creating)
        {
            item.CreatedAt = Now();
            set.Add(item);
        }
        else
        {
            item.UpdatedAt = Now();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new InventoryCatalogMutationResult(
            creating ? InventoryCatalogMutationStatus.Created : InventoryCatalogMutationStatus.Ok,
            item);
    }

    private async Task<InventoryCatalogMutationResult> SoftDeleteAsync<T>(
        DbSet<T> set,
        string id,
        Func<Task<string?>> usage,
        CancellationToken cancellationToken)
        where T : class, IItamCatalogEntity
    {
        var item = await set.FindAsync([id], cancellationToken);
        if (item is null || item.Deleted == true)
            return new InventoryCatalogMutationResult(InventoryCatalogMutationStatus.NotFound, Error: "El registro no existe o fue eliminado.");

        var inUse = await usage();
        if (inUse is not null)
            return new InventoryCatalogMutationResult(InventoryCatalogMutationStatus.Conflict, Error: $"No se puede eliminar: está en uso por {inUse}.");

        item.Deleted = true;
        item.DeletedAt = Now();
        item.UpdatedAt = item.DeletedAt;
        await _db.SaveChangesAsync(cancellationToken);

        return new InventoryCatalogMutationResult(InventoryCatalogMutationStatus.Ok, item);
    }

    private static string? Usage(int count, string label)
        => count > 0 ? $"{count} {label}" : null;

    private static InventoryCatalogMutationResult Invalid(string error)
        => new(InventoryCatalogMutationStatus.Invalid, Error: error);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Limit(string? value, int maxLength)
    {
        var clean = Clean(value);
        return clean is null || clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private static bool SameText(string? left, string? right)
        => string.Equals(Clean(left), Clean(right), StringComparison.OrdinalIgnoreCase);
}
