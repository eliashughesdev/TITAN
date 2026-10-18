using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Applications;
using TitanMDM.Application.Assistant;
using TitanMDM.Application.Policies;
using TitanMDM.Application.Reports;
using TitanMDM.Application.Security;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Assistant.Tools;

public sealed class PolicyListTool
    : AssistantToolBase
{
    private readonly IPolicyService _policies;

    public PolicyListTool(
        IPolicyService policies)
    {
        _policies =
            policies;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "policies.list",
            "Listar políticas",
            "Lista las políticas de configuración de la organización, filtrables por plataforma y estado.",
            AssistantToolKind.Read,
            new[]
            {
                PermissionCodes.Policies.View
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var policies =
            await _policies.GetAllAsync(
                context.Principal.OrganizationId,
                ReadString(arguments, "platform"),
                ReadString(arguments, "status"),
                cancellationToken);

        return AssistantToolResult.Ok(
            $"Se encontraron {policies.Count} políticas.",
            AssistantDataSanitizer.WrapUntrustedData(
                "policies.list",
                AssistantDataSanitizer.ToCompactJson(
                    policies,
                    4000)));
    }
}

public sealed class DevicePoliciesTool
    : AssistantToolBase
{
    private readonly TitanMdmDbContext _db;
    private readonly IScopeAccessService _scopeAccess;

    public DevicePoliciesTool(
        TitanMdmDbContext db,
        IScopeAccessService scopeAccess)
    {
        _db = db;
        _scopeAccess = scopeAccess;
    }

    public override AssistantToolDescriptor Descriptor =>
        new(
            "policies.device",
            "Políticas de un dispositivo",
            "Lista políticas asignadas y su estado para un dispositivo autorizado. Requiere el argumento deviceId.",
            AssistantToolKind.Read,
            new[]
            {
                PermissionCodes.Devices.View,
                PermissionCodes.Policies.View
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var deviceId = ReadGuid(arguments, "deviceId");
        if (deviceId is null || deviceId == Guid.Empty)
        {
            return AssistantToolResult.Fail(
                "invalid_argument",
                "Indica el identificador del dispositivo.");
        }

        var principal = context.Principal;
        var allowed = await _scopeAccess.CanAccessDeviceAsync(
            principal.OrganizationId,
            principal.UserId,
            deviceId.Value,
            cancellationToken);

        if (!allowed)
        {
            return AssistantToolResult.Fail(
                "forbidden",
                "El dispositivo no está dentro de tu alcance.");
        }

        var policies = await (
            from assignment in _db.DevicePolicyAssignments.AsNoTracking()
            join policy in _db.Policies.AsNoTracking()
                on assignment.PolicyId equals policy.Id
            where
                assignment.OrganizationId == principal.OrganizationId &&
                assignment.DeviceId == deviceId.Value &&
                policy.OrganizationId == principal.OrganizationId
            orderby assignment.AssignedAtUtc descending
            select new
            {
                policy.Id,
                policy.Name,
                policy.Platform,
                policy.Status,
                assignment.PolicyVersion,
                assignmentStatus = assignment.Status,
                assignment.AssignedAtUtc,
                assignment.AppliedAtUtc
            })
            .ToListAsync(cancellationToken);

        return AssistantToolResult.Ok(
            $"El dispositivo tiene {policies.Count} políticas asignadas.",
            AssistantDataSanitizer.WrapUntrustedData(
                "policies.device",
                AssistantDataSanitizer.ToCompactJson(
                    policies,
                    4000)));
    }
}

public sealed class ApplicationInventoryTool
    : AssistantToolBase
{
    private readonly IApplicationInventoryService _inventory;

    public ApplicationInventoryTool(
        IApplicationInventoryService inventory)
    {
        _inventory =
            inventory;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "applications.inventory",
            "Inventario de aplicaciones",
            "Resume las aplicaciones detectadas en los dispositivos dentro del alcance del usuario.",
            AssistantToolKind.Read,
            new[]
            {
                PermissionCodes.Applications.View
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Guid>? accessibleSiteIds =
            context.Scope.OrganizationWide
                ? null
                : context.AccessibleSiteIds;

        var applications =
            await _inventory.GetApplicationsAsync(
                context.Principal.OrganizationId,
                accessibleSiteIds,
                ReadString(arguments, "search"),
                ReadSystemApp(arguments),
                cancellationToken);

        return AssistantToolResult.Ok(
            $"Se encontraron {applications.Count} aplicaciones.",
            AssistantDataSanitizer.WrapUntrustedData(
                "applications.inventory",
                AssistantDataSanitizer.ToCompactJson(
                    applications,
                    4000)));
    }

    private static bool? ReadSystemApp(
        JsonObject arguments)
    {
        var raw =
            ReadString(
                arguments,
                "systemApp");

        return bool.TryParse(
            raw,
            out var value)
            ? value
            : null;
    }
}

public sealed class ReportOverviewTool
    : AssistantToolBase
{
    private readonly IReportsService _reports;

    public ReportOverviewTool(
        IReportsService reports)
    {
        _reports =
            reports;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "reports.overview",
            "Resumen de reportes",
            "Devuelve el resumen operativo de la organización (dispositivos, cumplimiento y actividad) dentro del alcance del usuario.",
            AssistantToolKind.Read,
            new[]
            {
                PermissionCodes.Reports.View
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Guid>? accessibleSiteIds =
            context.Scope.OrganizationWide
                ? null
                : context.AccessibleSiteIds;

        var overview =
            await _reports.GetOverviewAsync(
                context.Principal.OrganizationId,
                accessibleSiteIds,
                cancellationToken);

        return AssistantToolResult.Ok(
            "Resumen de reportes recuperado.",
            AssistantDataSanitizer.WrapUntrustedData(
                "reports.overview",
                AssistantDataSanitizer.ToCompactJson(
                    overview,
                    4000)));
    }
}
