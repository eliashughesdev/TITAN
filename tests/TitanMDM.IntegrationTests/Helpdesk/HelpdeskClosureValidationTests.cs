using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Helpdesk;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Helpdesk;

public sealed class HelpdeskClosureValidationTests
{
    [Fact]
    public async Task Resolve_WithInFlightDeviceCommand_IsRejected()
    {
        await using var db = Open();
        var (org, actor, ticket, device) = await SeedAsync(db);

        db.DeviceCommands.Add(
            new DeviceCommand(org, device.Id, "SOFTWARE_INSTALL", "{}", actor, DateTime.UtcNow.AddMinutes(30)));
        await db.SaveChangesAsync();

        var service = Create(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransitionAsync(org, ticket.Id, actor, new TransitionHelpdeskTicketRequest("resolved")));

        Assert.Contains("SOFTWARE_INSTALL", error.Message);

        var status = await db.HelpdeskTickets.AsNoTracking()
            .Where(x => x.Id == ticket.Id).Select(x => x.Status).SingleAsync();
        Assert.Equal("open", status);
    }

    [Fact]
    public async Task CompletedOrExpiredCommands_DoNotBlock()
    {
        await using var db = Open();
        var (org, actor, ticket, device) = await SeedAsync(db);

        var done = new DeviceCommand(org, device.Id, "PING", "{}", actor, DateTime.UtcNow.AddMinutes(30));
        done.Cancel();
        db.DeviceCommands.Add(done);
        await db.SaveChangesAsync();

        var blockers = await Create(db).GetPendingClosureBlockersAsync(ticket, CancellationToken.None);

        Assert.Empty(blockers);
    }

    [Fact]
    public async Task ActiveRemoteSession_BlocksClosure()
    {
        await using var db = Open();
        var (org, actor, ticket, device) = await SeedAsync(db);

        var session = new RemoteSession(
            org, device.Id, actor, "Tecnico", "Soporte", true, true, false, false, DateTime.UtcNow.AddHours(1));
        db.RemoteSessions.Add(session);
        ticket.LinkRemoteSession(session.Id);
        await db.SaveChangesAsync();

        var blockers = await Create(db).GetPendingClosureBlockersAsync(ticket, CancellationToken.None);

        Assert.Contains(blockers, x => x.Contains("sesión de soporte remoto"));
    }

    [Fact]
    public async Task TicketWithoutPendingWork_HasNoBlockers()
    {
        await using var db = Open();
        var (_, _, ticket, _) = await SeedAsync(db);

        var blockers = await Create(db).GetPendingClosureBlockersAsync(ticket, CancellationToken.None);

        Assert.Empty(blockers);
    }

    private static HelpdeskService Create(TitanMdmDbContext db) =>
        new(db, new HelpdeskTicketNumberGenerator(db));

    private static async Task<(Guid Org, Guid Actor, HelpdeskTicket Ticket, Device Device)> SeedAsync(
        TitanMdmDbContext db)
    {
        var org = Guid.NewGuid();
        var actor = new User(org, "Tecnico", "Uno", "tecnico@titanmdm.local");
        var device = new Device(org, "WIN-HD-1", DevicePlatform.Windows, "WIN-HD-1");

        var ticket = new HelpdeskTicket(
            org, "HD-TEST-1", "Equipo lento", "Revisar", "incident", "medium",
            "hardware", "portal", actor.Id, device.Id, null);
        ticket.Transition("open");

        db.Users.Add(actor);
        db.Devices.Add(device);
        db.HelpdeskTickets.Add(ticket);
        await db.SaveChangesAsync();

        return (org, actor.Id, ticket, device);
    }

    private static TitanMdmDbContext Open()
    {
        var options = new DbContextOptionsBuilder<TitanMdmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TitanMdmDbContext(options);
    }
}
