using System.Text.Json;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Groups;

public sealed class DeviceGroupRuleEvaluator
{
    public bool Matches(
        Device device,
        string? ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            return false;
        }

        try
        {
            var rule =
                JsonSerializer.Deserialize<
                    DynamicGroupRule>(
                    ruleJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    });

            if (
                rule is null ||
                rule.Conditions is null ||
                rule.Conditions.Count == 0)
            {
                return false;
            }

            var evaluations =
                rule.Conditions
                    .Select(condition =>
                        EvaluateCondition(
                            device,
                            condition))
                    .ToArray();

            if (
                string.Equals(
                    rule.Operator,
                    "OR",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                return evaluations.Any(
                    result => result);
            }

            return evaluations.All(
                result => result);
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateCondition(
        Device device,
        DynamicGroupCondition condition)
    {
        if (
            !string.Equals(
                condition.Operator,
                "equals",
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        var expected =
            condition.Value?.Trim()
            ?? string.Empty;

        return condition.Field
            .Trim()
            .ToLowerInvariant()
            switch
            {
                "platform" =>
                    string.Equals(
                        device.Platform.ToString(),
                        expected,
                        StringComparison.OrdinalIgnoreCase),

                "status" =>
                    string.Equals(
                        device.Status.ToString(),
                        expected,
                        StringComparison.OrdinalIgnoreCase),

                "compliancestatus" =>
                    string.Equals(
                        device.ComplianceStatus.ToString(),
                        expected,
                        StringComparison.OrdinalIgnoreCase),

                "department" =>
                    string.Equals(
                        device.Department,
                        expected,
                        StringComparison.OrdinalIgnoreCase),

                "manufacturer" =>
                    string.Equals(
                        device.Manufacturer,
                        expected,
                        StringComparison.OrdinalIgnoreCase),

                "model" =>
                    string.Equals(
                        device.Model,
                        expected,
                        StringComparison.OrdinalIgnoreCase),

                _ => false
            };
    }

    private sealed class DynamicGroupRule
    {
        public string Operator { get; set; } =
            "AND";

        public List<DynamicGroupCondition>
            Conditions { get; set; } = [];
    }

    private sealed class DynamicGroupCondition
    {
        public string Field { get; set; } =
            string.Empty;

        public string Operator { get; set; } =
            "equals";

        public string Value { get; set; } =
            string.Empty;
    }
}