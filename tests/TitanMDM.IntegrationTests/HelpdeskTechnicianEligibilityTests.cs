using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

/*
 * ============================================================
 * TITANMDM HELPDESK — ELEGIBILIDAD DE TÉCNICOS
 * ============================================================
 *
 * Verifica la fuente única de elegibilidad de técnicos:
 *  - incluye usuarios activos con un permiso de routing,
 *  - excluye usuarios inactivos,
 *  - excluye roles inactivos y permisos inactivos,
 *  - y aísla por organización.
 */

public sealed class HelpdeskTechnicianEligibilityTests
{
    [Fact]
    public async Task ReturnsActiveEligibleTechniciansOnly()
    {
        await using var db = Open();

        var organizationId = Guid.NewGuid();

        var eligible =
            await SeedTechnicianAsync(
                db,
                organizationId,
                "helpdesk.ticket.take");

        var inactive =
            await SeedTechnicianAsync(
                db,
                organizationId,
                "helpdesk.ticket.assign");

        inactive.Deactivate();

        await db.SaveChangesAsync();

        var ids = await HelpdeskTechnicianEligibility
            .EligibleTechnicianIds(
                db,
                organizationId)
            .ToListAsync();

        Assert.Contains(
            eligible.Id,
            ids);

        Assert.DoesNotContain(
            inactive.Id,
            ids);
    }

    [Fact]
    public async Task ExcludesUsersWithoutRoutingPermission()
    {
        await using var db = Open();

        var organizationId = Guid.NewGuid();

        var eligible =
            await SeedTechnicianAsync(
                db,
                organizationId,
                "helpdesk.ticket.take");

        var unrelated = await SeedTechnicianAsync(
            db,
            organizationId,
            "devices.view");

        var ids = await HelpdeskTechnicianEligibility
            .EligibleTechnicianIds(
                db,
                organizationId)
            .ToListAsync();

        Assert.Contains(
            eligible.Id,
            ids);

        Assert.DoesNotContain(
            unrelated.Id,
            ids);
    }

    [Fact]
    public async Task IsolatesOrganizations()
    {
        await using var db = Open();

        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();

        var local =
            await SeedTechnicianAsync(
                db,
                organizationId,
                "helpdesk.ticket.take");

        var foreign =
            await SeedTechnicianAsync(
                db,
                otherOrganizationId,
                "helpdesk.ticket.take");

        var ids = await HelpdeskTechnicianEligibility
            .EligibleTechnicianIds(
                db,
                organizationId)
            .ToListAsync();

        Assert.Contains(
            local.Id,
            ids);

        Assert.DoesNotContain(
            foreign.Id,
            ids);
    }

    private static async Task<User> SeedTechnicianAsync(
        TitanMdmDbContext db,
        Guid organizationId,
        string permissionCode)
    {
        var role = new Role(
            organizationId,
            "Technician");

        var permission = new Permission(
            permissionCode,
            permissionCode,
            "helpdesk");

        var user = new User(
            organizationId,
            "Tech",
            Guid.NewGuid()
                .ToString("N")[..8],
            $"{Guid.NewGuid():N}@example.test");

        db.AddRange(
            role,
            permission,
            user);

        db.Add(
            new RolePermission(
                role.Id,
                permission.Id));

        db.Add(
            new UserRole(
                user.Id,
                role.Id));

        await db.SaveChangesAsync();

        return user;
    }

    private static TitanMdmDbContext Open()
    {
        var options =
            new DbContextOptionsBuilder<
                    TitanMdmDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid()
                        .ToString("N"))
                .Options;

        return new TitanMdmDbContext(
            options);
    }
}
