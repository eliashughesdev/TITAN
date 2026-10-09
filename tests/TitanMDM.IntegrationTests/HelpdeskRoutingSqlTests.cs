using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

// Set TITAN_TEST_SQL to a SQL Server connection with permission to create a
// disposable database. InitialCatalog is always replaced; operational data is
// never used. These are relational tests, not EF InMemory concurrency simulations.
public sealed class SqlRoutingFactAttribute : FactAttribute
{
    public SqlRoutingFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TITAN_TEST_SQL")))
            Skip = "Set TITAN_TEST_SQL to run isolated SQL Server routing tests.";
    }
}

public sealed class RoutingSqlFixture : IAsyncLifetime
{
    private string? _connection;
    public TitanMdmDbContext Open() => new(new DbContextOptionsBuilder<TitanMdmDbContext>()
        .UseSqlServer(_connection!, sql => sql.EnableRetryOnFailure()).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("TITAN_TEST_SQL");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var builder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = $"TitanMDM_RoutingTests_{Guid.NewGuid():N}"
        };
        _connection = builder.ConnectionString;
        await using var db = Open();
        // Current-model schema only for this disposable test database. Production
        // schema continues to be managed exclusively by versioned migrations.
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        var catalog = new SqlConnectionStringBuilder(_connection).InitialCatalog;
        if (!catalog.StartsWith("TitanMDM_RoutingTests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to drop a non-test database.");
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
    }
}

public sealed class HelpdeskRoutingSqlTests(RoutingSqlFixture fixture) : IClassFixture<RoutingSqlFixture>
{
    [SqlRoutingFact]
    public async Task OneEligibleTechnicianReceivesTicket()
    {
        var s = await SeedAsync();
        Assert.True(await AssignAsync(s));
        await AssertAssignedAsync(s, s.Technicians[0]);
    }

    [SqlRoutingFact]
    public async Task FifteenEquivalentTechniciansShareThirtyTickets()
    {
        var s = await SeedAsync(15);
        for (var i = 0; i < 30; i++)
        {
            var id = i == 0 ? s.TicketId : await AddTicketAsync(s);
            Assert.True(await AssignAsync(s with { TicketId = id }));
        }
        await using var db = fixture.Open();
        var counts = await db.HelpdeskTickets.Where(x => x.OrganizationId == s.Org)
            .GroupBy(x => x.AssigneeUserId).Select(g => g.Count()).ToListAsync();
        Assert.Equal(15, counts.Count);
        Assert.All(counts, count => Assert.Equal(2, count));
    }

    [SqlRoutingFact]
    public async Task OutOfShiftTechnicianIsRejectedEvenByFallback()
    {
        var s = await SeedAsync();
        await using var db = fixture.Open();
        var schedule = new HelpdeskTechnicianSchedule(s.Org, s.Team, s.Technicians[0]);
        schedule.Configure(1, true, "UTC",
            [new HelpdeskWeeklySlot(((int)DateTime.UtcNow.DayOfWeek + 2) % 7, "08:00", "09:00")]);
        db.Add(schedule);
        await db.SaveChangesAsync();
        Assert.False(await AssignAsync(s));
        await AssertAssignedAsync(s, null);
    }

    [SqlRoutingFact]
    public async Task FullCapacityIsNeverOverridden()
    {
        var s = await SeedAsync(capacity: 1);
        Assert.True(await AssignAsync(s));
        var next = s with { TicketId = await AddTicketAsync(s) };
        Assert.False(await AssignAsync(next));
        await AssertAssignedAsync(next, null);
    }

