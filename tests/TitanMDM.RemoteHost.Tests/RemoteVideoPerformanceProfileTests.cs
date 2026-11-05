using TitanMDM.RemoteHost.Transport;

namespace TitanMDM.RemoteHost.Tests;

public sealed class RemoteVideoPerformanceProfileTests
{
    [Fact]
    public void Default_IsAutoAt30Fps()
    {
        var profile = new RemoteVideoPerformanceProfile();

        Assert.Equal("auto", profile.Preset);
        Assert.Equal(30, profile.TargetFps);
        Assert.Equal(58, profile.JpegQuality);
    }

    [Fact]
    public void QualityPreset_RaisesJpegQualityAndResolution()
    {
        var profile = new RemoteVideoPerformanceProfile();

        profile.ApplyViewerPreference("quality", 0, 0);

        Assert.Equal(80, profile.JpegQuality);
        Assert.Equal(1920, profile.MaximumWidth);
    }

    [Fact]
    public void MaxWidthAndFps_AreHonored()
    {
        var profile = new RemoteVideoPerformanceProfile();

        profile.ApplyViewerPreference("balanced", 1280, 15);

        Assert.True(profile.MaximumWidth <= 1280);
        Assert.Equal(15, profile.TargetFps);
    }

    [Fact]
    public void Congestion_NeverDropsBelowPresetFloor()
    {
        var profile = new RemoteVideoPerformanceProfile();
        profile.ApplyViewerPreference("quality", 0, 0);

        for (var i = 0; i < 50; i++)
        {
            profile.ReportCongestion();
        }

        Assert.True(profile.JpegQuality >= 60);
        Assert.True(profile.MaximumWidth >= 1280);
    }

    [Fact]
    public void StableNetwork_RecoversUpToPresetCeiling()
    {
        var profile = new RemoteVideoPerformanceProfile();
        profile.ApplyViewerPreference("speed", 0, 0);

        for (var i = 0; i < 5_000; i++)
        {
            profile.ReportFrameCompleted(TimeSpan.FromMilliseconds(5));
        }

        Assert.True(profile.JpegQuality <= 55);
        Assert.True(profile.MaximumWidth <= 1366);
        Assert.Equal(55, profile.JpegQuality);
    }
}
