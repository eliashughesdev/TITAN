using System.Text.Json.Nodes;
using TitanMDM.Application.Assistant;

namespace TitanMDM.Infrastructure.Assistant;

/// <summary>
/// Registro de herramientas del asistente. Solo expone y
/// ejecuta herramientas de lectura cuyos permisos requeridos
/// posee el usuario. Nunca usa dispatch por reflexión: el
/// nombre debe existir en el catálogo y pasar la revalidación
/// de permisos en el backend.
/// </summary>
public sealed class AssistantToolRegistry
    : IAssistantToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAssistantTool>
        _tools;

    public AssistantToolRegistry(
        IEnumerable<IAssistantTool> tools)
    {
        _tools =
            tools
                .Where(
                    tool =>
                        tool.Descriptor.Kind ==
                            AssistantToolKind.Read)
                .GroupBy(
                    tool =>
                        tool.Descriptor.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AssistantToolDescriptor> Describe(
        AssistantPrincipal principal)
    {
        return _tools
            .Values
            .Where(
                tool =>
                    IsAuthorized(
                        tool.Descriptor,
                        principal))
            .Select(
                tool =>
                    tool.Descriptor)
            .OrderBy(
                descriptor =>
                    descriptor.Name,
                    StringComparer.Ordinal)
            .ToList();
    }

    public async Task<AssistantToolResult> ExecuteAsync(
        string toolName,
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        var name =
            toolName
                ?.Trim()
            ?? string.Empty;

        if (!_tools.TryGetValue(
                name,
                out var tool))
        {
            return AssistantToolResult.Fail(
                "tool_not_found",
                "La herramienta solicitada no existe o no está habilitada.");
        }

        if (!IsAuthorized(
                tool.Descriptor,
                context.Principal))
        {
            return AssistantToolResult.Fail(
                "forbidden",
                "No tienes permisos para ejecutar esa herramienta.");
        }

        return await tool.ExecuteAsync(
            context,
            arguments,
            cancellationToken);
    }

    private static bool IsAuthorized(
        AssistantToolDescriptor descriptor,
        AssistantPrincipal principal)
    {
        if (!principal.AssistantEnabled)
        {
            return false;
        }

        return descriptor.RequiredPermissions.All(
            permission =>
                principal.HasPermission(
                    permission));
    }
}
