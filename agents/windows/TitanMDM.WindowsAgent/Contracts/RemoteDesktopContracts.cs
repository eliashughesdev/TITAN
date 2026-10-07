namespace TitanMDM.WindowsAgent.Contracts;

public sealed record RemoteDesktopStartRequest(
    Guid SessionId,
    string TechnicianName,
    string Reason,
    bool AllowKeyboard,
    bool AllowMouse,
    bool AllowClipboard,
    bool AllowFileTransfer,
    DateTime ExpiresAtUtc,
    string ServerUrl,
    string AccessToken);

public sealed record RemoteDesktopHostStatus(
    Guid SessionId,
    string Status,
    int ProcessId,
    int WindowsSessionId,
    DateTime StartedAtUtc,
    string? ErrorMessage = null);

public sealed record RemoteDesktopFrame(
    Guid SessionId,
    long Sequence,
    int Width,
    int Height,
    string MimeType,
    string Base64Data,
    DateTime CapturedAtUtc);

public sealed record RemotePointerEvent(
    Guid SessionId,
    string EventType,
    double NormalizedX,
    double NormalizedY,
    int Button = 0,
    int WheelDelta = 0);

public sealed record RemoteKeyboardEvent(
    Guid SessionId,
    string EventType,
    int VirtualKey,
    bool Alt,
    bool Control,
    bool Shift);

public sealed record RemoteClipboardPayload(
    Guid SessionId,
    string Text);

public sealed record RemoteHostBootstrap(
    Guid SessionId,
    string ServerUrl,
    string AccessToken,
    DateTime ExpiresAtUtc);