    [SqlRoutingFact]
    public async Task DisabledTechnicianCannotReceiveTicket()
    {
        var s = await SeedAsync();
        await using var db = fixture.Open();
        await db.Users.Where(x => x.Id == s.Technicians[0])
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, false));
        Assert.False(await AssignAsync(s));
    }

    [SqlRoutingFact]
    public async Task ExplicitCoverageDoesNotFallBackOutsideItsSite()
    {
        var s = await SeedAsync();
        await using var db = fixture.Open();
        var other = new Site(s.Org, "OTHER", "Other site");
        db.Add(other);
        db.Add(new HelpdeskSiteCoverage(s.Org, s.Team, other.Id, null, null));
        await db.SaveChangesAsync();
        Assert.False(await AssignAsync(s));
    }

    [SqlRoutingFact]
    public async Task NoExplicitCoverageIncludesActiveSublocations()
    {
        var s = await SeedAsync();
        await using var db = fixture.Open();
        // Site-level implicit coverage also matches a sublocation; use the
        // domain constructor rather than bypassing relational foreign keys.
        var location = new SiteLocation(s.Org, s.Site, "Floor 1");
        db.Add(location);
        await db.SaveChangesAsync();
        await db.HelpdeskTickets.Where(x => x.Id == s.TicketId)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.SiteLocationId, location.Id));
        Assert.True(await AssignAsync(s));
    }

    [SqlRoutingFact]
    public async Task GeneralCategoryCanUseSpecializedGroup()
    {
        var s = await SeedAsync(category: "general");
        Assert.True(await AssignAsync(s));
    }

    [SqlRoutingFact]
    public async Task DeterministicFallbackDoesNotRequireAi()
    {
        var s = await SeedAsync(category: "general");
        // The service has no AI dependency. No provider or key is registered.
        Assert.True(await AssignAsync(s));
    }

    [SqlRoutingFact]
    public async Task ConcurrentWorkersAssignOnceAndEmitOneEvent()
    {
        var s = await SeedAsync();
        var results = await Task.WhenAll(AssignAsync(s), AssignAsync(s));
        Assert.Single(results, x => x);
        await using var db = fixture.Open();
        Assert.Equal(1, await db.HelpdeskTicketEvents.CountAsync(
            x => x.TicketId == s.TicketId && x.EventType == "auto_assigned"));
    }

    [SqlRoutingFact]
    public async Task ConcurrentDifferentTicketsCannotOverbookTechnician()
    {
        var s = await SeedAsync(capacity: 1);
        var second = s with { TicketId = await AddTicketAsync(s) };
        var results = await Task.WhenAll(AssignAsync(s), AssignAsync(second));
        Assert.Single(results, x => x);
    }

    [SqlRoutingFact]
    public async Task AssignedTicketIsIdempotent()
    {
        var s = await SeedAsync();
        Assert.True(await AssignAsync(s));
        Assert.False(await AssignAsync(s));
        await AssertAssignedAsync(s, s.Technicians[0]);
    }

    [SqlRoutingFact]
    public async Task PendingUserTicketIsNotAssigned()
    {
        var s = await SeedAsync();
        await using var db = fixture.Open();
        var ticket = await db.HelpdeskTickets.SingleAsync(x => x.Id == s.TicketId);
        ticket.Transition("pendinguser");
        await db.SaveChangesAsync();
        Assert.False(await AssignAsync(s));
        await AssertAssignedAsync(s, null);
    }

    [SqlRoutingFact]
    public async Task SpecializedCategoryCannotRouteToIncompatibleGroup()
    {
        var s = await SeedAsync(category: "payroll");
        Assert.False(await AssignAsync(s));
    }

    [SqlRoutingFact]
    public async Task MissingOperationalPermissionRejectsTechnician()
    {
        var s = await SeedAsync();
        await using var db = fixture.Open();
        await db.UserRoles.Where(x => x.UserId == s.Technicians[0]).ExecuteDeleteAsync();
        Assert.False(await AssignAsync(s));
    }

    private async Task<bool> AssignAsync(Scenario s)
    {
        await using var db = fixture.Open();
        var service = new HelpdeskService(db, new HelpdeskTicketNumberGenerator(db));
        return await service.RetryAutomaticAssignmentWithFallbackAsync(s.Org, s.TicketId);
    }

    private async Task AssertAssignedAsync(Scenario s, Guid? expected)
    {
        await using var db = fixture.Open();
        Assert.Equal(expected, await db.HelpdeskTickets.Where(x => x.Id == s.TicketId)
            .Select(x => x.AssigneeUserId).SingleAsync());
    }

    private async Task<Scenario> SeedAsync(int count = 1, int capacity = 20, string category = "network")
    {
        await using var db = fixture.Open();
        var org = new Organization("Routing test", Guid.NewGuid().ToString("N")[..12]);
        var site = new Site(org.Id, "HQ", "Headquarters");
        var requester = new User(org.Id, "Test", "Requester", $"{Guid.NewGuid():N}@example.test");
        requester.SetSite(site.Id);
        var team = new HelpdeskTeam(org.Id, "Network", null);
        team.ConfigureCategories(["network"]);
        var role = new Role(org.Id, "Technician");
        var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Code == "helpdesk.ticket.assign");
        if (permission is null)
        {
            permission = new Permission("helpdesk.ticket.assign", "Assign", "helpdesk");
            db.Add(permission);
        }
        db.AddRange(org, site, requester, team, role, new RolePermission(role.Id, permission.Id));
        var technicians = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var user = new User(org.Id, "Tech", i.ToString(), $"{Guid.NewGuid():N}@example.test");
            db.AddRange(user, new UserRole(user.Id, role.Id),
                new HelpdeskTeamMember(org.Id, team.Id, user.Id, true, capacity));
            technicians.Add(user.Id);
        }
        var ticket = Ticket(org.Id, requester.Id, site.Id, category);
        db.Add(ticket);
        await db.SaveChangesAsync();
        return new Scenario(org.Id, site.Id, team.Id, requester.Id, ticket.Id, technicians.ToArray());
    }

    private async Task<Guid> AddTicketAsync(Scenario s)
    {
        await using var db = fixture.Open();
        var ticket = Ticket(s.Org, s.Requester, s.Site, "network");
        db.Add(ticket);
        await db.SaveChangesAsync();
        return ticket.Id;
    }

    private static HelpdeskTicket Ticket(Guid org, Guid requester, Guid site, string category)
    {
        var ticket = new HelpdeskTicket(org, $"HD-{Guid.NewGuid():N}"[..20],
            "Test request", "Routing validation", "incident", "medium", category,
            "portal", requester, null, null);
        ticket.AssignSite(site, null);
        return ticket;
    }

    private sealed record Scenario(Guid Org, Guid Site, Guid Team, Guid Requester,
        Guid TicketId, Guid[] Technicians);
}
