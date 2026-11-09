using System.Text.Json;
using System.Text.Json.Nodes;

namespace TitanMDM.Infrastructure.Commands;

/// <summary>
/// Oculta los secretos del payload de un comando (PIN temporal de
/// LOCK_DEVICE) en todo lo que ve la consola: historial, SignalR y
/// auditoría. El agente recibe el payload original.
/// </summary>
internal static class CommandPayloadRedactor
{
    public const string Mask = "••••";

    private static readonly string[] SecretKeys = ["pin"];

    public static string Redact(string commandType, string payloadJson)
    {
        if (!string.Equals(commandType, "LOCK_DEVICE", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(payloadJson)
            || !payloadJson.Contains("pin", StringComparison.OrdinalIgnoreCase))
        {
            return payloadJson;
        }

        JsonObject? payload;
        try
        {
            payload = JsonNode.Parse(payloadJson) as JsonObject;
        }
        catch (JsonException)
        {
            return payloadJson;
        }

        if (payload is null)
        {
            return payloadJson;
        }

        var changed = false;
        foreach (var key in SecretKeys)
        {
            var match = payload
                .Select(x => x.Key)
                .FirstOrDefault(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));

            if (match is not null && payload[match] is not null)
            {
                payload[match] = Mask;
                changed = true;
            }
        }

        return changed
            ? payload.ToJsonString()
            : payloadJson;
    }

    /// <summary>LOCK_DEVICE con PIN o mensaje: bloqueo por extravío.</summary>
    public static bool IsLostModeLock(string commandType, string? payloadJson)
    {
        if (!string.Equals(commandType?.Trim(), "LOCK_DEVICE", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(payloadJson) is not JsonObject payload)
            {
                return false;
            }

            return payload.Any(x =>
                (string.Equals(x.Key, "pin", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(x.Key, "message", StringComparison.OrdinalIgnoreCase))
                && x.Value is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
