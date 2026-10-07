using TitanMDM.Domain.Enums;

namespace TitanMDM.Domain.Entities;

public sealed class DevicePolicyAssignment
{
    private DevicePolicyAssignment()
    {
    }

    public DevicePolicyAssignment(
        Guid organizationId,
        Guid policyId,
        Guid deviceId,
        int policyVersion)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (policyId == Guid.Empty)
            throw new ArgumentException(
                "PolicyId is required.");

        if (deviceId == Guid.Empty)
            throw new ArgumentException(
                "DeviceId is required.");

        if (policyVersion < 1)
            throw new ArgumentOutOfRangeException(
                nameof(policyVersion));

        Id = Guid.NewGuid();

        OrganizationId =
            organizationId;

        PolicyId =
            policyId;

        DeviceId =
            deviceId;

        PolicyVersion =
            policyVersion;

        Status =
            PolicyAssignmentStatus.Pending;

        AssignedAtUtc =
            DateTime.UtcNow;

        UpdatedAtUtc =
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

    public Guid DeviceId
    {
        get;
        private set;
    }

    public int PolicyVersion
    {
        get;
        private set;
    }

    public PolicyAssignmentStatus Status
    {
        get;
        private set;
    }

    public Guid? CommandId
    {
        get;
        private set;
    }

    public DateTime AssignedAtUtc
    {
        get;
        private set;
    }

    public DateTime UpdatedAtUtc
    {
        get;
        private set;
    }

    public DateTime? AppliedAtUtc
    {
        get;
        private set;
    }

    public string? ErrorMessage
    {
        get;
        private set;
    }

    public void Queue(
        Guid commandId)
    {
        if (commandId == Guid.Empty)
        {
            throw new ArgumentException(
                "CommandId is required.");
        }

        CommandId = commandId;

        Status =
            PolicyAssignmentStatus.Queued;

        ErrorMessage = null;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkApplying()
    {
        Status =
            PolicyAssignmentStatus.Applying;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkApplied()
    {
        Status =
            PolicyAssignmentStatus.Applied;

        AppliedAtUtc =
            DateTime.UtcNow;

        ErrorMessage = null;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void MarkFailed(
        string errorMessage)
    {
        Status =
            PolicyAssignmentStatus.Failed;

        ErrorMessage =
            errorMessage;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }

    public void Remove()
    {
        Status =
            PolicyAssignmentStatus.Removed;

        UpdatedAtUtc =
            DateTime.UtcNow;
    }
}