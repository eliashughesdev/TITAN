using System.Text.Json;
using System.Text.Json.Serialization;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Engine;

/*
 * Serialización compartida del motor empresarial.
 * camelCase para que la consola React pueda enviar y
 * recibir el mismo contrato. Lectura tolerante a mayúsculas.
 */
internal static class AutomationDefinitionSerializer
{
    public static readonly JsonSerializerOptions Options =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(
            value,
            Options);
    }

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(
                json,
                Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
