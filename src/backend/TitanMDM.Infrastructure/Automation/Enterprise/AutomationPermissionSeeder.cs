using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TitanMDM.Application.Automation;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise;

/*
 * AUT-1.8 — Sembrado de permisos del motor.
 *
 * No modifica TitanMdmSeeder.cs (archivo compartido). Inserta
 * de forma idempotente los permisos de automatización y los
 * concede a los roles de sistema "SuperAdmin" activos, igual
 * que hace el seeder principal con el resto de permisos.
 *
 * Se ejecuta como hosted service al arrancar. Si la base de
 * datos todavía no está disponible, se registra y se ignora
 * para no impedir el arranque.
 */
internal sealed class AutomationPermissionSeeder
    : IHostedService
{
    private const string SuperAdminRoleName =
        "SuperAdmin";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationPermissionSeeder>
        _logger;

    public AutomationPermissionSeeder(
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationPermissionSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope =
                _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<TitanMdmDbContext>();

            await SeedAsync(
                db,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Automation permission seeding was skipped.");
        }
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task SeedAsync(
        TitanMdmDbContext db,
        CancellationToken cancellationToken)
    {
        var codes = AutomationPermissions.All
            .Select(x => x.Code.ToLowerInvariant())
            .ToArray();

        var existing = await db.Permissions
            .Where(x => codes.Contains(x.Code))
            .ToDictionaryAsync(
                x => x.Code,
                cancellationToken);

        var created =
            new List<Permission>();

        foreach (var definition in
            AutomationPermissions.All)
        {
            var code = definition.Code
                .ToLowerInvariant();

            if (existing.ContainsKey(code))
                continue;

            var permission = new Permission(
                definition.Code,
                definition.Name,
                definition.Module,
                definition.Description);

            db.Permissions.Add(permission);
            created.Add(permission);
        }

        if (created.Count > 0)
        {
            await db.SaveChangesAsync(
                cancellationToken);
        }

        var all = existing.Values
            .Concat(created)
            .ToArray();

        var superAdminRoleIds = await db.Roles
            .Where(x =>
                x.IsSystemRole &&
                x.IsActive &&
                x.Name == SuperAdminRoleName)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (superAdminRoleIds.Count == 0)
            return;

        var permissionIds = all
            .Select(x => x.Id)
            .ToArray();

        var granted = await db.RolePermissions
            .Where(x =>
                superAdminRoleIds.Contains(x.RoleId) &&
                permissionIds.Contains(x.PermissionId))
            .Select(x => new { x.RoleId, x.PermissionId })
            .ToListAsync(cancellationToken);

        var grantedSet = granted
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        foreach (var roleId in superAdminRoleIds)
        {
            foreach (var permission in all)
            {
                if (grantedSet.Contains(
                        (roleId, permission.Id)))
                    continue;

                db.RolePermissions.Add(
                    new RolePermission(
                        roleId,
                        permission.Id));
            }
        }

        await db.SaveChangesAsync(
            cancellationToken);
    }
}
