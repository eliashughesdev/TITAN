using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TitanMDM.Infrastructure.Assistant;

/// <summary>
/// Utilidades para tratar todo dato proveniente de la base
/// de datos o de servicios como contenido no confiable antes
/// de exponerlo al modelo: se eliminan caracteres de control,
/// se normalizan saltos y se trunca a un límite seguro.
/// </summary>
public static class AssistantDataSanitizer
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull,
            WriteIndented =
                false
        };

    public static string ToCompactJson(
        object value,
        int maxCharacters)
    {
        try
        {
            var json =
                JsonSerializer.Serialize(
                    value,
                    SerializerOptions);

            return Sanitize(
                json,
                maxCharacters);
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    public static string Sanitize(
        string? value,
        int maxCharacters)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return string.Empty;
        }

        var limit =
            Math.Max(
                64,
                maxCharacters);

        var builder =
            new StringBuilder(
                Math.Min(
                    value.Length,
                    limit));

        foreach (var character in value)
        {
            if (character == '\n' ||
                character == '\t' ||
                character == '\r')
            {
                builder.Append(
                    ' ');

                continue;
            }

            if (char.IsControl(
                    character))
            {
                continue;
            }

            builder.Append(
                character);

            if (builder.Length >= limit)
            {
                break;
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    /// <summary>
    /// Envuelve el resultado de una herramienta en un bloque
    /// delimitado e identificado como datos no confiables.
    /// </summary>
    public static string WrapUntrustedData(
        string toolName,
        string payload)
    {
        return
            "BEGIN_UNTRUSTED_TOOL_DATA " +
            toolName +
            "\n" +
            payload +
            "\nEND_UNTRUSTED_TOOL_DATA";
    }
}
