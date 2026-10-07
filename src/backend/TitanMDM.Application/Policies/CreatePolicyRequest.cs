namespace TitanMDM.Application.Policies;

public sealed record CreatePolicyRequest(
    string Name,
    string? Description,
    string Platform,
    string ConfigurationJson);