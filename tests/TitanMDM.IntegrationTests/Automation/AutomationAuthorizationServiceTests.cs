using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Automation;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Automation.Enterprise;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Infrastructure.Security;

namespace TitanMDM.IntegrationTests.Automation;

/*
 * AUT-1.8 — Pruebas de la autorización del motor.
 *
 * Verifica la resolución de permisos por base de datos
 * (incluidas las equivalencias), el fallo cerrado ante
 * actores desconocidos y el aislamiento por localidad.
 */
public sealed class AutomationAuthorizationServiceTests
{
    [Fact]
    public async Task GrantsExactPermission()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.View);

        var service = CreateService(db);

        Assert.True(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.View));
    }

    [Fact]
    public async Task ManageGrantsGranularCapabilities()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.Manage);

        var service = CreateService(db);

        Assert.True(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.View));

        Assert.True(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.Create));

        Assert.True(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.Edit));

        Assert.False(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.Execute));

        Assert.False(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.Simulate));

        // Compatibilidad: Manage conserva la capacidad Admin.
        Assert.True(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.Admin));
    }

    [Fact]
    public async Task AdminGrantsEveryAutomationCapability()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.Admin);

        var service = CreateService(db);

        foreach (var capability in new[]
        {
            AutomationPermissions.View,
            AutomationPermissions.Create,
            AutomationPermissions.Edit,
            AutomationPermissions.Manage,
            AutomationPermissions.Execute,
            AutomationPermissions.Simulate
        })
        {
            Assert.True(
                await service.HasPermissionAsync(
                    scenario.OrganizationId,
                    scenario.UserId,
                    capability));
        }
    }

    [Fact]
    public async Task AdminDoesNotGrantForeignPermissions()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.Admin);

        var service = CreateService(db);

        // Un permiso externo (device commands) no se
        // satisface con automations.admin.
        Assert.False(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                PermissionCodes.Devices.Commands));
    }

    [Fact]
    public async Task DeniesUnknownActorAndMissingPermission()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.View);

        var service = CreateService(db);

        Assert.False(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                Guid.Empty,
                AutomationPermissions.View));

        Assert.False(
            await service.HasPermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.Admin));
    }

    [Fact]
    public async Task EnsurePermissionThrowsWhenMissing()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.View);

        var service = CreateService(db);

        await Assert.ThrowsAsync<
            AutomationAuthorizationException>(
            () => service.EnsurePermissionAsync(
                scenario.OrganizationId,
                scenario.UserId,
                AutomationPermissions.Create));
    }

    [Fact]
    public async Task OrganizationScopeCoversAnySites()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.Create);

        var site = new Site(
            scenario.OrganizationId,
            "HQ",
            "Headquarters");

        db.AddRange(
            site,
            new UserScopeGrant(
                scenario.OrganizationId,
                scenario.UserId,
                AuthorizationScopeType.Organization,
                scenario.OrganizationId,
                scenario.UserId));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Sin sitios explícitos (regla de organización) y con
        // sitios arbitrarios: el alcance de organización cubre.
        await service.EnsureScopeCoversSitesAsync(
            scenario.OrganizationId,
            scenario.UserId,
            Array.Empty<Guid>());

        await service.EnsureScopeCoversSitesAsync(
            scenario.OrganizationId,
            scenario.UserId,
            new[] { site.Id });

        await Assert.ThrowsAsync<
            AutomationAuthorizationException>(
            () => service.EnsureScopeCoversSitesAsync(
                scenario.OrganizationId,
                scenario.UserId,
                new[] { Guid.NewGuid() }));
    }

    [Fact]
    public async Task SiteScopeRejectsForeignSites()
    {
        await using var db = Open();
        var scenario = await SeedUserAsync(
            db,
            AutomationPermissions.Create);

        var allowedSite = new Site(
            scenario.OrganizationId,
            "S1",
            "Allowed");
        var otherSite = new Site(
            scenario.OrganizationId,
            "S2",
            "Other");

        db.AddRange(allowedSite, otherSite);

        db.Add(
            new UserScopeGrant(
                scenario.OrganizationId,
                scenario.UserId,
                AuthorizationScopeType.Site,
                allowedSite.Id,
                scenario.UserId));

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Sitio accesible: permitido.
        await service.EnsureScopeCoversSitesAsync(
            scenario.OrganizationId,
            scenario.UserId,
            new[] { allowedSite.Id });

        // Sitio ajeno: denegado.
        await Assert.ThrowsAsync<
            AutomationAuthorizationException>(
            () => service.EnsureScopeCoversSitesAsync(
                scenario.OrganizationId,
                scenario.UserId,
                new[] { otherSite.Id }));

        // Sin sitios explícitos un alcance regional no puede
        // definir una regla de organización completa.
        await Assert.ThrowsAsync<
            AutomationAuthorizationException>(
            () => service.EnsureScopeCoversSitesAsync(
                scenario.OrganizationId,
                scenario.UserId,
                Array.Empty<Guid>()));
    }

    private static TitanMdmDbContext Open()
    {
        var options =
            new DbContextOptionsBuilder<
                    TitanMdmDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new TitanMdmDbContext(options);
    }

    private static AutomationAuthorizationService
        CreateService(TitanMdmDbContext db)
    {
        return new AutomationAuthorizationService(
            db,
            new ScopeAccessService(db));
    }

    private static async Task<Scenario> SeedUserAsync(
        TitanMdmDbContext db,
        string permissionCode)
    {
        var organizationId = Guid.NewGuid();

        var user = new User(
            organizationId,
            "Test",
            "Administrator",
            $"{Guid.NewGuid():N}@example.test");

        var role = new Role(
            organizationId,
            "AutomationRole");

        var permission = new Permission(
            permissionCode,
            permissionCode,
            "Automation",
            null);

        db.AddRange(user, role, permission);
        await db.SaveChangesAsync();

        db.AddRange(
            new UserRole(user.Id, role.Id),
            new RolePermission(role.Id, permission.Id));

        await db.SaveChangesAsync();

        return new Scenario(
            organizationId,
            user.Id);
    }

    private sealed record Scenario(
        Guid OrganizationId,
        Guid UserId);
}
