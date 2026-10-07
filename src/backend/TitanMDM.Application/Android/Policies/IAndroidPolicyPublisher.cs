namespace TitanMDM.Application.Android.Policies;

public interface IAndroidPolicyPublisher
{
    Task<AndroidPolicyPublishResult> PublishAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default);

    Task<AndroidPolicyPublicationDto?> GetCurrentPublicationAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default);

    Task<AndroidPolicyRemoteVerificationResult> VerifyRemoteAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default);
}

public sealed record AndroidPolicyPublishResult(
    Guid PolicyId,
    Guid PolicyVersionId,
    int PolicyVersion,
    Guid PublicationId,
    string GooglePolicyId,
    string GooglePolicyName,
    string Status,
    IReadOnlyCollection<string> Warnings,
    DateTime PublishedAtUtc);

public sealed record AndroidPolicyRemoteVerificationResult(
    Guid PolicyId,
    int PolicyVersion,
    string GooglePolicyId,
    string GooglePolicyName,
    bool ExistsInGoogle,
    string GooglePolicyJson);