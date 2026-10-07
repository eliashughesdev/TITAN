namespace TitanMDM.Application.Policies;

public sealed record AssignPolicyRequest(
    IReadOnlyCollection<Guid> DeviceIds);