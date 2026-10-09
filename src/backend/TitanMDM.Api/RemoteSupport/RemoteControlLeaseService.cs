using System.Data;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.RemoteSupport;

public sealed class RemoteControlLeaseService
{
    public static readonly TimeSpan LeaseDuration =
        TimeSpan.FromSeconds(45);

    private readonly TitanMdmDbContext _dbContext;

    private readonly RemoteSupportParticipantService
        _participants;

    private readonly RemoteSupportConnectionRegistry
        _connections;

    public RemoteControlLeaseService(
        TitanMdmDbContext dbContext,
        RemoteSupportParticipantService participants,
        RemoteSupportConnectionRegistry connections)
    {
        _dbContext = dbContext;
        _participants = participants;
        _connections = connections;
    }

    public async Task<RemoteControlLeaseState>
        AcquireAsync(
            Guid organizationId,
            Guid sessionId,
            Guid userId,
            string displayName,
            CancellationToken cancellationToken = default)
    {
        await using var sessionLock =
            await _connections.LockSessionAsync(
                organizationId,
                sessionId,
                cancellationToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var existing =
                await _dbContext
                    .RemoteSessionControlLeases
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId == organizationId
                            &&
                            x.RemoteSessionId == sessionId,
                        cancellationToken);

            if (existing is not null &&
                existing.ExpiresAtUtc <= DateTime.UtcNow)
            {
                await RemoveLeaseInternalAsync(
                    existing,
                    cancellationToken);

                existing = null;
            }

            if (existing is not null)
            {
                if (existing.UserId != userId)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw new HubException(
                        $"El control está siendo utilizado por {existing.DisplayName}.");
                }

                existing.Renew(
                    LeaseDuration);

                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                var state = Map(existing);
                _connections.SetControlLease(
                    organizationId,
                    sessionId,
                    state);
                return state;
            }

            var participant =
                await _participants.EnsureAsync(
                    organizationId,
                    sessionId,
                    userId,
                    displayName,
                    cancellationToken);

            var lease =
                new RemoteSessionControlLease(
                    organizationId,
                    sessionId,
                    userId,
                    displayName,
                    LeaseDuration);

            participant.GrantControl();

            _dbContext
                .RemoteSessionControlLeases
                .Add(lease);

            _dbContext.RemoteSessionEvents.Add(
                new RemoteSessionEvent(
                    organizationId,
                    sessionId,
                    "CONTROL_ACQUIRED",
                    $"{displayName} obtuvo control de teclado y mouse.",
                    userId));

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            var acquiredState = Map(lease);
            _connections.SetControlLease(
                organizationId,
                sessionId,
                acquiredState);
            return acquiredState;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw new HubException(
                "Otro técnico obtuvo el control de la sesión simultáneamente.");
        }
    }

    public async Task<RemoteControlLeaseState> RenewAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var sessionLock =
            await _connections.LockSessionAsync(
                organizationId,
                sessionId,
                cancellationToken);

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.RemoteSessionId == sessionId
                        &&
                        x.UserId == userId,
                    cancellationToken);

        if (lease is null)
        {
            throw new HubException(
                "El usuario actual no posee el control.");
        }

        if (lease.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await RemoveLeaseInternalAsync(
                lease,
                cancellationToken);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            _connections.RemoveControlLease(
                organizationId,
                sessionId);

            throw new HubException(
                "El lease de control expiró.");
        }

        lease.Renew(
            LeaseDuration);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        var state = Map(lease);
        _connections.SetControlLease(
            organizationId,
            sessionId,
            state);
        return state;
    }

    public async Task<bool> ReleaseAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        await using var sessionLock =
            await _connections.LockSessionAsync(
                organizationId,
                sessionId,
                cancellationToken);

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.RemoteSessionId == sessionId
                        &&
                        x.UserId == userId,
                    cancellationToken);

        if (lease is null)
        {
            return false;
        }

        await RemoveLeaseInternalAsync(
            lease,
            cancellationToken);

        _dbContext.RemoteSessionEvents.Add(
            new RemoteSessionEvent(
                organizationId,
                sessionId,
                "CONTROL_RELEASED",
                $"{displayName} liberó el control remoto.",
                userId));

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _connections.RemoveControlLease(
            organizationId,
            sessionId);

        return true;
    }

    public async Task RequireOwnershipAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var sessionLock =
            await _connections.LockSessionAsync(
                organizationId,
                sessionId,
                cancellationToken);

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.RemoteSessionId == sessionId,
                    cancellationToken);

        if (lease is null)
        {
            _connections.RemoveControlLease(
                organizationId,
                sessionId);

            throw new HubException(
                "La sesión está en modo solo lectura. Solicita el control para utilizar teclado o mouse.");
        }

        if (lease.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await RemoveExpiredInternalAsync(
                organizationId,
                sessionId,
                cancellationToken);

            throw new HubException(
                "El control remoto expiró. Solicita el control nuevamente.");
        }

        if (lease.UserId != userId)
        {
            throw new HubException(
                $"La sesión está siendo controlada por {lease.DisplayName}.");
        }

        _connections.SetControlLease(
            organizationId,
            sessionId,
            Map(lease));
    }

    public async Task<RemoteControlLeaseState>
        GetStateAsync(
            Guid organizationId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        await using var sessionLock =
            await _connections.LockSessionAsync(
                organizationId,
                sessionId,
                cancellationToken);

        await RemoveExpiredInternalAsync(
            organizationId,
            sessionId,
            cancellationToken);

        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.RemoteSessionId == sessionId,
                    cancellationToken);

        var state = lease is null
            ? RemoteControlLeaseState.Empty
            : Map(lease);

        _connections.SetControlLease(
            organizationId,
            sessionId,
            state);

        return state;
    }

    public async Task RemoveExpiredAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var sessionLock =
            await _connections.LockSessionAsync(
                organizationId,
                sessionId,
                cancellationToken);

        await RemoveExpiredInternalAsync(
            organizationId,
            sessionId,
            cancellationToken);
    }

    private async Task RemoveExpiredInternalAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var lease =
            await _dbContext
                .RemoteSessionControlLeases
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.RemoteSessionId == sessionId
                        &&
                        x.ExpiresAtUtc <= DateTime.UtcNow,
                    cancellationToken);

        if (lease is null)
        {
            return;
        }

        await RemoveLeaseInternalAsync(
            lease,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _connections.RemoveControlLease(
            organizationId,
            sessionId);
    }

    private async Task RemoveLeaseInternalAsync(
        RemoteSessionControlLease lease,
        CancellationToken cancellationToken)
    {
        var participant =
            await _dbContext
                .RemoteSessionParticipants
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == lease.OrganizationId
                        &&
                        x.RemoteSessionId == lease.RemoteSessionId
                        &&
                        x.UserId == lease.UserId,
                    cancellationToken);

        participant?.RevokeControl();

        _dbContext
            .RemoteSessionControlLeases
            .Remove(lease);
    }

    private static RemoteControlLeaseState Map(
        RemoteSessionControlLease lease)
    {
        return new RemoteControlLeaseState(
            true,
            lease.UserId,
            lease.DisplayName,
            lease.AcquiredAtUtc,
            lease.ExpiresAtUtc);
    }
}

public sealed record RemoteControlLeaseState(
    bool HasController,
    Guid? UserId,
    string? DisplayName,
    DateTime? AcquiredAtUtc,
    DateTime? ExpiresAtUtc)
{
    public static RemoteControlLeaseState Empty { get; } =
        new(
            false,
            null,
            null,
            null,
            null);
}
