using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using TitanMDM.Application.Automation;
using TitanMDM.Application.Commands;
using TitanMDM.Domain.Automation;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Infrastructure.Security;

namespace TitanMDM.IntegrationTests.Automation;

public sealed class AutomationLegacySecurityTests
{
    [Fact]
    public async Task EnterpriseRuleDoesNotFallBackToLegacyExecution()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var user = new User(
            organizationId,
            "Enterprise",
            "Operator",
            "enterprise.operator@example.test");
        var rule = NewRule(organizationId, user.Id);

        var definition = new AutomationRuleDefinition
        {
            Trigger = "Manual",
            Actions = new List<AutomationActionDto>
            {
                new()
                {
                    Code = "CreateNotification",
                    Parameters = new Dictionary<string, string?>
                    {
                        ["title"] = "Test",
                        ["message"] = "Test"
                    }
                }
            }
        };

        rule.ApplyDefinition(
            AutomationDefinitionSerializer.Serialize(
                definition),
            AutomationTriggerType.Manual,
            AutomationModules.General,
            100,
            null,
            user.Id);

        db.AddRange(
            user,
            rule,
            new UserScopeGrant(
                organizationId,
                user.Id,
                AuthorizationScopeType.Organization,
                organizationId,
                user.Id));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<
            AutomationPlatformUnavailableException>(
            () => service.ExecuteRuleAsync(
                organizationId,
                user.Id,
                rule.Id,
                new ExecuteAutomationRequest(null, "{}")));
    }

    [Fact]
    public async Task SiteScopedActorCannotEditLegacyOrganizationRule()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var user = new User(
            organizationId,
            "Regional",
            "Operator",
            "regional.operator@example.test");
        var site = new Site(
            organizationId,
            "REGION",
            "Regional site");
        var rule = NewRule(organizationId, user.Id);

        db.AddRange(
            user,
            site,
            rule,
            new UserScopeGrant(
                organizationId,
                user.Id,
                AuthorizationScopeType.Site,
                site.Id,
                user.Id));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<
            AutomationAuthorizationException>(
            () => service.UpdateAsync(
                organizationId,
                user.Id,
                rule.Id,
                new UpdateAutomationRuleRequest(
                    "Updated",
                    null,
                    "Manual",
                    "{}",
                    "Notification",
                    "{}",
                    true)));

        Assert.Empty(
            await service.GetRulesAsync(
                organizationId,
                user.Id));
    }

    private static AutomationService CreateService(
        TitanMdmDbContext db)
    {
        var provider = new ServiceCollection()
            .BuildServiceProvider();

        return new AutomationService(
            db,
            new UnusedCommandService(),
            new ScopeAccessService(db),
            provider);
    }

    private static AutomationRule NewRule(
        Guid organizationId,
        Guid userId)
    {
        return new AutomationRule(
            organizationId,
            "Rule",
            null,
            AutomationTriggerType.Manual,
            "{}",
            AutomationActionType.Notification,
            "{}",
            userId);
    }

    private static TitanMdmDbContext Open()
    {
        var options =
            new DbContextOptionsBuilder<
                    TitanMdmDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString("N"))
                .Options;

        return new TitanMdmDbContext(options);
    }

    private sealed class UnusedCommandService
        : IDeviceCommandService
    {
        public Task<DeviceCommandDto> CreateAsync(
            Guid organizationId,
            Guid createdByUserId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DeviceCommandDto?> GetByIdAsync(
            Guid organizationId,
            Guid commandId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DeviceCommandListResultDto>
            GetCommandsAsync(
                Guid organizationId,
                Guid? deviceId,
                string? status,
                int page,
                int pageSize,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task CancelAsync(
            Guid organizationId,
            Guid commandId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
