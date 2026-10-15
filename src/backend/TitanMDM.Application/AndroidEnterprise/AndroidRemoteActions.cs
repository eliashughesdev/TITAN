namespace TitanMDM.Application.AndroidEnterprise;

/// <summary>
/// Remote actions officially supported by the Google
/// Android Management API for managed Android devices.
/// </summary>
public enum AndroidRemoteActionType
{
    Lock,
    Reboot,
    ResetPassword,
    StartLostMode,
    StopLostMode,
    Wipe,
    Delete
}

public sealed record AndroidRemoteActionRequest(
    string Action,
    string? NewPassword = null,
    string? LostModeMessage = null,
    string? LostModePhoneNumber = null);

public sealed record AndroidRemoteActionResponse(
    Guid DeviceId,
    string Action,
    string Status,
    string? GoogleCommandName,
    string? CorrelationId,
    DateTime IssuedAtUtc);

public sealed class AndroidRemoteActionException
    : Exception
{
    public AndroidRemoteActionException(
        string code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public interface IAndroidRemoteActionService
{
    Task<AndroidRemoteActionResponse> ExecuteAsync(
        Guid organizationId,
        Guid userId,
        Guid deviceId,
        AndroidRemoteActionRequest request,
        string correlationId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
