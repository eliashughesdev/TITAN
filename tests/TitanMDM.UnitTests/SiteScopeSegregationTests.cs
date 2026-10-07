using TitanMDM.Application.Security;

using TitanMDM.Domain.Enums;

namespace TitanMDM.UnitTests;

public sealed class SiteScopeSegregationTests
{
    [Fact]
    public void SiteA_DoesNotGrantAccessToSiteB()
    {
        var organizationId =
            Guid.NewGuid();

        var siteA =
            Guid.NewGuid();

        var siteB =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationId,
                    AuthorizationScopeType.Site,
                    siteA)
            };

        var canAccessA =
            AuthorizationScopeEvaluator
                .CanAccess(
                    organizationId,
                    grants,
                    AuthorizationScopeType.Site,
                    siteA);

        var canAccessB =
            AuthorizationScopeEvaluator
                .CanAccess(
                    organizationId,
                    grants,
                    AuthorizationScopeType.Site,
                    siteB);

        Assert.True(
            canAccessA);

        Assert.False(
            canAccessB);
    }

    [Fact]
    public void OrganizationScope_AllowsAllSitesInsideOrganization()
    {
        var organizationId =
            Guid.NewGuid();

        var siteA =
            Guid.NewGuid();

        var siteB =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationId,
                    AuthorizationScopeType.Organization,
                    organizationId)
            };

        Assert.True(
            AuthorizationScopeEvaluator
                .CanAccess(
                    organizationId,
                    grants,
                    AuthorizationScopeType.Site,
                    siteA));

        Assert.True(
            AuthorizationScopeEvaluator
                .CanAccess(
                    organizationId,
                    grants,
                    AuthorizationScopeType.Site,
                    siteB));
    }

    [Fact]
    public void SiteScope_FromAnotherOrganization_IsDenied()
    {
        var organizationA =
            Guid.NewGuid();

        var organizationB =
            Guid.NewGuid();

        var site =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationA,
                    AuthorizationScopeType.Site,
                    site)
            };

        var result =
            AuthorizationScopeEvaluator
                .CanAccess(
                    organizationB,
                    grants,
                    AuthorizationScopeType.Site,
                    site);

        Assert.False(
            result);
    }

    [Fact]
    public void DepartmentScope_DoesNotImplicitlyGrantSite()
    {
        var organizationId =
            Guid.NewGuid();

        var id =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationId,
                    AuthorizationScopeType.Department,
                    id)
            };

        var result =
            AuthorizationScopeEvaluator
                .CanAccess(
                    organizationId,
                    grants,
                    AuthorizationScopeType.Site,
                    id);

        Assert.False(
            result);
    }

    [Fact]
    public void GroupScope_DoesNotImplicitlyGrantSite()
    {
        var organizationId =
            Guid.NewGuid();

        var id =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationId,
                    AuthorizationScopeType.Group,
                    id)
            };

        var result =
            AuthorizationScopeEvaluator
                .CanAccess(
                    organizationId,
                    grants,
                    AuthorizationScopeType.Site,
                    id);

        Assert.False(
            result);
    }
}