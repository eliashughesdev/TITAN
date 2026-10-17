using System.Globalization;
using System.Text.Json;
using TitanMDM.Application.Automation;
using TitanMDM.Domain.Automation;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Engine;

internal sealed record AutomationConditionEvaluation(
    bool Passed,
    IReadOnlyList<AutomationConditionTraceDto> Traces);

/*
 * AUT-1.2 — Evaluador tipado de condiciones.
 *
 * Soporta grupos AND/OR anidados y operadores tipados sobre
 * el payload JSON del evento. NO usa eval, reflexión
 * dinámica ni SQL de usuario. Los valores se interpretan
 * como número, fecha, booleano o cadena de forma explícita.
 */
internal static class AutomationConditionEvaluator
{
    public static AutomationConditionEvaluation Evaluate(
        AutomationConditionGroupDto? group,
        string? payloadJson)
    {
        var traces =
            new List<AutomationConditionTraceDto>();

        var passed =
            EvaluateGroup(
                group,
                payloadJson,
                "root",
                traces);

        return new AutomationConditionEvaluation(
            passed,
            traces);
    }

    private static bool EvaluateGroup(
        AutomationConditionGroupDto? group,
        string? payloadJson,
        string path,
        List<AutomationConditionTraceDto> traces)
    {
        if (group is null)
            return true;

        var conditions =
            group.Conditions ?? new();

        var nested =
            group.Groups ?? new();

        if (conditions.Count == 0 &&
            nested.Count == 0)
            return true;

        var isAll =
            !string.Equals(
                group.Operator,
                "Any",
                StringComparison.OrdinalIgnoreCase);

        var results = new List<bool>();

        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];

            var (result, trace) =
                EvaluateCondition(
                    condition,
                    payloadJson,
                    $"{path}.conditions[{i}]");

            results.Add(result);
            traces.Add(trace);

            if (isAll && !result)
            {
                // Cortocircuito: el grupo ya no puede pasar,
                // pero continuamos para registrar trazas útiles
                // de todas las condiciones evaluadas hasta aquí.
                return false;
            }

