using Microsoft.AspNetCore.SignalR;
using TitanMDM.Api.Hubs;

namespace TitanMDM.Api.Services;

public sealed class RemoteSupportNotifier
{
    private readonly IHubContext<
        RemoteSupportHub> _hubContext;

    public RemoteSupportNotifier(
        IHubContext<RemoteSupportHub> hubContext)
    {
        _hubContext =
            hubContext;
    }

    public Task SessionUpdatedAsync(
        Guid organizationId,
        Guid sessionId,
        object payload,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(
                RemoteSupportHub.SessionGroup(
                    organizationId,
                    sessionId))
            .SendAsync(
                "RemoteSessionUpdated",
                payload,
                cancellationToken);
    }

    public Task OrganizationUpdatedAsync(
        Guid organizationId,
        object payload,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(
                RemoteSupportHub.OrganizationGroup(
                    organizationId))
            .SendAsync(
                "RemoteSessionChanged",
                payload,
                cancellationToken);
    }

    public async Task PublishAsync(
        Guid organizationId,
        Guid sessionId,
        object payload,
        CancellationToken cancellationToken = default)
    {
        await SessionUpdatedAsync(
            organizationId,
            sessionId,
            payload,
            cancellationToken);

        await OrganizationUpdatedAsync(
            organizationId,
            payload,
            cancellationToken);
    }
}