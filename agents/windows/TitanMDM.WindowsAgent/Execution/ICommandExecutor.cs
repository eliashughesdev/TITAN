using TitanMDM.WindowsAgent.Contracts;

namespace TitanMDM.WindowsAgent.Execution;

public interface ICommandExecutor
{
    Task<CommandExecutionResult> ExecuteAsync(
        AgentCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record CommandExecutionResult(
    bool Success,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage);