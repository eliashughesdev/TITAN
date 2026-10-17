using System.Reflection;

using TitanMDM.Api.Controllers;
using TitanMDM.Api.Security;
using TitanMDM.Application.Automation;

namespace TitanMDM.IntegrationTests.Automation;

public sealed class AutomationControllerPermissionTests
{
    [Theory]
    [InlineData(
        nameof(AutomationController.CreateEnterpriseRule),
        "permission:automation.create|automation.manage|automation.admin")]
    [InlineData(
        nameof(AutomationController.UpdateEnterpriseRule),
        "permission:automation.edit|automation.manage|automation.admin")]
    [InlineData(
        nameof(AutomationController.Simulate),
        "permission:automation.simulate|automation.admin")]
    [InlineData(
        nameof(AutomationController.RetryExecution),
        "permission:automation.execute|automation.admin")]
    [InlineData(
        nameof(AutomationController.Dashboard),
        "permission:automation.view|automation.manage|automation.admin")]
    [InlineData(
        nameof(AutomationController.DeleteSchedule),
        "permission:automation.admin|automation.manage")]
    public void EnterpriseEndpointRequiresExpectedPermission(
        string methodName,
        string expectedPolicy)
    {
        var method = typeof(AutomationController)
            .GetMethod(methodName)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' was not found.");

        var attribute = method.GetCustomAttribute<
            RequireAnyPermissionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(expectedPolicy, attribute.Policy);
    }

    [Theory]
    [InlineData(nameof(
        IAutomationPlatformService.CreateEnterpriseRuleAsync))]
    [InlineData(nameof(
        IAutomationPlatformService.UpdateEnterpriseRuleAsync))]
    public void EnterpriseMutationReturnsEnterpriseContract(
        string methodName)
    {
        var method = typeof(IAutomationPlatformService)
            .GetMethod(methodName)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' was not found.");

        Assert.Equal(
            typeof(Task<AutomationEnterpriseRuleDto>),
            method.ReturnType);
    }
}
