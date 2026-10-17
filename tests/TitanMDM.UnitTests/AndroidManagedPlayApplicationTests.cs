using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

namespace TitanMDM.UnitTests;

public sealed class AndroidManagedPlayApplicationTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid UserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesPackageName()
    {
        var application = Create("  Com.Acme.Mail  ");

        Assert.Equal("com.acme.mail", application.PackageName);
        Assert.Equal(
            AndroidManagedPlayApprovalState.Pending,
            application.ApprovalState);
        Assert.False(application.HasOfficialMetadata);
        Assert.False(application.IsRemoved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("com")]
    [InlineData("com.")]
    [InlineData(".com.acme")]
    [InlineData("com..acme")]
    [InlineData("com.acme.")]
    [InlineData("com.1acme")]
    [InlineData("com.acme app")]
    [InlineData("com/acme")]
    public void Constructor_RejectsInvalidPackageName(string packageName)
    {
        Assert.Throws<ArgumentException>(() => Create(packageName));
    }

    [Theory]
    [InlineData("com.acme.mail", true)]
    [InlineData("com.acme.mail_v2", true)]
    [InlineData("single", false)]
    [InlineData("com.", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidPackageName_Classifies(
        string? packageName,
        bool expected)
    {
        Assert.Equal(
            expected,
            AndroidManagedPlayApplication.IsValidPackageName(packageName));
    }

    [Fact]
    public void ApplyOfficialMetadata_MarksAvailable()
    {
        var application = Create("com.acme.mail");

        application.ApplyOfficialMetadata(
            "Acme Mail",
            "https://play-lh.googleusercontent.com/icon.png",
            "Correo corporativo",
            "3.4.1",
            DateTime.UtcNow);

        Assert.Equal(
            AndroidManagedPlayApprovalState.Available,
            application.ApprovalState);
        Assert.Equal("Acme Mail", application.DisplayTitle);
        Assert.Equal("3.4.1", application.LatestVersionName);
        Assert.True(application.HasOfficialMetadata);
        Assert.Null(application.MetadataError);
    }

    [Fact]
    public void MarkMetadataUnavailable_DoesNotInventMetadata()
    {
        var application = Create("com.acme.mail");

        application.MarkMetadataUnavailable(
            "No disponible",
            DateTime.UtcNow);

        Assert.Equal(
            AndroidManagedPlayApprovalState.Unavailable,
            application.ApprovalState);
        Assert.Null(application.DisplayTitle);
        Assert.Null(application.IconUrl);
        Assert.Null(application.LatestVersionName);
        Assert.True(application.HasOfficialMetadata);
    }

    [Fact]
    public void RemoveAndReactivate_TogglesSoftDelete()
    {
        var application = Create("com.acme.mail");

        application.Remove();

        Assert.True(application.IsRemoved);
        Assert.NotNull(application.RemovedAtUtc);

        application.Reactivate();

        Assert.False(application.IsRemoved);
        Assert.Null(application.RemovedAtUtc);
    }

    [Fact]
    public void ApplyOfficialMetadata_TruncatesOverlongValues()
    {
        var application = Create("com.acme.mail");

        application.ApplyOfficialMetadata(
            new string('t', AndroidManagedPlayApplication.MaxDisplayTitleLength + 50),
            null,
            null,
            null,
            DateTime.UtcNow);

        Assert.NotNull(application.DisplayTitle);
        Assert.Equal(
            AndroidManagedPlayApplication.MaxDisplayTitleLength,
            application.DisplayTitle!.Length);
    }

    private static AndroidManagedPlayApplication Create(string packageName)
    {
        return new AndroidManagedPlayApplication(
            OrganizationId,
            packageName,
            AndroidManagedPlayApplicationSource.ManagedPlayIframe,
            UserId);
    }
}
