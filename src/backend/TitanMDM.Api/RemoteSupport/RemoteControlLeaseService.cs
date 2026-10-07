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

    public RemoteControlLeaseService(
        TitanMdmDbContext dbContext,
        RemoteSupportParticipantService participants)
    {
        _dbContext = dbContext;
        _participants = participants;
    }

    public async Task<RemoteControlLeaseState>
        AcquireAsync(
            Guid organizationId,
            Guid sessionId,
            Guid userId,
            string displayName,
            CancellationToken cancellationToken = default)
    {
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

                return Map(existing);
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

            return Map(lease);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw new HubException(
                "Otro técnico obtuvo el control de la sesión simultáneamente.");
        }
    }

    public async Task RenewAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
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

            throw new HubException(
                "El lease de control expiró.");
        }

        lease.Renew(
            LeaseDuration);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task ReleaseAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        string displayName,
        CancellationToken cancellationToken = default)
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
                        x.UserId == userId,
                    cancellationToken);

        if (lease is null)
        {
            return;
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
    }

    public async Task RequireOwnershipAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
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
            throw new HubException(
                "La sesión está en modo solo lectura. Solicita el control para utilizar teclado o mouse.");
        }

        if (lease.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await RemoveExpiredAsync(
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
    }

    public async Task<RemoteControlLeaseState>
        GetStateAsync(
            Guid organizationId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        await RemoveExpiredAsync(
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

        return lease is null
            ? RemoteControlLeaseState.Empty
            : Map(lease);
    }

    public async Task RemoveExpiredAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
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