namespace TitanMDM.WindowsAgent.Contracts;

public sealed record RemoteSupportRequest(
    Guid SessionId,
    Guid DeviceId,
    Guid RequestedByUserId,
    string TechnicianDisplayName,
    string Reason,
    bool AllowKeyboard,
    bool AllowMouse,
    bool AllowClipboard,
    bool AllowFileTransfer,
    DateTime RequestedAtUtc,
    DateTime ExpiresAtUtc);

public sealed record RemoteSupportSessionState(
    Guid SessionId,
    string Status,
    string TechnicianDisplayName,
    string Reason,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc);

public sealed record RemoteSupportAuditEvent(
    Guid SessionId,
    string EventType,
    string Description,
    DateTime OccurredAtUtc);