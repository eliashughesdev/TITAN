using TitanMDM.Application.Automation;
using TitanMDM.Domain.Automation;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Engine;

internal static class AutomationRuleEngine
{
    public static AutomationRuleDefinition?
        DeserializeDefinition(AutomationRule rule)
    {
        return AutomationDefinitionSerializer
            .Deserialize<AutomationRuleDefinition>(
                rule.DefinitionJson);
    }

    public static AutomationConditionEvaluation Evaluate(
        AutomationRule rule,
        string? payloadJson)
    {
        var definition =
            DeserializeDefinition(rule);

        if (definition is null)
        {
            // Regla sin definición empresarial: sin
            // condiciones adicionales (no debería llegar
            // aquí en la ruta empresarial).
            return new AutomationConditionEvaluation(
                true,
                Array.Empty<
                    AutomationConditionTraceDto>());
        }

        return AutomationConditionEvaluator.Evaluate(
            definition.Conditions,
            payloadJson);
    }

    public static bool IncludesSite(
        AutomationRuleDefinition definition,
        Guid? siteId)
    {
        var scope =
            definition.ScopeSiteIds;

        if (scope is null || scope.Count == 0)
            return true;

        return siteId.HasValue &&
            scope.Contains(siteId.Value);
    }
}
