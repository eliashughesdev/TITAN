namespace TitanMDM.Domain.Entities;

public sealed class PolicyVersion
{
    private PolicyVersion()
    {
    }

    public PolicyVersion(
        Guid organizationId,
        Guid policyId,
        int versionNumber,
        string configurationJson,
        Guid createdByUserId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (policyId == Guid.Empty)
            throw new ArgumentException(
                "PolicyId is required.");

        if (versionNumber < 1)
            throw new ArgumentOutOfRangeException(
                nameof(versionNumber));

        if (string.IsNullOrWhiteSpace(
                configurationJson))
        {
            throw new ArgumentException(
                "ConfigurationJson is required.");
        }

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException(
                "CreatedByUserId is required.");

        Id = Guid.NewGuid();

        OrganizationId =
            organizationId;

        PolicyId =
            policyId;

        VersionNumber =
            versionNumber;

        ConfigurationJson =
            configurationJson.Trim();

        CreatedByUserId =
            createdByUserId;

        CreatedAtUtc =
            DateTime.UtcNow;
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

    public int VersionNumber
    {
        get;
        private set;
    }

    public string ConfigurationJson
    {
        get;
        private set;
    } = "{}";

    public Guid CreatedByUserId
    {
        get;
        private set;
    }

    public DateTime CreatedAtUtc
    {
        get;
        private set;
    }
}