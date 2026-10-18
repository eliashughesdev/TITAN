using System.Text.Json.Nodes;
using TitanMDM.Application.Security;

namespace TitanMDM.Application.Assistant;

/// <summary>
/// Clasificación de seguridad de una herramienta del
/// asistente. Solo <see cref="Read"/> puede ejecutarse
/// sin confirmación adicional del usuario.
/// </summary>
public enum AssistantToolKind
{
    /// <summary>
    /// Consulta de solo lectura. No modifica estado.
    /// </summary>
    Read = 0,

    /// <summary>
    /// Acción que modifica estado. Requiere confirmación
    /// explícita del usuario y auditoría antes de habilitarse.
    /// </summary>
    Action = 1,

    /// <summary>
    /// Acción de alto impacto (bloqueo, borrado, cambio de
    /// políticas, ejecución remota). Permanemente deshabilitada
    /// hasta contar con confirmaciones fuertes y auditoría.
    /// </summary>
    Sensitive = 2
}

/// <summary>
/// Identidad verificada del usuario que conversa con el
/// asistente. Se resuelve en el backend a partir del token,
/// nunca a partir del texto del modelo.
/// </summary>
public sealed record AssistantPrincipal(
    Guid OrganizationId,
    Guid UserId,
    string DisplayName,
    string Email,
    bool AssistantEnabled,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions)
{
    public bool HasPermission(string code)
    {
        return Permissions.Contains(
            code,
            StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Contexto de ejecución de una herramienta: quién la
/// ejecuta, con qué alcance de sitio y desde qué módulo.
/// </summary>
public sealed record AssistantToolContext(
    AssistantPrincipal Principal,
    AuthorizationScopeSnapshot Scope,
    IReadOnlyCollection<Guid> AccessibleSiteIds,
    string? Module,
    string? IpAddress = null);

/// <summary>
/// Descripción pública de una herramienta. El backend es
/// la única fuente de verdad sobre disponibilidad y permisos.
/// </summary>
public sealed record AssistantToolDescriptor(
    string Name,
    string Title,
    string Description,
    AssistantToolKind Kind,
    IReadOnlyList<string> RequiredPermissions);

/// <summary>
/// Resultado de ejecutar una herramienta. <see cref="DataJson"/>
/// ya viene saneado y truncado hacia el modelo.
/// </summary>
public sealed record AssistantToolResult(
    bool Success,
    string Summary,
    string? DataJson = null,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static AssistantToolResult Ok(
        string summary,
        string? dataJson = null)
    {
        return new AssistantToolResult(
            true,
            summary,
            dataJson);
    }

    public static AssistantToolResult Fail(
        string errorCode,
        string errorMessage)
    {
        return new AssistantToolResult(
            false,
            errorMessage,
            null,
            errorCode,
            errorMessage);
    }
}

/// <summary>
/// Traza de una invocación de herramienta. Se persiste como
/// historial y se usa para trazabilidad de la sesión.
/// </summary>
public sealed record AssistantToolInvocation(
    string Tool,
    string Kind,
    DateTime StartedAtUtc,
    long DurationMs,
    bool Success,
    string? ErrorCode);

public sealed record AssistantConversationEntry(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    IReadOnlyList<AssistantToolInvocation> ToolInvocations);

public sealed record AssistantConversation(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<AssistantConversationEntry> Entries);

public sealed record AssistantConversationSummary(
    Guid Id,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MessageCount);

public sealed record AssistantTurnRequest(
    Guid? ConversationId,
    string Message,
    string? Module);

public sealed record AssistantTurnResponse(
    Guid ConversationId,
    string Answer,
    string Provider,
    string Model,
    IReadOnlyList<AssistantToolInvocation> ToolInvocations,
    IReadOnlyList<string> AvailableTools,
    bool Cancelled = false);

public sealed record AssistantProviderMessage(
    string Role,
    string Content);

public sealed record AssistantProviderRequest(
    string SystemPrompt,
    IReadOnlyList<AssistantProviderMessage> Messages,
    double Temperature,
    int MaxTokens,
    bool JsonMode,
    string? Model);

public sealed record AssistantProviderResponse(
    string Content,
    string Model,
    string Provider);

public sealed record AssistantProviderStatus(
    string Name,
    bool Enabled,
    bool Available,
    string Model,
    string? Detail = null,
    DateTime? CheckedAtUtc = null,
    long? LatencyMs = null);

public sealed record AssistantCapabilities(
    bool Enabled,
    bool Available,
    string Provider,
    string Model,
    IReadOnlyList<AssistantToolDescriptor> Tools);
