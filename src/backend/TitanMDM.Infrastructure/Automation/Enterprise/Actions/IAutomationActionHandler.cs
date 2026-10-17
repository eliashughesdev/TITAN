using TitanMDM.Application.Automation;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Actions;

internal sealed record AutomationActionContext(
    Guid OrganizationId,
    Guid? DeviceId,
    Guid ActorUserId,
    Guid? AutomationRuleId,
    Guid? AutomationExecutionId,
    AutomationActionDto Action,
    string TriggerPayloadJson,
    bool Simulated,
    int CycleDepth,
    IReadOnlyCollection<Guid> ScopeSiteIds);

internal sealed record AutomationActionResult(
    bool Success,
    string? Message = null,
    string? ResultJson = null);

/*
 * AUT-1.4 — Contrato de una acción autorizada.
 *
 * Cada handler conoce su código, valida sus parámetros
 * y ejecuta la acción registrando un resultado real.
 */
internal interface IAutomationActionHandler
{
    string Code { get; }

    Task<AutomationActionResult> ExecuteAsync(
        AutomationActionContext context,
        CancellationToken cancellationToken = default);
}
