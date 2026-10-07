using TitanMDM.Application.Devices.Naming;

namespace TitanMDM.UnitTests;

public sealed class DeviceNameParserTests
{
    private static DeviceNamingOptions
        CreateOptions()
    {
        return new DeviceNamingOptions
        {
            Enabled =
                true,

            OrganizationPrefix =
                "CI",

            Cities =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["SPM"] =
                        "San Pedro de Macoris"
                },

            Areas =
                new Dictionary<string, DeviceAreaMapping>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["CEDI"] =
                        new()
                        {
                            SiteName =
                                "CEDI SPM"
                        },

                    ["FACT"] =
                        new()
                        {
                            SiteName =
                                "CESAR IGLESIAS",

                            SiteLocationName =
                                "FACTURACION"
                        }
                }
        };
    }

    [Fact]
    public void ParseLaptopCedi()
    {
        var result =
            DeviceNameParser.Parse(
                "CILSPMCEDI01",
                CreateOptions());

        Assert.True(
            result.Matched);

        Assert.True(
            result.Valid);

        Assert.Equal(
            "CI",
            result.OrganizationCode);

        Assert.Equal(
            "L",
            result.DeviceTypeCode);

        Assert.Equal(
            "Laptop",
            result.DeviceType);

        Assert.Equal(
            "SPM",
            result.CityCode);

        Assert.Equal(
            "CEDI",
            result.AreaCode);

        Assert.Equal(
            "CEDI SPM",
            result.SuggestedSiteName);

        Assert.Equal(
            1,
            result.Sequence);
    }

    [Fact]
    public void ParseDesktopFacturacion()
    {
        var result =
            DeviceNameParser.Parse(
                "CIDSPMFACT01",
                CreateOptions());

        Assert.True(
            result.Valid);

        Assert.Equal(
            "Desktop",
            result.DeviceType);

        Assert.Equal(
            "FACT",
            result.AreaCode);

        Assert.Equal(
            "CESAR IGLESIAS",
            result.SuggestedSiteName);

        Assert.Equal(
            "FACTURACION",
            result.SuggestedSiteLocationName);
    }

    [Fact]
    public void ParseIsCaseInsensitive()
    {
        var result =
            DeviceNameParser.Parse(
                "cilspmcedi01",
                CreateOptions());

        Assert.True(
            result.Valid);

        Assert.Equal(
            "CILSPMCEDI01",
            result.DeviceName);
    }

    [Fact]
    public void UnknownDeviceTypeFails()
    {
        var result =
            DeviceNameParser.Parse(
                "CIXSPMCEDI01",
                CreateOptions());

        Assert.False(
            result.Valid);
    }

    [Fact]
    public void WrongOrganizationPrefixFails()
    {
        var result =
            DeviceNameParser.Parse(
                "XXLSPMCEDI01",
                CreateOptions());

        Assert.False(
            result.Valid);
    }
}