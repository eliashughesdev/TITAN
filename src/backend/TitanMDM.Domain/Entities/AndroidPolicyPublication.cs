using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class AndroidPolicyPublication
{
    private AndroidPolicyPublication()
    {
    }

    public AndroidPolicyPublication(
        Guid organizationId,
        Guid policyId,
        Guid policyVersionId,
        int policyVersion,
        string googlePolicyId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.");
        }

        if (policyId == Guid.Empty)
        {
            throw new ArgumentException(
                "PolicyId is required.");
        }

        if (policyVersionId == Guid.Empty)
        {
            throw new ArgumentException(
                "PolicyVersionId is required.");
        }

        if (policyVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyVersion));
        }

        if (string.IsNullOrWhiteSpace(
                googlePolicyId))
        {
            throw new ArgumentException(
                "GooglePolicyId is required.");
        }

        Id = Guid.NewGuid();

        OrganizationId = organizationId;
        PolicyId = policyId;
        PolicyVersionId = policyVersionId;
        PolicyVersion = policyVersion;

        GooglePolicyId =
            googlePolicyId.Trim();

        Status =
            AndroidPolicyPublicationStatus.Pending;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public Guid PolicyId
    {
        get;
        private set;
    }

    public Guid PolicyVersionId
    {
        get;
        private set;
    }

    public int PolicyVersion
    {
        get;
        private set;
    }

    public string GooglePolicyId
    {
        get;
        private set;
    } = string.Empty;

    public string? GooglePolicyName
    {
        get;
        private set;
    }

    public AndroidPolicyPublicationStatus Status
    {
        get;
        private set;
    }

    public string? CompiledPolicyJson
    {
        get;
        private set;
    }

    public string? GoogleResponseJson
    {
        get;
        private set;
    }

    public string? ErrorCode
    {
        get;
        private set;
    }

    public string? ErrorMessage
    {
        get;
        private set;
    }

    public DateTime CreatedAtUtc
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    public DateTime? PublishedAtUtc
    {
        get;
        private set;
    }

    public DateTime? LastAttemptAtUtc
    {
        get;
        private set;
    }

    public DateTime? DeletedAtUtc
    {
        get;
        private set;
    }

    public void BeginPublication(
        string compiledPolicyJson)
    {
        if (string.IsNullOrWhiteSpace(
                compiledPolicyJson))
        {
            throw new ArgumentException(
                "CompiledPolicyJson is required.");
        }

        Status =
            AndroidPolicyPublicationStatus.Publishing;

        CompiledPolicyJson =
            compiledPolicyJson.Trim();

        GoogleResponseJson = null;
        ErrorCode = null;
        ErrorMessage = null;

        LastAttemptAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = LastAttemptAtUtc.Value;
    }

    public void MarkPublished(
        string googlePolicyName,
        string googleResponseJson)
    {
        if (string.IsNullOrWhiteSpace(
                googlePolicyName))
        {
            throw new ArgumentException(
                "GooglePolicyName is required.");
        }

        Status =
            AndroidPolicyPublicationStatus.Published;

        GooglePolicyName =
            googlePolicyName.Trim();

        GoogleResponseJson =
            string.IsNullOrWhiteSpace(
                googleResponseJson)
                ? "{}"
                : googleResponseJson.Trim();

        ErrorCode = null;
        ErrorMessage = null;

        PublishedAtUtc = DateTime.UtcNow;
        LastAttemptAtUtc = PublishedAtUtc;
        UpdatedAtUtc = PublishedAtUtc.Value;
        DeletedAtUtc = null;
    }

    public void MarkFailed(
        string errorCode,
        string errorMessage)
    {
        Status =
            AndroidPolicyPublicationStatus.Failed;

        ErrorCode =
            string.IsNullOrWhiteSpace(errorCode)
                ? "ANDROID_POLICY_PUBLICATION_FAILED"
                : errorCode.Trim();

        ErrorMessage =
            string.IsNullOrWhiteSpace(errorMessage)
                ? "La publicación Android falló."
                : errorMessage.Trim();

        LastAttemptAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = LastAttemptAtUtc.Value;
    }

    public void MarkDeleted()
    {
        Status =
            AndroidPolicyPublicationStatus.Deleted;

        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DeletedAtUtc.Value;
    }
}