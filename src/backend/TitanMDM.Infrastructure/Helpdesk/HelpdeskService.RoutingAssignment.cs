namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // Preserve the legacy entry point while using the same identity, schedule,
    // capacity and transaction rules as the worker and administrative retry.
    public Task<bool> RetryAutomaticAssignmentAsync(
        Guid organizationId,
        Guid ticketId,
        CancellationToken cancellationToken = default) =>
        RetryAutomaticAssignmentEnterpriseAsync(organizationId, ticketId, cancellationToken);
}
