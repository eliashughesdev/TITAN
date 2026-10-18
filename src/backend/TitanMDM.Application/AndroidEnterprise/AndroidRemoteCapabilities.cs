namespace TitanMDM.Application.AndroidEnterprise;

/// <summary>
/// Resultado de compatibilidad de una operación administrativa
/// Android Enterprise para un dispositivo concreto.
/// </summary>
public enum AndroidCapabilityStatus
{
    /// <summary>La operación puede emitirse al dispositivo.</summary>
    Available,

    /// <summary>La operación no está soportada por el modo, versión o fabricante.</summary>
    NotSupported,

    /// <summary>No hay datos suficientes para confirmar compatibilidad.</summary>
    PendingValidation
}

public sealed record AndroidCapabilityDto(
    string Action,
    string Status,
    string? Reason);

public sealed record AndroidRemoteCapabilityInput(
    string? Manufacturer,
    string? Model,
    int? ApiLevel,
    string? ManagementMode,
    string? State,
    string? Ownership,
    bool IsManaged,
    bool IsDeletedInGoogle,
    DateTime? LastStatusReportTimeUtc);

public sealed record AndroidDeviceCapabilitiesDto(
    Guid DeviceId,
    string DeviceName,
    string? Manufacturer,
    string? Model,
    int? ApiLevel,
    string? AndroidVersion,
    string? SecurityPatchLevel,
    string? ManagementMode,
    string? Ownership,
    bool IsManaged,
    bool IsDeletedInGoogle,
    bool HasGoogleMobileServices,
    bool RequiresAlternativeRoute,
    string? AlternativeRouteNote,
    bool RemoteDiagnosticsSupported,
    bool RemoteVisionSupported,
    bool RemoteControlSupported,
    string RemoteSupportNote,
    IReadOnlyList<AndroidCapabilityDto> Actions);

/// <summary>
/// Resuelve, sin efectos secundarios, qué operaciones puede
/// aceptar un dispositivo Android según el modo de administración
/// reportado por Google, el nivel de API y el fabricante.
///
/// Es una capa de asesoría: el backend sigue siendo la autoridad
/// final y Google puede rechazar una operación aun cuando aquí
/// figure como disponible.
/// </summary>
public static class AndroidRemoteCapabilityResolver
{
    public const string ActionLock = "lock";
    public const string ActionReboot = "reboot";
    public const string ActionResetPassword = "reset-password";
    public const string ActionStartLostMode = "start-lost-mode";
    public const string ActionStopLostMode = "stop-lost-mode";
    public const string ActionWipe = "wipe";
    public const string ActionDelete = "delete";

    private const int RebootMinimumApiLevel = 24;
    private const int LostModeMinimumApiLevel = 24;

    public static IReadOnlyList<AndroidCapabilityDto> Resolve(
        AndroidRemoteCapabilityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var actions = new List<AndroidCapabilityDto>
        {
            ResolveLock(input),
            ResolveReboot(input),
            ResolveResetPassword(input),
            ResolveLostMode(input, isStart: true),
            ResolveLostMode(input, isStart: false),
            ResolveWipe(input),
            ResolveDelete(input)
        };

        return actions;
    }

    public static AndroidCapabilityDto ResolveAction(
        AndroidRemoteCapabilityInput input,
        string action)
    {
        ArgumentNullException.ThrowIfNull(input);

        return NormalizeAction(action) switch
        {
            ActionLock => ResolveLock(input),
            ActionReboot => ResolveReboot(input),
            ActionResetPassword => ResolveResetPassword(input),
            ActionStartLostMode => ResolveLostMode(input, isStart: true),
            ActionStopLostMode => ResolveLostMode(input, isStart: false),
            ActionWipe => ResolveWipe(input),
            ActionDelete => ResolveDelete(input),
            _ => new AndroidCapabilityDto(
                action,
                nameof(AndroidCapabilityStatus.NotSupported),
                "La operación no está soportada por TitanMDM.")
        };
    }

