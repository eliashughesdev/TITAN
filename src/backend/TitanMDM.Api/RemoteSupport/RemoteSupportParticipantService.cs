using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.RemoteSupport;

public sealed class RemoteSupportParticipantService
{
    private readonly TitanMdmDbContext _dbContext;

    public RemoteSupportParticipantService(
        TitanMdmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RemoteSessionParticipant>
        EnsureAsync(
            Guid organizationId,
            Guid sessionId,
            Guid userId,
            string displayName,
            CancellationToken cancellationToken = default)
    {
        var participant =
            await _dbContext
                .RemoteSessionParticipants
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.RemoteSessionId == sessionId
                        &&
                        x.UserId == userId,
                    cancellationToken);

        if (participant is not null)
        {
            return participant;
        }

        participant =
            new RemoteSessionParticipant(
                organizationId,
                sessionId,
                userId,
                displayName,
                canControl: false);

        _dbContext
            .RemoteSessionParticipants
            .Add(participant);

        return participant;
    }

    public async Task ConnectAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var participant =
            await EnsureAsync(
                organizationId,
                sessionId,
                userId,
                displayName,
                cancellationToken);

        participant.MarkConnected();

        _dbContext.RemoteSessionEvents.Add(
            new RemoteSessionEvent(
                organizationId,
                sessionId,
                "PARTICIPANT_CONNECTED",
                $"{displayName} se conectó a la sesión remota.",
                userId));

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DisconnectAsync(
        Guid organizationId,
        Guid sessionId,
        Guid userId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var participant =
            await _dbContext
                .RemoteSessionParticipants
                .FirstOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId
                        &&
                        x.RemoteSessionId == sessionId
                        &&
                        x.UserId == userId,
                    cancellationToken);

        if (participant is not null)
        {
            participant.MarkDisconnected();
            participant.RevokeControl();
        }

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

        if (lease is not null)
        {
            _dbContext
                .RemoteSessionControlLeases
                .Remove(lease);
        }

        _dbContext.RemoteSessionEvents.Add(
            new RemoteSessionEvent(
                organizationId,
                sessionId,
                "PARTICIPANT_DISCONNECTED",
                $"{displayName} salió de la sesión remota.",
                userId));

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}