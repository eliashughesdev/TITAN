namespace TitanMDM.Application.Commands.Agent;

public sealed record AgentCommandDto(
    Guid CommandId,
    string CommandType,
    string PayloadJson,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);