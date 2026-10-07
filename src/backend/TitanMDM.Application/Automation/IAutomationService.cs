namespace TitanMDM.Application.Automation;

public interface IAutomationService
{
    Task<IReadOnlyCollection<AutomationRuleDto>>
        GetRulesAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default);

    Task<AutomationRuleDto?>
        GetRuleAsync(
            Guid organizationId,
            Guid ruleId,
            CancellationToken cancellationToken = default);

    Task<AutomationRuleDto>
        CreateAsync(
            Guid organizationId,
            Guid userId,
            CreateAutomationRuleRequest request,
            CancellationToken cancellationToken = default);

    Task<AutomationRuleDto>
        UpdateAsync(
            Guid organizationId,
            Guid ruleId,
            UpdateAutomationRuleRequest request,
            CancellationToken cancellationToken = default);

    Task SetEnabledAsync(
        Guid organizationId,
        Guid ruleId,
        bool enabled,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid organizationId,
        Guid ruleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AutomationExecutionDto>>
        GetExecutionsAsync(
            Guid organizationId,
            int limit,
            CancellationToken cancellationToken = default);

    Task ExecuteRuleAsync(
        Guid organizationId,
        Guid userId,
        Guid ruleId,
        ExecuteAutomationRequest request,
        CancellationToken cancellationToken = default);

    Task ProcessEventAsync(
        AutomationEvent automationEvent,
        CancellationToken cancellationToken = default);
}