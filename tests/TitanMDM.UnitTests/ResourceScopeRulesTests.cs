using TitanMDM.Application.Security;

namespace TitanMDM.UnitTests;

public sealed class ResourceScopeRulesTests
{
    [Fact]
    public void OrganizationScope_CanAccessResourceWithoutSite()
    {
        Assert.True(
            ResourceScopeRules
                .CanAccessResource(
                    true,
                    Array.Empty<Guid>(),
                    null));
    }

    [Fact]
    public void SiteLimitedUser_CannotAccessResourceWithoutSite()
    {
        Assert.False(
            ResourceScopeRules
                .CanAccessResource(
                    false,
                    new[]
                    {
                        Guid.NewGuid()
                    },
                    null));
    }

    [Fact]
    public void SiteA_CanAccessResourceFromSiteA()
    {
        var siteA =
            Guid.NewGuid();

        Assert.True(
            ResourceScopeRules
                .CanAccessResource(
                    false,
                    new[]
                    {
                        siteA
                    },
                    siteA));
    }

    [Fact]
    public void SiteA_CannotAccessResourceFromSiteB()
    {
        var siteA =
            Guid.NewGuid();

        var siteB =
            Guid.NewGuid();

        Assert.False(
            ResourceScopeRules
                .CanAccessResource(
                    false,
                    new[]
                    {
                        siteA
                    },
                    siteB));
    }

    [Fact]
    public void MultipleSiteScopes_CanAccessOnlyAssignedSites()
    {
        var siteA =
            Guid.NewGuid();

        var siteB =
            Guid.NewGuid();

        var siteC =
            Guid.NewGuid();

        var scopes =
            new[]
            {
                siteA,
                siteB
            };

        Assert.True(
            ResourceScopeRules
                .CanAccessResource(
                    false,
                    scopes,
                    siteA));

        Assert.True(
            ResourceScopeRules
                .CanAccessResource(
                    false,
                    scopes,
                    siteB));

        Assert.False(
            ResourceScopeRules
                .CanAccessResource(
                    false,
                    scopes,
                    siteC));
    }
}