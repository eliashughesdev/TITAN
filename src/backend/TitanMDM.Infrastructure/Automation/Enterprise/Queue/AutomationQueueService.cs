using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Queue;

internal interface IAutomationQueueService
{
    Task<AutomationQueueItem> EnqueueAsync(
        AutomationQueueItem item,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationQueueItem>>
        ClaimDueAsync(
            int max,
            TimeSpan lease,
            CancellationToken cancellationToken = default);

    Task<int> RecoverExpiredLeasesAsync(
        TimeSpan lease,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveForOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

/*
 * AUT-1.5 — Cola persistente.
 *
 * Idempotencia por IdempotencyKey: si ya existe un item
 * no terminal con la misma clave, se devuelve el existente
 * y no se crea un duplicado.
 */
internal sealed class AutomationQueueService
    : IAutomationQueueService
{
    private readonly TitanMdmDbContext _db;

    public AutomationQueueService(
        TitanMdmDbContext db)
    {
        _db = db;
    }

    public async Task<AutomationQueueItem>
        EnqueueAsync(
            AutomationQueueItem item,
            CancellationToken cancellationToken = default)
    {
        var existing =
            await _db
                .Set<AutomationQueueItem>()
                .FirstOrDefaultAsync(
                    x =>
                        x.IdempotencyKey ==
                            item.IdempotencyKey &&
                        (x.State ==
                            AutomationQueueItemState
                                .Pending ||
                         x.State ==
                            AutomationQueueItemState
                                .Retried ||
                         x.State ==
                            AutomationQueueItemState
                                .Running),
                    cancellationToken);

        if (existing is not null)
            return existing;

        _db.Set<AutomationQueueItem>()
            .Add(item);

        await _db.SaveChangesAsync(
            cancellationToken);

        return item;
    }

    public async Task<IReadOnlyList<AutomationQueueItem>>
        ClaimDueAsync(
            int max,
            TimeSpan lease,
            CancellationToken cancellationToken = default)
    {
        if (max <= 0)
            return Array.Empty<AutomationQueueItem>();

        var now = DateTime.UtcNow;

        var candidates =
            await _db
                .Set<AutomationQueueItem>()
                .Where(x =>
                    (x.State ==
                        AutomationQueueItemState
                            .Pending ||
                     x.State ==
                        AutomationQueueItemState
                            .Retried) &&
                    x.NextAttemptAtUtc <= now)
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.NextAttemptAtUtc)
                .Take(max * 2)
                .ToListAsync(cancellationToken);

        var claimed =
            new List<AutomationQueueItem>();

        foreach (var item in candidates)
        {
            if (item.TryAcquire(now, lease))
                claimed.Add(item);

            if (claimed.Count >= max)
                break;
        }

        if (claimed.Count > 0)
        {
            await _db.SaveChangesAsync(
                cancellationToken);
        }

        return claimed;
    }

    public async Task<int> RecoverExpiredLeasesAsync(
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var stale =
            await _db
                .Set<AutomationQueueItem>()
                .Where(x =>
                    x.State ==
                        AutomationQueueItemState
                            .Running &&
                    x.LeaseUntilUtc.HasValue &&
                    x.LeaseUntilUtc.Value <= now)
                .ToListAsync(cancellationToken);

        if (stale.Count == 0)
            return 0;

        foreach (var item in stale)
            item.RecoverExpiredLease();

        await _db.SaveChangesAsync(
            cancellationToken);

        return stale.Count;
    }

    public Task<int> CountActiveForOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return _db
            .Set<AutomationQueueItem>()
            .CountAsync(
                x =>
                    x.OrganizationId ==
                        organizationId &&
                    (x.State ==
                        AutomationQueueItemState
                            .Pending ||
                     x.State ==
                        AutomationQueueItemState
                            .Retried ||
                     x.State ==
                        AutomationQueueItemState
                            .Running),
                cancellationToken);
    }
}
