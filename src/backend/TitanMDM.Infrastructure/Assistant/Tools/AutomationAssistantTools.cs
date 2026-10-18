using System.Text.Json.Nodes;

using TitanMDM.Application.Assistant;
using TitanMDM.Application.Automation;

namespace TitanMDM.Infrastructure.Assistant.Tools;

public sealed class AutomationOverviewTool
    : AssistantToolBase
{
    private readonly IAutomationPlatformService _automation;

    public AutomationOverviewTool(
        IAutomationPlatformService automation)
    {
        _automation = automation;
    }

    public override AssistantToolDescriptor Descriptor =>
        new(
            "automation.overview",
            "Estado de automatizaciones",
            "Consulta reglas activas, fallos de las últimas 24 horas, cola pendiente y última ejecución. Admite module y ruleId como filtros.",
            AssistantToolKind.Read,
            new[]
            {
                AutomationPermissions.View
            });

    public override async Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var principal = context.Principal;
        var dashboard = await _automation.GetDashboardAsync(
            principal.OrganizationId,
            principal.UserId,
            cancellationToken);
        var rules = await _automation.GetEnterpriseRulesAsync(
            principal.OrganizationId,
            principal.UserId,
            cancellationToken);

        var module = ReadString(arguments, "module");
        var ruleId = ReadGuid(arguments, "ruleId");

        var filteredRules = rules
            .Where(rule =>
                string.IsNullOrWhiteSpace(module) ||
                string.Equals(
                    rule.Module,
                    module,
                    StringComparison.OrdinalIgnoreCase))
            .Where(rule =>
                !ruleId.HasValue ||
                rule.Id == ruleId.Value)
            .Select(rule => new
            {
                rule.Id,
                rule.Name,
                rule.IsEnabled,
                rule.Module,
                rule.ExecutionCount,
                rule.LastExecutedAtUtc,
                rule.ConsecutiveFailures
            })
            .ToList();

        var payload = new
        {
            dashboard.TotalRules,
            dashboard.ActiveRules,
            dashboard.ExecutionsLast24Hours,
            dashboard.FailuresLast24Hours,
            dashboard.QueuePending,
            dashboard.QueueDeadLetter,
            dashboard.SuccessRate,
            rules = filteredRules
        };

        return AssistantToolResult.Ok(
            $"Hay {dashboard.ActiveRules} automatizaciones activas, {dashboard.FailuresLast24Hours} fallos en 24 horas y {dashboard.QueuePending} ejecuciones pendientes.",
            AssistantDataSanitizer.WrapUntrustedData(
                "automation.overview",
                AssistantDataSanitizer.ToCompactJson(
                    payload,
                    4000)));
    }
}
