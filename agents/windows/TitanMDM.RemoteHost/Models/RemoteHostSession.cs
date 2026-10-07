namespace TitanMDM.RemoteHost.Models;

public sealed record RemoteHostSession(
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