            if (!isAll && result)
            {
                return true;
            }
        }

        for (var i = 0; i < nested.Count; i++)
        {
            var result = EvaluateGroup(
                nested[i],
                payloadJson,
                $"{path}.groups[{i}]",
                traces);

            results.Add(result);

            if (isAll && !result)
                return false;

            if (!isAll && result)
                return true;
        }

        return isAll
            ? results.All(x => x)
            : results.Any(x => x);
    }

    private static (
        bool Result,
        AutomationConditionTraceDto Trace)
        EvaluateCondition(
            AutomationConditionDto condition,
            string? payloadJson,
            string path)
    {
        var op =
            ParseOperator(condition.Operator);

        var (found, actual) =
            ResolveField(
                payloadJson,
                condition.Field);

        var result =
            Compare(
                op,
                found,
                actual,
                condition);

        return (
            result,
            new AutomationConditionTraceDto(
                path,
                condition.Field,
                op.ToString(),
                Describe(condition),
                found
                    ? actual.GetRawText()
                    : null,
                result));
    }

    private static bool Compare(
        AutomationConditionOperator op,
        bool found,
        JsonElement actual,
        AutomationConditionDto condition)
    {
        switch (op)
        {
            case AutomationConditionOperator.Exists:
                return found;

            case AutomationConditionOperator.NotExists:
                return !found;

            case AutomationConditionOperator.IsEmpty:
                return !found || IsEmpty(actual);

            case AutomationConditionOperator.IsNotEmpty:
                return found && !IsEmpty(actual);
        }

        if (!found)
            return false;

        var expected =
            condition.Value;

        switch (op)
        {
            case AutomationConditionOperator.Eq:
                return Equals(actual, expected);

            case AutomationConditionOperator.NotEq:
                return !Equals(actual, expected);

            case AutomationConditionOperator.Contains:
                return ToText(actual).Contains(
                    expected ?? string.Empty,
                    StringComparison
                        .OrdinalIgnoreCase);

            case AutomationConditionOperator.NotContains:
                return !ToText(actual).Contains(
                    expected ?? string.Empty,
                    StringComparison
                        .OrdinalIgnoreCase);

            case AutomationConditionOperator.StartsWith:
                return ToText(actual).StartsWith(
                    expected ?? string.Empty,
                    StringComparison
                        .OrdinalIgnoreCase);

            case AutomationConditionOperator.EndsWith:
                return ToText(actual).EndsWith(
                    expected ?? string.Empty,
                    StringComparison
                        .OrdinalIgnoreCase);

            case AutomationConditionOperator.GreaterThan:
                return Ordered(actual, expected) > 0;

            case AutomationConditionOperator
                .GreaterThanOrEqual:
                return Ordered(actual, expected) >= 0;

            case AutomationConditionOperator.LessThan:
                return Ordered(actual, expected) < 0;

            case AutomationConditionOperator
                .LessThanOrEqual:
                return Ordered(actual, expected) <= 0;

            case AutomationConditionOperator.In:
                return MatchesAnyList(
                    actual,
                    condition.Values);

            case AutomationConditionOperator.NotIn:
                return !MatchesAnyList(
                    actual,
                    condition.Values);

            default:
                return false;
        }
    }

    private static bool MatchesAnyList(
        JsonElement actual,
        List<string>? values)
    {
        if (values is null || values.Count == 0)
            return false;

        foreach (var value in values)
        {
            if (Equals(actual, value))
                return true;
        }

        return false;
    }

    private static bool Equals(
        JsonElement actual,
        string? expected)
    {
        expected ??= string.Empty;

        if (TryGetDecimal(actual, out var actualNumber) &&
            TryParseDecimal(expected, out var expectedNumber))
        {
            return actualNumber == expectedNumber;
        }

        if (TryGetDateTime(actual, out var actualDate) &&
            TryParseDateTime(expected, out var expectedDate))
        {
            return actualDate == expectedDate;
        }

        if (actual.ValueKind == JsonValueKind.True ||
            actual.ValueKind == JsonValueKind.False)
        {
            return bool.TryParse(
                       expected,
                       out var expectedBool) &&
                   actual.GetBoolean() == expectedBool;
        }

        return string.Equals(
            ToText(actual),
            expected,
            StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Devuelve -1, 0 o 1 comparando número/fecha/cadena.
     * Si no son del mismo tipo, compara como cadena.
     */
    private static int Ordered(
        JsonElement actual,
        string? expected)
    {
        expected ??= string.Empty;

        if (TryGetDecimal(actual, out var actualNumber) &&
            TryParseDecimal(expected, out var expectedNumber))
        {
            return actualNumber.CompareTo(
                expectedNumber);
        }

        if (TryGetDateTime(actual, out var actualDate) &&
            TryParseDateTime(expected, out var expectedDate))
        {
            return actualDate.CompareTo(expectedDate);
        }

        return string.Compare(
            ToText(actual),
            expected,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmpty(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;

            case JsonValueKind.String:
                return string.IsNullOrWhiteSpace(
                    value.GetString());

            case JsonValueKind.Array:
                return value.GetArrayLength() == 0;

            case JsonValueKind.Object:
                return !value.EnumerateObject()
                    .Any();

            default:
                return false;
        }
    }

    private static (
        bool Found,
        JsonElement Value)
        ResolveField(
            string? payloadJson,
            string field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return (false, default);

        if (string.IsNullOrWhiteSpace(payloadJson))
            return (false, default);

        try
        {
            using var document =
                JsonDocument.Parse(payloadJson);

            var current =
                document.RootElement;

            foreach (
                var segment in field.Split(
                    '.',
                    StringSplitOptions
                        .RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            {
                if (current.ValueKind !=
                        JsonValueKind.Object ||
                    !TryGetProperty(
                        current,
                        segment,
                        out var next))
                {
                    return (false, default);
                }

                current = next;
            }

            return (true, current.Clone());
        }
        catch (JsonException)
        {
            return (false, default);
        }
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        foreach (var property in
            element.EnumerateObject())
        {
            if (property.Name.Equals(
                    name,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ToText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String =>
                value.GetString() ?? string.Empty,
            JsonValueKind.Null =>
                string.Empty,
            JsonValueKind.Undefined =>
                string.Empty,
            _ => value.GetRawText()
        };
    }

    private static bool TryGetDecimal(
        JsonElement value,
        out decimal number)
    {
        number = 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number =>
                value.TryGetDecimal(out number),
            JsonValueKind.String =>
                TryParseDecimal(
                    value.GetString(),
                    out number),
            _ => false
        };
    }

    private static bool TryParseDecimal(
        string? value,
        out decimal number)
    {
        return decimal.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool TryGetDateTime(
        JsonElement value,
        out DateTime date)
    {
        date = default;

        if (value.ValueKind ==
            JsonValueKind.String)
        {
            return TryParseDateTime(
                value.GetString(),
                out date);
        }

        return false;
    }

    private static bool TryParseDateTime(
        string? value,
        out DateTime date)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles
                .AdjustToUniversal |
            DateTimeStyles.AssumeUniversal,
            out date);
    }

    private static AutomationConditionOperator
        ParseOperator(string? value)
    {
        if (Enum.TryParse<
                AutomationConditionOperator>(
                value,
                true,
                out var result))
        {
            return result;
        }

        return AutomationConditionOperator.Eq;
    }

    private static string? Describe(
        AutomationConditionDto condition)
    {
        if (condition.Values is { Count: > 0 })
        {
            return string.Join(
                ", ",
                condition.Values);
        }

        return condition.Value;
    }
}
