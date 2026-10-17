using System.Text.Json.Nodes;

using TitanMDM.Application.Assistant;
using TitanMDM.Application.Devices;
using TitanMDM.Application.Security;

namespace TitanMDM.Infrastructure.Assistant.Tools;

public sealed class DeviceListTool
    : AssistantToolBase
{
    private readonly IDeviceQueryService _devices;

    public DeviceListTool(
        IDeviceQueryService devices)
    {
        _devices =
            devices;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "devices.list",
            "Listar dispositivos",
            "Lista dispositivos administrados dentro del alcance del usuario, con estado de cumplimiento y sistema operativo.",
            AssistantToolKind.Read,
            new[]
            {
                PermissionCodes.Devices.View
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var principal =
            context.Principal;

        IReadOnlyCollection<Guid>? accessibleSiteIds =
            context.Scope.OrganizationWide
                ? null
                : context.AccessibleSiteIds;

        var result =
            await _devices.GetDevicesAsync(
                principal.OrganizationId,
                accessibleSiteIds,
                ReadString(arguments, "search"),
                ReadString(arguments, "platform"),
                ReadString(arguments, "status"),
                ReadString(arguments, "compliance"),
                ReadManaged(arguments),
                ReadString(arguments, "sortBy"),
                ReadString(arguments, "sortDirection"),
                ReadInt(arguments, "page", 1, 1, 500),
                ReadInt(arguments, "pageSize", 25, 1, 100),
                cancellationToken);

        return AssistantToolResult.Ok(
            $"Se encontraron {result.Items.Count} dispositivos (total {result.TotalCount}).",
            AssistantDataSanitizer.WrapUntrustedData(
                "devices.list",
                AssistantDataSanitizer.ToCompactJson(
                    result,
                    4000)));
    }

    private static bool? ReadManaged(
        JsonObject arguments)
    {
        var raw =
            ReadString(
                arguments,
                "managed");

        return bool.TryParse(
            raw,
            out var value)
            ? value
            : null;
    }
}

public sealed class DeviceStatusTool
    : AssistantToolBase
{
    private readonly IDeviceQueryService _devices;
    private readonly IScopeAccessService _scopeAccess;

    public DeviceStatusTool(
        IDeviceQueryService devices,
        IScopeAccessService scopeAccess)
    {
        _devices =
            devices;

        _scopeAccess =
            scopeAccess;
    }

    public override AssistantToolDescriptor Descriptor
        =>
        new(
            "devices.status",
            "Estado operativo de un dispositivo",
            "Devuelve el estado operativo resumido de un dispositivo, solo si el usuario tiene alcance sobre él.",
            AssistantToolKind.Read,
            new[]
            {
                PermissionCodes.Devices.View
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var deviceId =
            ReadGuid(
                arguments,
                "deviceId");

        if (deviceId is null ||
            deviceId == Guid.Empty)
        {
            return AssistantToolResult.Fail(
                "invalid_argument",
                "Indica el identificador del dispositivo.");
        }

        var principal =
            context.Principal;

        var allowed =
            await _scopeAccess.CanAccessDeviceAsync(
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

        var snapshot =
            await _devices.GetOperationalSnapshotAsync(
                principal.OrganizationId,
                deviceId.Value,
                cancellationToken);

        if (snapshot is null)
        {
            return AssistantToolResult.Fail(
                "not_found",
                "El dispositivo no existe.");
        }

        return AssistantToolResult.Ok(
            "Estado operativo del dispositivo recuperado.",
            AssistantDataSanitizer.WrapUntrustedData(
                "devices.status",
                AssistantDataSanitizer.ToCompactJson(
                    snapshot,
                    3000)));
    }
}
