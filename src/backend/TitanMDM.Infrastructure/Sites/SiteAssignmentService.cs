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

    public async Task<IReadOnlyCollection<SiteAssignmentUserDto>> GetUsersAsync(
        Guid organizationId,
        string? search = null,
        bool unassignedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim();

        var query =
            from user in _dbContext.Users.AsNoTracking()
            join site in _dbContext.Sites.AsNoTracking()
                on user.SiteId equals site.Id into sites
            from site in sites.DefaultIfEmpty()
            join location in _dbContext.SiteLocations.AsNoTracking()
                on user.SiteLocationId equals location.Id into locations
            from location in locations.DefaultIfEmpty()
            where user.OrganizationId == organizationId
            select new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.JobTitle,
                user.IsActive,
                user.SiteId,
                SiteName = site == null ? null : site.Name,
                user.SiteLocationId,
                SiteLocationName = location == null ? null : location.Name
            };

        if (unassignedOnly)
        {
            query = query.Where(x => x.SiteId == null);
        }

        var users = await query
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToListAsync(cancellationToken);

        var memberships = await (
                from member in _dbContext.HelpdeskTeamMembers.AsNoTracking()
                join team in _dbContext.HelpdeskTeams.AsNoTracking()
                    on member.TeamId equals team.Id
                where member.OrganizationId == organizationId && team.IsActive
                select new
                {
                    member.UserId,
                    team.Name
                })
            .ToListAsync(cancellationToken);

        var groupsByUser = memberships
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<string>)x
                    .Select(item => item.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToArray());

        return users
            .Select(user =>
            {
                var groups = groupsByUser.GetValueOrDefault(user.Id) ?? [];

                return new SiteAssignmentUserDto(
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    $"{user.FirstName} {user.LastName}".Trim(),
                    user.Email,
                    user.JobTitle,
                    user.IsActive,
                    user.SiteId,
                    user.SiteName,
                    user.SiteLocationId,
                    user.SiteLocationName,
                    groups.Count > 0,
                    groups);
            })
            .Where(user =>
                string.IsNullOrWhiteSpace(normalizedSearch)
                || user.FullName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || user.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || user.Groups.Any(group =>
                    group.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
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

    public async Task AssignUsersAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> userIds,
        Guid? siteId,
        Guid? siteLocationId,
        CancellationToken cancellationToken = default)
    {
        if (userIds is null || userIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Debe seleccionar al menos un usuario.");
        }

        var distinctUserIds = userIds.Distinct().ToArray();

        if (distinctUserIds.Length > 500)
        {
            throw new InvalidOperationException(
                "No se pueden actualizar más de 500 usuarios por operación.");
        }

        await ValidateSiteSelectionAsync(
            organizationId,
            siteId,
            siteLocationId,
            cancellationToken);

        var users = await _dbContext.Users
            .Where(x =>
                x.OrganizationId == organizationId
                && distinctUserIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (users.Count != distinctUserIds.Length)
        {
            throw new InvalidOperationException(
                "Uno o más usuarios no existen o pertenecen a otra organización.");
        }

        foreach (var user in users)
        {
            user.SetSite(siteId, siteLocationId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
