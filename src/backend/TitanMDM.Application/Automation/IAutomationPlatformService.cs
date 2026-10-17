namespace TitanMDM.Application.Automation;

public interface IAutomationPlatformService
{
    Task<IReadOnlyCollection<
            AutomationEventDescriptorDto>>
        GetEventCatalogAsync(
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<
            AutomationActionDescriptorDto>>
        GetActionCatalogAsync(
            CancellationToken cancellationToken = default);

    Task<AutomationDefinitionValidationDto>
        ValidateDefinitionAsync(
            AutomationRuleDefinition definition,
            CancellationToken cancellationToken = default);

    Task<AutomationEnterpriseRuleDto>
        CreateEnterpriseRuleAsync(
            Guid organizationId,
            Guid userId,
            EnterpriseCreateRuleRequest request,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<
            AutomationEnterpriseRuleDto>>
        GetEnterpriseRulesAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AutomationIntegrationDto>>
        GetIntegrationsAsync(
            CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AutomationToolContractDto>>
        GetToolContractsAsync(
            CancellationToken cancellationToken = default);

    Task<AutomationEnterpriseRuleDto>
        UpdateEnterpriseRuleAsync(
            Guid organizationId,
            Guid ruleId,
            Guid userId,
            EnterpriseUpdateRuleRequest request,
            CancellationToken cancellationToken = default);

    Task<AutomationSimulationResultDto>
        SimulateAsync(
            Guid organizationId,
            Guid userId,
            Guid ruleId,
            SimulateRuleRequest request,
            CancellationToken cancellationToken = default);

    Task<AutomationDashboardDto> GetDashboardAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AutomationExecutionDetailDto?>
        GetExecutionDetailAsync(
            Guid organizationId,
            Guid userId,
            Guid executionId,
            CancellationToken cancellationToken = default);

    Task<bool> RetryExecutionAsync(
        Guid organizationId,
        Guid userId,
        Guid executionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<
            AutomationNotificationDto>>
        GetNotificationsAsync(
            Guid organizationId,
            Guid userId,
            int limit,
            CancellationToken cancellationToken = default);

    Task<bool> MarkNotificationReadAsync(
        Guid organizationId,
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<AutomationScheduleDto?> GetScheduleAsync(
        Guid organizationId,
        Guid userId,
        Guid ruleId,
        CancellationToken cancellationToken = default);

    Task<AutomationScheduleDto> SaveScheduleAsync(
        Guid organizationId,
        Guid ruleId,
        Guid userId,
        AutomationScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteScheduleAsync(
        Guid organizationId,
        Guid userId,
        Guid ruleId,
        CancellationToken cancellationToken = default);

    Task<AutomationQueueStatsDto> GetQueueStatsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