    public static bool IsMobileDeviceOwner(
        string? managementMode)
    {
        return ModeEquals(managementMode, "DEVICE_OWNER")
            || ModeEquals(managementMode, "FULLY_MANAGED")
            || ModeEquals(managementMode, "DEDICATED_DEVICE")
            || ModeEquals(managementMode, "DEDICATED");
    }

    public static bool IsProfileOwner(
        string? managementMode)
    {
        return ModeEquals(managementMode, "PROFILE_OWNER")
            || ModeEquals(managementMode, "WORK_PROFILE");
    }

    public static bool IsHuaweiFamily(
        string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer))
            return false;

        var value = manufacturer.Trim();

        return value.Contains("Huawei", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Honor", StringComparison.OrdinalIgnoreCase);
    }

    private static AndroidCapabilityDto ResolveLock(
        AndroidRemoteCapabilityInput input)
    {
        if (!input.IsManaged || input.IsDeletedInGoogle)
        {
            return new AndroidCapabilityDto(
                ActionLock,
                nameof(AndroidCapabilityStatus.NotSupported),
                "El dispositivo no está administrado actualmente.");
        }

        if (IsProfileOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                ActionLock,
                nameof(AndroidCapabilityStatus.Available),
                "Se bloqueará el perfil de trabajo administrado.");
        }

        return new AndroidCapabilityDto(
            ActionLock,
            nameof(AndroidCapabilityStatus.Available),
            "Soportado mediante Android Management API.");
    }

    private static AndroidCapabilityDto ResolveReboot(
        AndroidRemoteCapabilityInput input)
    {
        if (!input.IsManaged || input.IsDeletedInGoogle)
        {
            return new AndroidCapabilityDto(
                ActionReboot,
                nameof(AndroidCapabilityStatus.NotSupported),
                "El dispositivo no está administrado actualmente.");
        }

        if (IsProfileOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                ActionReboot,
                nameof(AndroidCapabilityStatus.NotSupported),
                "Android no admite reinicio remoto en perfil de trabajo.");
        }

        if (!IsMobileDeviceOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                ActionReboot,
                nameof(AndroidCapabilityStatus.PendingValidation),
                "El modo de administración no está confirmado.");
        }

        if (input.ApiLevel is null)
        {
            return new AndroidCapabilityDto(
                ActionReboot,
                nameof(AndroidCapabilityStatus.PendingValidation),
                "Falta confirmar el nivel de API del dispositivo.");
        }

        if (input.ApiLevel < RebootMinimumApiLevel)
        {
            return new AndroidCapabilityDto(
                ActionReboot,
                nameof(AndroidCapabilityStatus.NotSupported),
                $"Requiere Android 7.0 (API {RebootMinimumApiLevel}) o superior.");
        }

        if (IsHuaweiFamily(input.Manufacturer))
        {
            return new AndroidCapabilityDto(
                ActionReboot,
                nameof(AndroidCapabilityStatus.PendingValidation),
                "Fabricante Huawei/Honor: validar soporte de hardware.");
        }

        return new AndroidCapabilityDto(
            ActionReboot,
            nameof(AndroidCapabilityStatus.Available),
            "Soportado en modo propietario del dispositivo.");
    }

    private static AndroidCapabilityDto ResolveResetPassword(
        AndroidRemoteCapabilityInput input)
    {
        if (!input.IsManaged || input.IsDeletedInGoogle)
        {
            return new AndroidCapabilityDto(
                ActionResetPassword,
                nameof(AndroidCapabilityStatus.NotSupported),
                "El dispositivo no está administrado actualmente.");
        }

        if (IsProfileOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                ActionResetPassword,
                nameof(AndroidCapabilityStatus.NotSupported),
                "Android no admite restablecer contraseña en perfil de trabajo.");
        }

        if (!IsMobileDeviceOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                ActionResetPassword,
                nameof(AndroidCapabilityStatus.PendingValidation),
                "El modo de administración no está confirmado.");
        }

        return new AndroidCapabilityDto(
            ActionResetPassword,
            nameof(AndroidCapabilityStatus.Available),
            "Soportado únicamente en dispositivos propietario del dispositivo.");
    }

    private static AndroidCapabilityDto ResolveLostMode(
        AndroidRemoteCapabilityInput input,
        bool isStart)
    {
        var action = isStart
            ? ActionStartLostMode
            : ActionStopLostMode;

        if (!input.IsManaged || input.IsDeletedInGoogle)
        {
            return new AndroidCapabilityDto(
                action,
                nameof(AndroidCapabilityStatus.NotSupported),
                "El dispositivo no está administrado actualmente.");
        }

        if (IsProfileOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                action,
                nameof(AndroidCapabilityStatus.NotSupported),
                "El modo perdido requiere propietario del dispositivo.");
        }

        if (!IsMobileDeviceOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                action,
                nameof(AndroidCapabilityStatus.PendingValidation),
                "El modo de administración no está confirmado.");
        }

        if (input.ApiLevel is not null &&
            input.ApiLevel < LostModeMinimumApiLevel)
        {
            return new AndroidCapabilityDto(
                action,
                nameof(AndroidCapabilityStatus.NotSupported),
                $"Requiere Android 7.0 (API {LostModeMinimumApiLevel}) o superior.");
        }

        return new AndroidCapabilityDto(
            action,
            nameof(AndroidCapabilityStatus.Available),
            "Modo de recuperación empresarial.");
    }

    private static AndroidCapabilityDto ResolveWipe(
        AndroidRemoteCapabilityInput input)
    {
        if (!input.IsManaged || input.IsDeletedInGoogle)
        {
            return new AndroidCapabilityDto(
                ActionWipe,
                nameof(AndroidCapabilityStatus.NotSupported),
                "El dispositivo no está administrado actualmente.");
        }

        if (IsProfileOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                ActionWipe,
                nameof(AndroidCapabilityStatus.Available),
                "Elimina únicamente el perfil de trabajo administrado.");
        }

        if (!IsMobileDeviceOwner(input.ManagementMode))
        {
            return new AndroidCapabilityDto(
                ActionWipe,
                nameof(AndroidCapabilityStatus.PendingValidation),
                "El modo de administración no está confirmado.");
        }

        return new AndroidCapabilityDto(
            ActionWipe,
            nameof(AndroidCapabilityStatus.Available),
            "Borrado de fábrica irreversible. Requiere confirmación reforzada.");
    }

    private static AndroidCapabilityDto ResolveDelete(
        AndroidRemoteCapabilityInput input)
    {
        if (input.IsDeletedInGoogle)
        {
            return new AndroidCapabilityDto(
                ActionDelete,
                nameof(AndroidCapabilityStatus.NotSupported),
                "El dispositivo ya no existe en Android Enterprise.");
        }

        return new AndroidCapabilityDto(
            ActionDelete,
            nameof(AndroidCapabilityStatus.Available),
            "Elimina el recurso administrado de Google.");
    }

    private static string NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return string.Empty;

        return action.Trim().ToLowerInvariant() switch
        {
            "lock" => ActionLock,
            "reboot" => ActionReboot,
            "resetpassword" => ActionResetPassword,
            "reset-password" => ActionResetPassword,
            "startlostmode" => ActionStartLostMode,
            "start-lost-mode" => ActionStartLostMode,
            "stoplostmode" => ActionStopLostMode,
            "stop-lost-mode" => ActionStopLostMode,
            "wipe" => ActionWipe,
            "delete" => ActionDelete,
            _ => action.Trim()
        };
    }

    private static bool ModeEquals(
        string? actual,
        string expected)
    {
        return string.Equals(
            actual,
            expected,
            StringComparison.OrdinalIgnoreCase);
    }
}
