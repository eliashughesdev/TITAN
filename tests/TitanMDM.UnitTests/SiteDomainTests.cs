using TitanMDM.Domain.Entities;

namespace TitanMDM.UnitTests;

public sealed class SiteDomainTests
{
    [Fact]
    public void Site_NormalizesCode()
    {
        var site =
            new Site(
                Guid.NewGuid(),
                " sto-dgo ",
                "Santo Domingo");

        Assert.Equal(
            "STO-DGO",
            site.Code);
    }

    [Fact]
    public void Site_CanBeDeactivatedAndActivated()
    {
        var site =
            new Site(
                Guid.NewGuid(),
                "SITE-01",
                "Site 01");

        site.Deactivate();

        Assert.False(
            site.IsActive);

        site.Activate();

        Assert.True(
            site.IsActive);
    }

    [Fact]
    public void SiteLocation_RequiresSite()
    {
        Assert.Throws<
            ArgumentException>(
                () =>
                    new SiteLocation(
                        Guid.NewGuid(),
                        Guid.Empty,
                        "Nave 1"));
    }

    [Fact]
    public void Location_CanBeUpdated()
    {
        var location =
            new SiteLocation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Nave A");

        location.Update(
            "Nave B",
            "Área operativa");

        Assert.Equal(
            "Nave B",
            location.Name);

        Assert.Equal(
            "Área operativa",
            location.Description);
    }
}