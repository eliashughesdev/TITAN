using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Commands;
using TitanMDM.Application.LostMode;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Application.Automation;

namespace TitanMDM.Infrastructure.LostMode;

public sealed class LostModeService
    : ILostModeService
{
    private readonly TitanMdmDbContext _db;
    private readonly IDeviceCommandService _commands;

    private readonly IAutomationEventDispatcher
    _automation;

  public LostModeService(
    TitanMdmDbContext db,
    IDeviceCommandService commands,
    IAutomationEventDispatcher automation)
{
    _db = db;
    _commands = commands;
    _automation = automation;
}

    public async Task<LostModeDto>
        ActivateAsync(
            Guid organizationId,
            Guid userId,
            ActivateLostModeRequest request,
            CancellationToken cancellationToken = default)
    {
        var device =
            await _db.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.DeviceId &&
                        x.OrganizationId ==
                            organizationId &&
                        !x.IsDeleted,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Device not found.");

        var active =
            await _db.LostModeSessions
                .Where(x =>
                    x.OrganizationId ==
                        organizationId &&
                    x.DeviceId ==
                        device.Id &&
                    x.Status == "Active")
                .ToListAsync(
                    cancellationToken);

        foreach (var session in active)
            session.Deactivate();

        var lostMode =
            new LostModeSession(
                organizationId,
                device.Id,
                userId,
                request.Message,
                request.PhoneNumber);

        _db.LostModeSessions.Add(
            lostMode);

        await _db.SaveChangesAsync(
            cancellationToken);

        var payload =
            JsonSerializer.Serialize(
                new
                {
                    message =
                        lostMode.Message,

                    phoneNumber =
                        lostMode.PhoneNumber
                });

        await _commands.CreateAsync(
            organizationId,
            userId,
            new CreateDeviceCommandRequest(
                device.Id,
                "LOST_MODE_ENABLE",
                payload,
                1440),
            cancellationToken);

        await _commands.CreateAsync(
            organizationId,
            userId,
            new CreateDeviceCommandRequest(
                device.Id,
                "LOCATION_REQUEST",
                "{}",
                60),
            cancellationToken);
        
        await _automation.DispatchAsync(
    organizationId,
    device.Id,
    "LostModeActivated",
    new
    {
        platform =
            device.Platform.ToString(),

        deviceName =
            device.DeviceName,

        sessionId =
            lostMode.Id,

        message =
            lostMode.Message,

        phoneNumber =
            lostMode.PhoneNumber
    },
    userId,
    cancellationToken);

        return Map(lostMode);
    }

    public async Task<LostModeDto?>
        GetActiveAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        return await _db.LostModeSessions
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId ==
                    organizationId &&
                x.DeviceId ==
                    deviceId &&
                x.Status == "Active")
            .OrderByDescending(
                x => x.ActivatedAtUtc)
            .Select(x =>
                new LostModeDto(
                    x.Id,
                    x.DeviceId,
                    x.Message,
                    x.PhoneNumber,
                    x.Status,
                    x.ActivatedAtUtc,
                    x.DeactivatedAtUtc))
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public async Task DeactivateAsync(
        Guid organizationId,
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var active =
            await _db.LostModeSessions
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId &&
                        x.DeviceId ==
                            deviceId &&
                        x.Status ==
                            "Active",
                    cancellationToken);

        if (active is null)
            return;

        active.Deactivate();

        await _db.SaveChangesAsync(
            cancellationToken);

        await _commands.CreateAsync(
            organizationId,
            userId,
            new CreateDeviceCommandRequest(
                deviceId,
                "LOST_MODE_DISABLE",
                "{}",
                60),
            cancellationToken);
    }

    private static LostModeDto Map(
        LostModeSession session)
    {
        return new LostModeDto(
            session.Id,
            session.DeviceId,
            session.Message,
            session.PhoneNumber,
            session.Status,
            session.ActivatedAtUtc,
            session.DeactivatedAtUtc);
    }
}