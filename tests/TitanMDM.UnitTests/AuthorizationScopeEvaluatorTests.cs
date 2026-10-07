using TitanMDM.Application.Security;

using TitanMDM.Domain.Enums;

namespace TitanMDM.UnitTests;

public sealed class AuthorizationScopeEvaluatorTests
{
    [Fact]
    public void OrganizationScope_AllowsChildScopes()
    {
        var organizationId =
            Guid.NewGuid();

        var departmentId =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationId,
                    AuthorizationScopeType.Organization,
                    organizationId)
            };

        var allowed =
            AuthorizationScopeEvaluator.CanAccess(
                organizationId,
                grants,
                AuthorizationScopeType.Department,
                departmentId);

        Assert.True(
            allowed);
    }

    [Fact]
    public void ExactDepartmentScope_AllowsDepartment()
    {
        var organizationId =
            Guid.NewGuid();

        var departmentId =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationId,
                    AuthorizationScopeType.Department,
                    departmentId)
            };

        Assert.True(
            AuthorizationScopeEvaluator.CanAccess(
                organizationId,
                grants,
                AuthorizationScopeType.Department,
                departmentId));
    }

    [Fact]
    public void DifferentDepartmentScope_IsDenied()
    {
        var organizationId =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    organizationId,
                    AuthorizationScopeType.Department,
                    Guid.NewGuid())
            };

        Assert.False(
            AuthorizationScopeEvaluator.CanAccess(
                organizationId,
                grants,
                AuthorizationScopeType.Department,
                Guid.NewGuid()));
    }

    [Fact]
    public void ScopeFromDifferentOrganization_IsDenied()
    {
        var organizationId =
            Guid.NewGuid();

        var departmentId =
            Guid.NewGuid();

        var grants =
            new[]
            {
                new AuthorizationScopeDescriptor(
                    Guid.NewGuid(),
                    AuthorizationScopeType.Department,
                    departmentId)
            };

        Assert.False(
            AuthorizationScopeEvaluator.CanAccess(
                organizationId,
                grants,
                AuthorizationScopeType.Department,
                departmentId));
    }

    [Fact]
    public void GroupDoesNotImplicitlyGrantDepartment()
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

        Assert.False(
            AuthorizationScopeEvaluator.CanAccess(
                organizationId,
                grants,
                AuthorizationScopeType.Department,
                id));
    }
}