namespace TitanMDM.WindowsAgent.Contracts;

public sealed record AgentCommand(
    Guid CommandId,
    string CommandType,
    string PayloadJson,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);