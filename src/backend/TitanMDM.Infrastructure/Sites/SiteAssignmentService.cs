using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Sites;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Sites;

public sealed class SiteAssignmentService
    : ISiteAssignmentService
{
    private readonly TitanMdmDbContext
        _dbContext;

    public SiteAssignmentService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task AssignUserAsync(
        Guid organizationId,
        Guid userId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default)
    {
        await ValidateSiteSelectionAsync(
            organizationId,
            siteId,
            siteLocationId,
            cancellationToken);

        var user =
            await _dbContext
                .Users
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            userId,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "El usuario no existe.");

        user.SetSite(
            siteId,
            siteLocationId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task AssignDeviceAsync(
        Guid organizationId,
        Guid deviceId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default)
    {
        await ValidateSiteSelectionAsync(
            organizationId,
            siteId,
            siteLocationId,
            cancellationToken);

        var device =
            await _dbContext
                .Devices
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            deviceId
                        &&
                        !x.IsDeleted,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "El dispositivo no existe.");

        device.AssignSite(
            siteId,
            siteLocationId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    public async Task AssignTicketAsync(
        Guid organizationId,
        Guid ticketId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default)
    {
        await ValidateSiteSelectionAsync(
            organizationId,
            siteId,
            siteLocationId,
            cancellationToken);

        var ticket =
            await _dbContext
                .HelpdeskTickets
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            ticketId,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "El ticket no existe.");

        ticket.AssignSite(
            siteId,
            siteLocationId);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);
    }

    private async Task ValidateSiteSelectionAsync(
        Guid organizationId,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken)
    {
        if (
            !siteId.HasValue
            &&
            !siteLocationId.HasValue)
        {
            return;
        }

        if (
            !siteId.HasValue
            &&
            siteLocationId.HasValue)
        {
            throw new InvalidOperationException(
                "No se puede asignar una ubicación sin una localidad.");
        }

        var site =
            await _dbContext
                .Sites
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            siteId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (site is null)
        {
            throw new InvalidOperationException(
                "La localidad no existe o está desactivada.");
        }

        if (!siteLocationId.HasValue)
        {
            return;
        }

        var locationExists =
            await _dbContext
                .SiteLocations
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            site.Id
                        &&
                        x.Id ==
                            siteLocationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (!locationExists)
        {
            throw new InvalidOperationException(
                "La ubicación no existe, está desactivada o no pertenece a la localidad.");
        }
    }
}