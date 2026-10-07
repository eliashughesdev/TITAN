namespace TitanMDM.WindowsAgent.Contracts;

public sealed record CommandFailedRequest(
    string ErrorCode,
    string ErrorMessage,
    string? ResultJson);