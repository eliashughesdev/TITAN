using TitanMDM.Application.Audit;

using TitanMDM.Domain.Entities;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Audit;

public sealed class AdministrativeAuditWriter
    : IAdministrativeAuditWriter
{
    private readonly TitanMdmDbContext
        _dbContext;

    public AdministrativeAuditWriter(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task WriteAsync(
        AdministrativeAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (entry.OrganizationId == Guid.Empty)
        {
            return;
        }

        _dbContext
            .AdministrativeAuditEvents
            .Add(
                new AdministrativeAuditEvent(
                    entry.OrganizationId,
                    entry.ActorUserId,
                    entry.Action,
                    entry.TargetType,
                    entry.TargetId,
                    entry.Result,
                    entry.CorrelationId,
                    entry.IpAddress));

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }
}