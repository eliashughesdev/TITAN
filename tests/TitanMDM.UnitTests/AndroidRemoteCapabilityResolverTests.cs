using TitanMDM.Application.AndroidEnterprise;

namespace TitanMDM.UnitTests;

public sealed class AndroidRemoteCapabilityResolverTests
{
    [Fact]
    public void DeviceOwnerModern_AllCoreActionsAvailable()
    {
        var input = Input(
            managementMode: "DEVICE_OWNER",
            apiLevel: 34);

        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionLock));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionReboot));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionResetPassword));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionStartLostMode));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionWipe));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionDelete));
    }

    [Fact]
    public void ProfileOwner_BlocksOwnerOnlyActions()
    {
        var input = Input(
            managementMode: "PROFILE_OWNER",
            apiLevel: 34);

        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionLock));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionWipe));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionReboot));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionResetPassword));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionStartLostMode));
    }

    [Fact]
    public void LegacyApiLevel_BlocksRebootAndLostMode()
    {
        var input = Input(
            managementMode: "DEVICE_OWNER",
            apiLevel: 23);

        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionReboot));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionStartLostMode));
    }

    [Fact]
    public void HuaweiDeviceOwner_RequiresValidationForReboot()
    {
        var input = Input(
            managementMode: "DEVICE_OWNER",
            apiLevel: 34,
            manufacturer: "Huawei");

        Assert.True(
            AndroidRemoteCapabilityResolver.IsHuaweiFamily("Huawei"));
        Assert.True(
            AndroidRemoteCapabilityResolver.IsHuaweiFamily("HONOR"));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.PendingValidation),
            Status(input, AndroidRemoteCapabilityResolver.ActionReboot));
    }

    [Fact]
    public void DeletedInGoogle_BlocksDeletionAndManagementActions()
    {
        var input = Input(
            managementMode: "DEVICE_OWNER",
            apiLevel: 34,
            isDeletedInGoogle: true);

        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionDelete));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionLock));
    }

    [Fact]
    public void UnknownManagementMode_RequiresValidation()
    {
        var input = Input(managementMode: null, apiLevel: null);

        Assert.Equal(
            nameof(AndroidCapabilityStatus.PendingValidation),
            Status(input, AndroidRemoteCapabilityResolver.ActionReboot));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.PendingValidation),
            Status(input, AndroidRemoteCapabilityResolver.ActionWipe));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.Available),
            Status(input, AndroidRemoteCapabilityResolver.ActionLock));
    }

    [Fact]
    public void UnmanagedDevice_BlocksManagementActions()
    {
        var input = Input(
            managementMode: "DEVICE_OWNER",
            apiLevel: 34,
            isManaged: false);

        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionLock));
        Assert.Equal(
            nameof(AndroidCapabilityStatus.NotSupported),
            Status(input, AndroidRemoteCapabilityResolver.ActionWipe));
    }

    private static string Status(
        AndroidRemoteCapabilityInput input,
        string action)
    {
        return AndroidRemoteCapabilityResolver
            .ResolveAction(input, action)
            .Status;
    }

    private static AndroidRemoteCapabilityInput Input(
        string? managementMode,
        int? apiLevel,
        string? manufacturer = "Google",
        bool isManaged = true,
        bool isDeletedInGoogle = false)
    {
        return new AndroidRemoteCapabilityInput(
            Manufacturer: manufacturer,
            Model: "Test",
            ApiLevel: apiLevel,
            ManagementMode: managementMode,
            State: isManaged ? "ACTIVE" : "DELETED",
            Ownership: "COMPANY_OWNED",
            IsManaged: isManaged,
            IsDeletedInGoogle: isDeletedInGoogle,
            LastStatusReportTimeUtc: DateTime.UtcNow);
    }
}
