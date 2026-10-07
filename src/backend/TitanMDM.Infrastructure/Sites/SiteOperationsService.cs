using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Applications;
using TitanMDM.Application.Policies;
using TitanMDM.Application.Security;
using TitanMDM.Application.Sites;

using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Sites;

public sealed class SiteOperationsService
    : ISiteOperationsService
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IAuthorizationScopeService
        _authorizationScopeService;

    private readonly IPolicyService
        _policyService;

    private readonly ISoftwareDeploymentService
        _softwareDeploymentService;

    public SiteOperationsService(
        TitanMdmDbContext dbContext,
        IAuthorizationScopeService authorizationScopeService,
        IPolicyService policyService,
        ISoftwareDeploymentService softwareDeploymentService)
    {
        _dbContext =
            dbContext;

        _authorizationScopeService =
            authorizationScopeService;

        _policyService =
            policyService;

        _softwareDeploymentService =
            softwareDeploymentService;
    }

    // ============================================================
    // SITE SUMMARY
    // ============================================================

    public async Task<SiteOperationalSummaryDto>
        GetSummaryAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            CancellationToken cancellationToken = default)
    {
        var site =
            await GetAuthorizedSiteAsync(
                organizationId,
                actorUserId,
                siteId,
                cancellationToken);

        // ========================================================
        // DEVICES
        // ========================================================

        var devices =
            _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            siteId
                        &&
                        !x.IsDeleted);

        var totalDevices =
            await devices
                .CountAsync(
                    cancellationToken);

        var online =
            await devices
                .CountAsync(
                    x =>
                        x.Status ==
                            DeviceStatus.Online,
                    cancellationToken);

        var offline =
            await devices
                .CountAsync(
                    x =>
                        x.Status ==
                            DeviceStatus.Offline,
                    cancellationToken);

        var managed =
            await devices
                .CountAsync(
                    x =>
                        x.IsManaged,
                    cancellationToken);

        var windows =
            await devices
                .CountAsync(
                    x =>
                        x.Platform ==
                            DevicePlatform.Windows,
                    cancellationToken);

        var android =
            await devices
                .CountAsync(
                    x =>
                        x.Platform ==
                            DevicePlatform.Android,
                    cancellationToken);

        var compliant =
            await devices
                .CountAsync(
                    x =>
                        x.ComplianceStatus ==
                            ComplianceStatus.Compliant,
                    cancellationToken);

        var nonCompliant =
            await devices
                .CountAsync(
                    x =>
                        x.ComplianceStatus ==
                            ComplianceStatus.NonCompliant,
                    cancellationToken);

        var quarantined =
            await devices
                .CountAsync(
                    x =>
                        x.Status ==
                            DeviceStatus.Quarantined
                        ||
                        x.ComplianceStatus ==
                            ComplianceStatus.Quarantined,
                    cancellationToken);

        var deviceIds =
            await devices
                .Select(
                    x =>
                        x.Id)
                .ToArrayAsync(
                    cancellationToken);

        // ========================================================
        // SECURITY
        // ========================================================

        var securityQuery =
            _dbContext
                .DeviceSecurityPostures
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        deviceIds.Contains(
                            x.DeviceId));

        var evaluatedDevices =
            await securityQuery
                .CountAsync(
                    cancellationToken);

        decimal averageComplianceScore =
            0m;

        if (evaluatedDevices > 0)
        {
            averageComplianceScore =
                Math.Round(
                    await securityQuery
                        .AverageAsync(
                            x =>
                                (decimal)
                                x.ComplianceScore,
                            cancellationToken),
                    1);
        }

        var criticalRisk =
            await securityQuery
                .CountAsync(
                    x =>
                        x.RiskLevel ==
                            "Critical",
                    cancellationToken);

        var highRisk =
            await securityQuery
                .CountAsync(
                    x =>
                        x.RiskLevel ==
                            "High",
                    cancellationToken);

        // ========================================================
        // HELPDESK
        // ========================================================

        var tickets =
            _dbContext
                .HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            siteId);

        var now =
            DateTime.UtcNow;

        var totalTickets =
            await tickets
                .CountAsync(
                    cancellationToken);

        var newTickets =
            await tickets
                .CountAsync(
                    x =>
                        x.Status ==
                            "new",
                    cancellationToken);

        var openTickets =
            await tickets
                .CountAsync(
                    x =>
                        x.Status ==
                            "open",
                    cancellationToken);

        var inProgressTickets =
            await tickets
                .CountAsync(
                    x =>
                        x.Status ==
                            "inprogress",
                    cancellationToken);

        var pendingUserTickets =
            await tickets
                .CountAsync(
                    x =>
                        x.Status ==
                            "pendinguser",
                    cancellationToken);

        var resolvedTickets =
            await tickets
                .CountAsync(
                    x =>
                        x.Status ==
                            "resolved",
                    cancellationToken);

        var closedTickets =
            await tickets
                .CountAsync(
                    x =>
                        x.Status ==
                            "closed",
                    cancellationToken);

        var unassignedTickets =
            await tickets
                .CountAsync(
                    x =>
                        !x.AssigneeUserId
                            .HasValue
                        &&
                        x.Status !=
                            "resolved"
                        &&
                        x.Status !=
                            "closed",
                    cancellationToken);

        var slaBreachedTickets =
            await tickets
                .CountAsync(
                    x =>
                        x.Status !=
                            "resolved"
                        &&
                        x.Status !=
                            "closed"
                        &&
                        (
                            (
                                x.FirstRespondedAtUtc ==
                                    null
                                &&
                                x.FirstResponseDueAtUtc
                                    .HasValue
                                &&
                                x.FirstResponseDueAtUtc
                                    .Value <
                                    now
                            )
                            ||
                            (
                                x.ResolvedAtUtc ==
                                    null
                                &&
                                x.ResolveDueAtUtc
                                    .HasValue
                                &&
                                x.ResolveDueAtUtc
                                    .Value <
                                    now
                            )
                        ),
                    cancellationToken);

        // ========================================================
        // RESULT
        // ========================================================

        return new SiteOperationalSummaryDto(
            site.Id,
            site.Code,
            site.Name,
            site.IsActive,
            DateTime.UtcNow,
            new SiteDeviceSummaryDto(
                totalDevices,
                online,
                offline,
                managed,
                windows,
                android,
                compliant,
                nonCompliant,
                quarantined),
            new SiteHelpdeskSummaryDto(
                totalTickets,
                newTickets,
                openTickets,
                inProgressTickets,
                pendingUserTickets,
                resolvedTickets,
                closedTickets,
                unassignedTickets,
                slaBreachedTickets),
            new SiteSecuritySummaryDto(
                evaluatedDevices,
                averageComplianceScore,
                criticalRisk,
                highRisk));
    }

    // ============================================================
    // POLICY → SITE
    // ============================================================

    public async Task<SitePolicyOperationResultDto>
        AssignPolicyAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            Guid policyId,
            CancellationToken cancellationToken = default)
    {
        await GetAuthorizedSiteAsync(
            organizationId,
            actorUserId,
            siteId,
            cancellationToken);

        var policy =
            await _dbContext
                .Policies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            policyId,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "La política no existe.");

        if (
            policy.Status !=
            PolicyStatus.Active)
        {
            throw new InvalidOperationException(
                "La política debe estar activa antes de asignarla a una localidad.");
        }

        var devices =
            _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            siteId
                        &&
                        !x.IsDeleted);

        devices =
            policy.Platform switch
            {
                PolicyPlatform.Windows =>
                    devices.Where(
                        x =>
                            x.Platform ==
                                DevicePlatform.Windows),

                PolicyPlatform.Android =>
                    devices.Where(
                        x =>
                            x.Platform ==
                                DevicePlatform.Android),

                _ =>
                    devices
            };

        var deviceIds =
            await devices
                .Select(
                    x =>
                        x.Id)
                .Distinct()
                .ToArrayAsync(
                    cancellationToken);

        if (deviceIds.Length == 0)
        {
            throw new InvalidOperationException(
                "La localidad no tiene dispositivos compatibles con esta política.");
        }

        var assignments =
            await _policyService
                .AssignAsync(
                    organizationId,
                    actorUserId,
                    policyId,
                    new AssignPolicyRequest(
                        deviceIds),
                    cancellationToken);

        return new SitePolicyOperationResultDto(
            siteId,
            policyId,
            deviceIds.Length,
            assignments);
    }

    // ============================================================
    // SOFTWARE → SITE
    // ============================================================

    public async Task<SiteSoftwareOperationResultDto>
        DeploySoftwareAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            Guid packageId,
            CancellationToken cancellationToken = default)
    {
        await GetAuthorizedSiteAsync(
            organizationId,
            actorUserId,
            siteId,
            cancellationToken);

        var package =
            await _dbContext
                .SoftwarePackages
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Id ==
                            packageId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (package is null)
        {
            throw new InvalidOperationException(
                "El paquete no existe o está desactivado.");
        }

        /*
         * El motor de paquetes actual trabaja con Windows
         * para MSI/MSIX/APPX.
         *
         * No incluimos Android aquí porque Android Enterprise
         * utilizará su mecanismo de aplicaciones administradas.
         */

        var deviceIds =
            await _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            siteId
                        &&
                        x.Platform ==
                            DevicePlatform.Windows
                        &&
                        x.IsManaged
                        &&
                        !x.IsDeleted)
                .Select(
                    x =>
                        x.Id)
                .Distinct()
                .ToArrayAsync(
                    cancellationToken);

        if (deviceIds.Length == 0)
        {
            throw new InvalidOperationException(
                "La localidad no tiene dispositivos Windows administrados.");
        }

        var deployments =
            new List<SoftwareDeploymentDto>(
                deviceIds.Length);

        /*
         * Reutilizamos el motor existente de deployments.
         *
         * Esto conserva:
         * - validación del paquete;
         * - generación del comando;
         * - hash SHA-256;
         * - argumentos;
         * - timeout;
         * - estado;
         * - auditoría existente del deployment.
         */

        foreach (var deviceId in deviceIds)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var deployment =
                await _softwareDeploymentService
                    .DeployAsync(
                        organizationId,
                        actorUserId,
                        package.Id,
                        new DeploySoftwarePackageRequest(
                            "Device",
                            deviceId),
                        cancellationToken);

            deployments.Add(
                deployment);
        }

        return new SiteSoftwareOperationResultDto(
            siteId,
            package.Id,
            deviceIds.Length,
            deployments);
    }

    // ============================================================
    // SITE DEVICE REPORT
    // ============================================================

    public async Task<byte[]>
        ExportDevicesCsvAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            CancellationToken cancellationToken = default)
    {
        var site =
            await GetAuthorizedSiteAsync(
                organizationId,
                actorUserId,
                siteId,
                cancellationToken);

        var devices =
            await _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.SiteId ==
                            siteId
                        &&
                        !x.IsDeleted)
                .OrderBy(
                    x =>
                        x.DeviceName)
                .ToListAsync(
                    cancellationToken);

        var builder =
            new StringBuilder();

        /*
         * UTF-8 BOM:
         * mejora compatibilidad con Excel en Windows.
         */
        builder.Append(
            '\uFEFF');

        builder.AppendLine(
            "Localidad,CodigoLocalidad,Nombre,Plataforma,Estado," +
            "Cumplimiento,Serial,Fabricante,Modelo,SistemaOperativo," +
            "Version,Usuario,Departamento,IP,Administrado,UltimoContactoUTC");

        foreach (var device in devices)
        {
            builder.AppendLine(
                string.Join(
                    ",",
                    Csv(site.Name),
                    Csv(site.Code),
                    Csv(device.DeviceName),
                    Csv(
                        device.Platform
                            .ToString()),
                    Csv(
                        device.Status
                            .ToString()),
                    Csv(
                        device.ComplianceStatus
                            .ToString()),
                    Csv(device.SerialNumber),
                    Csv(device.Manufacturer),
                    Csv(device.Model),
                    Csv(device.OperatingSystem),
                    Csv(
                        device.OperatingSystemVersion),
                    Csv(device.AssignedUser),
                    Csv(device.Department),
                    Csv(device.IpAddress),
                    Csv(
                        device.IsManaged
                            ? "Sí"
                            : "No"),
                    Csv(
                        device.LastSeenAtUtc
                            ?.ToString(
                                "O",
                                CultureInfo
                                    .InvariantCulture))));
        }

        return Encoding
            .UTF8
            .GetBytes(
                builder.ToString());
    }

    // ============================================================
    // AUTHORIZATION
    // ============================================================

    private async Task<Site>
        GetAuthorizedSiteAsync(
            Guid organizationId,
            Guid actorUserId,
            Guid siteId,
            CancellationToken cancellationToken)
    {
        // ========================================================
        // SECURITY CONTEXT
        // ========================================================

        if (
            organizationId ==
                Guid.Empty
            ||
            actorUserId ==
                Guid.Empty
            ||
            siteId ==
                Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Contexto de seguridad inválido.");
        }

        // ========================================================
        // ORGANIZATION + ACTIVE SITE
        // ========================================================

        /*
         * IMPORTANTE:
         *
         * Una asignación de scope histórica NO debe permitir
         * trabajar con una Site que haya sido desactivada.
         *
         * Por eso IsActive se valida en cada operación
         * administrativa sensible.
         */

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

        // ========================================================
        // SCOPE
        // ========================================================

        var allowed =
            await _authorizationScopeService
                .CanAccessAsync(
                    organizationId,
                    actorUserId,
                    AuthorizationScopeType.Site,
                    siteId,
                    cancellationToken);

        if (!allowed)
        {
            throw new UnauthorizedAccessException(
                "El usuario no tiene acceso a esta localidad.");
        }

        return site;
    }

    // ============================================================
    // CSV
    // ============================================================

    private static string Csv(
        string? value)
    {
        var safe =
            value
            ??
            string.Empty;

        /*
         * CSV seguro:
         *
         * - campos entre comillas;
         * - comillas internas duplicadas.
         */

        return
            "\"" +
            safe.Replace(
                "\"",
                "\"\"",
                StringComparison.Ordinal)
            +
            "\"";
    }
}