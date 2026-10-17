using System.Text.Json.Nodes;
using TitanMDM.Application.Assistant;

namespace TitanMDM.Infrastructure.Assistant.Tools;

public abstract class AssistantToolBase
    : IAssistantTool
{
    public abstract AssistantToolDescriptor Descriptor { get; }

    public abstract Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default);

    protected static string? ReadString(
        JsonObject arguments,
        string name)
    {
        if (!arguments.TryGetPropertyValue(
                name,
                out var node) ||
            node is null)
        {
            return null;
        }

        var value =
            node.ToString();

        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }

    protected static Guid? ReadGuid(
        JsonObject arguments,
        string name)
    {
        var raw =
            ReadString(
                arguments,
                name);

        return Guid.TryParse(
            raw,
            out var value)
            ? value
            : null;
    }

    protected static int ReadInt(
        JsonObject arguments,
        string name,
        int fallback,
        int minimum,
        int maximum)
    {
        var raw =
            ReadString(
                arguments,
                name);

        if (!int.TryParse(
                raw,
                out var value))
        {
            return fallback;
        }

        return Math.Clamp(
            value,
            minimum,
            maximum);
    }
}
