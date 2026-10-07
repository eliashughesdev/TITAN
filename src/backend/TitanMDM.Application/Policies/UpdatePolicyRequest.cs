namespace TitanMDM.Application.Policies;

public sealed record UpdatePolicyRequest(
    string Name,
    string? Description,
    string ConfigurationJson);