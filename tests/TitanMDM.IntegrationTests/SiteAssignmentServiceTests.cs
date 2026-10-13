using System.Reflection;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Api.Controllers;
using TitanMDM.Api.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Infrastructure.Sites;

namespace TitanMDM.IntegrationTests;

public sealed class SiteAssignmentServiceTests
{
    [Fact]
    public async Task ListsOrganizationUsersAndSearchesTechnicianGroup()
    {
        await using var db = Open();
        var scenario = await SeedAsync(db);
        var team = new HelpdeskTeam(scenario.OrganizationId, "Network", null);
        var technician = new User(
            scenario.OrganizationId,
            "Network",
            "Technician",
            "network.tech@example.test");
        var foreignUser = new User(
            Guid.NewGuid(),
            "Foreign",
            "User",
            "foreign@example.test");
        db.AddRange(
            team,
            technician,
            foreignUser,
            new HelpdeskTeamMember(
                scenario.OrganizationId,
                team.Id,
                technician.Id,
                true,
                10));
        await db.SaveChangesAsync();

        var service = new SiteAssignmentService(db);
        var users = await service.GetUsersAsync(
            scenario.OrganizationId,
            "network");

        var result = Assert.Single(users);
        Assert.Equal(technician.Id, result.Id);
        Assert.True(result.IsTechnician);
        Assert.Contains("Network", result.Groups);
    }

    [Fact]
    public async Task AssignsRequesterToSiteAndLocation()
    {
        await using var db = Open();
        var scenario = await SeedAsync(db);
        var location = new SiteLocation(
            scenario.OrganizationId,
            scenario.SiteId,
            "Floor 1");
        db.Add(location);
        await db.SaveChangesAsync();

        var service = new SiteAssignmentService(db);
        await service.AssignUserAsync(
            scenario.OrganizationId,
            scenario.RequesterId,
            scenario.SiteId,
            location.Id);

        var user = await db.Users.SingleAsync(x => x.Id == scenario.RequesterId);
        Assert.Equal(scenario.SiteId, user.SiteId);
        Assert.Equal(location.Id, user.SiteLocationId);
    }

    [Fact]
    public async Task BulkAssignsTechniciansWithoutChangingGroups()
    {
        await using var db = Open();
        var scenario = await SeedAsync(db);
        var team = new HelpdeskTeam(scenario.OrganizationId, "Support", null);
        var technician = new User(
            scenario.OrganizationId,
            "Tech",
            "One",
            "tech@example.test");
        db.AddRange(
            team,
            technician,
            new HelpdeskTeamMember(
                scenario.OrganizationId,
                team.Id,
                technician.Id,
                true,
                10));
        await db.SaveChangesAsync();

        var service = new SiteAssignmentService(db);
        await service.AssignUsersAsync(
            scenario.OrganizationId,
            [technician.Id],
            scenario.SiteId,
            null);

        Assert.Equal(
            scenario.SiteId,
            (await db.Users.SingleAsync(x => x.Id == technician.Id)).SiteId);
        Assert.True(await db.HelpdeskTeamMembers.AnyAsync(x =>
            x.TeamId == team.Id && x.UserId == technician.Id));
    }

    [Fact]
    public async Task RejectsUserFromAnotherOrganization()
    {
        await using var db = Open();
        var scenario = await SeedAsync(db);
        var otherOrganizationId = Guid.NewGuid();
        var otherUser = new User(
            otherOrganizationId,
            "Other",
            "User",
            "other@example.test");
        db.Add(otherUser);
        await db.SaveChangesAsync();

        var service = new SiteAssignmentService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignUserAsync(
                scenario.OrganizationId,
                otherUser.Id,
                scenario.SiteId,
                null));
    }

    [Fact]
    public async Task RejectsLocationFromAnotherSite()
    {
        await using var db = Open();
        var scenario = await SeedAsync(db);
        var otherSite = new Site(
            scenario.OrganizationId,
            "OTHER",
            "Other site");
        var otherLocation = new SiteLocation(
            scenario.OrganizationId,
            otherSite.Id,
            "Floor 2");
        db.AddRange(otherSite, otherLocation);
        await db.SaveChangesAsync();

        var service = new SiteAssignmentService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignUserAsync(
                scenario.OrganizationId,
                scenario.RequesterId,
                scenario.SiteId,
                otherLocation.Id));
    }

    [Fact]
    public void BulkEndpointRequiresUserAndSiteManagementPermissions()
    {
        var method = typeof(SiteAssignmentsController)
            .GetMethod(nameof(SiteAssignmentsController.AssignUsers));
        var policies = method!
            .GetCustomAttributes<RequirePermissionAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        Assert.Contains("permission:users.manage", policies);
        Assert.Contains("permission:sites.manage", policies);
    }

    private static TitanMdmDbContext Open()
    {
        var options = new DbContextOptionsBuilder<TitanMdmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TitanMdmDbContext(options);
    }

    private static async Task<Scenario> SeedAsync(TitanMdmDbContext db)
    {
        var organizationId = Guid.NewGuid();
        var site = new Site(organizationId, "HQ", "Headquarters");
        var requester = new User(
            organizationId,
            "Test",
            "Requester",
            "requester@example.test");
        db.AddRange(site, requester);
        await db.SaveChangesAsync();
        return new Scenario(organizationId, site.Id, requester.Id);
    }

    private sealed record Scenario(
        Guid OrganizationId,
        Guid SiteId,
        Guid RequesterId);
}
