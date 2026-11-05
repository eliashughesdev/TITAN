using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Application.Notifications;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Notifications;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Notifications;

public sealed class NotificationFeedServiceTests
{
    private static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        PermissionCodes.Security.View,
        PermissionCodes.Helpdesk.TicketsView,
        PermissionCodes.Helpdesk.TicketsAssign,
        PermissionCodes.Inventory.View,
    };

    [Fact]
    public async Task NonCompliantDevices_AndOverdueTicket_AreReported()
    {
        await using var db = Open();
        var (org, user, site) = await SeedAsync(db);

        var feed = await Create(db).GetFeedAsync(new NotificationFeedRequest(org, user, All, null));

        Assert.Contains(feed.Items, x => x.Category == "security" && x.Severity == NotificationSeverity.Critical);
        Assert.Contains(feed.Items, x => x.Id.StartsWith("ticket-sla:") && x.Link!.StartsWith("/helpdesk/tickets/"));
        Assert.Equal(NotificationSeverity.Critical, feed.Items[0].Severity);
    }

    [Fact]
    public async Task WithoutPermissions_FeedIsEmpty()
    {
        await using var db = Open();
        var (org, user, _) = await SeedAsync(db);

        var feed = await Create(db).GetFeedAsync(
            new NotificationFeedRequest(org, user, new HashSet<string>(), null));

        Assert.Empty(feed.Items);
    }

    [Fact]
    public async Task SiteScopedUser_DoesNotSeeOtherSites()
    {
        await using var db = Open();
        var (org, user, _) = await SeedAsync(db);

        var feed = await Create(db).GetFeedAsync(
            new NotificationFeedRequest(org, user, All, new[] { Guid.NewGuid() }));

        Assert.DoesNotContain(feed.Items, x => x.Category is "security" or "helpdesk");
    }

    [Fact]
    public async Task FailingSource_IsReportedAsUnavailable_WithoutBreakingFeed()
    {
        await using var db = Open();
        var (org, user, _) = await SeedAsync(db);

        var service = new NotificationFeedService(
            db,
            NullLogger<NotificationFeedService>.Instance,
            ThrowingProxy<IInventoryService>.Create());

        var feed = await service.GetFeedAsync(new NotificationFeedRequest(org, user, All, null));

        Assert.Contains("inventory", feed.UnavailableSources);
        Assert.Contains(feed.Items, x => x.Category == "security");
    }

    private static NotificationFeedService Create(TitanMdmDbContext db) =>
        new(db, NullLogger<NotificationFeedService>.Instance);

    private static async Task<(Guid Org, Guid User, Guid Site)> SeedAsync(TitanMdmDbContext db)
    {
        var org = Guid.NewGuid();
        var site = Guid.NewGuid();
        var user = new User(org, "Tecnico", "Uno", "tecnico@titanmdm.local");

        var device = new Device(org, "WIN-1", DevicePlatform.Windows, "WIN-1");
        device.AssignSite(site);

        var posture = new DeviceSecurityPosture(org, device.Id);
        posture.UpdateCompliance(40, "Critical", "NonCompliant", 7, 3, 4, "[]");

        var ticket = new HelpdeskTicket(
            org, "HD-1", "Sin red", "No conecta", "incident", "high",
            "network", "portal", user.Id, device.Id, null);
        ticket.AssignSite(site, null);
        ticket.Assign(user.Id);
        ticket.ApplySla(DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-1));

        db.Users.Add(user);
        db.Devices.Add(device);
        db.DeviceSecurityPostures.Add(posture);
        db.HelpdeskTickets.Add(ticket);
        await db.SaveChangesAsync();

        return (org, user.Id, site);
    }

    private static TitanMdmDbContext Open() =>
        new(new DbContextOptionsBuilder<TitanMdmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    public class ThrowingProxy<T> : DispatchProxy
        where T : class
    {
        public static T Create() => Create<T, ThrowingProxy<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("ITAM no disponible.");
    }
}
