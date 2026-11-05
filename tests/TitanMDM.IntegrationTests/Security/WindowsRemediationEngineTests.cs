using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Commands;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Commands;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Infrastructure.Security;

namespace TitanMDM.IntegrationTests.Security;

public sealed class WindowsRemediationEngineTests
{
    [Fact]
    public async Task FailedDefenderAndFirewall_QueueServiceStartsAndVerification()
    {
        await using var db = Open();
        var (organizationId, device) = await SeedAsync(db);
        var engine = CreateEngine(db);

        var result = await engine.RemediateAsync(
            organizationId,
            device.Id,
            new[] { "DEFENDER_AVAILABLE", "FIREWALL_AVAILABLE", "SECURE_BOOT" });

        Assert.True(result.Enabled);
        Assert.Equal(3, result.Queued.Count);
        Assert.Contains(result.Manual, x => x.CheckCode == "SECURE_BOOT");

        var commands = await db.DeviceCommands.AsNoTracking().ToListAsync();
        Assert.Equal(2, commands.Count(x => x.CommandType == "SERVICE_START"));
        Assert.Contains(commands, x => x.PayloadJson.Contains("WinDefend"));
        Assert.Contains(commands, x => x.PayloadJson.Contains("mpssvc"));
        Assert.Single(commands, x => x.CommandType == "COMPLIANCE_CHECK");
    }

    [Fact]
    public async Task RepeatedFailure_WithinCooldown_DoesNotLoop()
    {
        await using var db = Open();
        var (organizationId, device) = await SeedAsync(db);
        var engine = CreateEngine(db);

        await engine.RemediateAsync(organizationId, device.Id, new[] { "DEFENDER_AVAILABLE" });
        var second = await engine.RemediateAsync(organizationId, device.Id, new[] { "DEFENDER_AVAILABLE" });

        Assert.Empty(second.Queued);
        Assert.Single(second.Suppressed);
        Assert.Equal(2, await db.DeviceCommands.CountAsync());
    }

    [Fact]
    public async Task ManualOnlyFailures_DoNotQueueCommands()
    {
        await using var db = Open();
        var (organizationId, device) = await SeedAsync(db);
        var engine = CreateEngine(db);

        var result = await engine.RemediateAsync(
            organizationId,
            device.Id,
            new[] { "NO_PENDING_REBOOT", "BITLOCKER_STATUS" });

        Assert.Empty(result.Queued);
        Assert.Equal(2, result.Manual.Count);
        Assert.Equal(0, await db.DeviceCommands.CountAsync());
    }

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        await using var db = Open();
        var (organizationId, device) = await SeedAsync(db);
        var engine = CreateEngine(db, new WindowsRemediationOptions { Enabled = false });

        var result = await engine.RemediateAsync(organizationId, device.Id, new[] { "DEFENDER_AVAILABLE" });

        Assert.False(result.Enabled);
        Assert.Equal(0, await db.DeviceCommands.CountAsync());
    }

    [Fact]
    public async Task AndroidDevice_IsIgnored()
    {
        await using var db = Open();
        var (organizationId, _) = await SeedAsync(db);
        var android = new Device(organizationId, "Android-1", DevicePlatform.Android, "AND-1");
        db.Devices.Add(android);
        await db.SaveChangesAsync();

        var result = await CreateEngine(db).RemediateAsync(
            organizationId,
            android.Id,
            new[] { "DEFENDER_AVAILABLE" });

        Assert.Empty(result.Queued);
        Assert.Equal(0, await db.DeviceCommands.CountAsync());
    }

    [Fact]
    public async Task WithoutSuperAdmin_SkipsSafely()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var device = new Device(organizationId, "WIN-1", DevicePlatform.Windows, "WIN-1");
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var result = await CreateEngine(db).RemediateAsync(
            organizationId,
            device.Id,
            new[] { "DEFENDER_AVAILABLE" });

        Assert.Empty(result.Queued);
        Assert.NotNull(result.SkippedReason);
    }

    private static WindowsRemediationEngine CreateEngine(
        TitanMdmDbContext db,
        WindowsRemediationOptions? options = null)
    {
        return new WindowsRemediationEngine(
            db,
            new DeviceCommandService(db, new NullNotifier()),
            Options.Create(options ?? new WindowsRemediationOptions()),
            NullLogger<WindowsRemediationEngine>.Instance);
    }

    private static async Task<(Guid OrganizationId, Device Device)> SeedAsync(
        TitanMdmDbContext db)
    {
        var organizationId = Guid.NewGuid();

        var user = new User(organizationId, "Super", "Admin", "superadmin@titanmdm.local");
        var role = new Role(organizationId, "SuperAdmin");
        var device = new Device(organizationId, "WIN-1", DevicePlatform.Windows, "WIN-1");

        db.Users.Add(user);
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole(user.Id, role.Id));
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        return (organizationId, device);
    }

    private static TitanMdmDbContext Open()
    {
        var options = new DbContextOptionsBuilder<TitanMdmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TitanMdmDbContext(options);
    }

    private sealed class NullNotifier : IDeviceCommandNotifier
    {
        public Task NotifyAvailableAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyUpdatedAsync(DeviceCommandDto command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
