using System.Text.Json.Nodes;

namespace TitanMDM.Application.Assistant;

public interface IAssistantTool
{
    AssistantToolDescriptor Descriptor { get; }

    Task<AssistantToolResult> ExecuteAsync(
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default);
}

public interface IAssistantToolRegistry
{
    /// <summary>
    /// Herramientas que el usuario puede ejecutar según sus
    /// permisos efectivos. Nunca incluye acciones de escritura.
    /// </summary>
    IReadOnlyList<AssistantToolDescriptor> Describe(
        AssistantPrincipal principal);

    /// <summary>
    /// Ejecuta una herramienta revalidando nombre y permisos
    /// en el backend. Una herramienta desconocida o no
    /// autorizada devuelve un resultado fallido, nunca ejecuta.
    /// </summary>
    Task<AssistantToolResult> ExecuteAsync(
        string toolName,
        AssistantToolContext context,
        JsonObject arguments,
        CancellationToken cancellationToken = default);
}

public interface IAssistantSessionStore
{
    AssistantConversation Create(
        Guid organizationId,
        Guid userId,
        Guid conversationId,
        string title);

    AssistantConversation? Get(
        Guid organizationId,
        Guid userId,
        Guid conversationId);

    IReadOnlyList<AssistantConversationSummary> List(
        Guid organizationId,
        Guid userId);

    bool Delete(
        Guid organizationId,
        Guid userId,
        Guid conversationId);

    void Append(
        Guid organizationId,
        Guid userId,
        Guid conversationId,
        AssistantConversationEntry entry);

    int CountActive(
        Guid organizationId,
        Guid userId);
}

public interface IAssistantProvider
{
    string Name { get; }

    Task<AssistantProviderStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<AssistantProviderResponse> CompleteAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Selecciona el proveedor activo (OpenRouter / Ollama)
/// según configuración y disponibilidad, sin exponer
/// credenciales al resto del sistema.
/// </summary>
public interface IAssistantProviderRegistry
{
    Task<IAssistantProvider?> GetActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssistantProviderStatus>> GetStatusesAsync(
        CancellationToken cancellationToken = default);
}

public interface IAssistantOrchestrator
{
    Task<AssistantCapabilities> GetCapabilitiesAsync(
        AssistantPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<AssistantTurnResponse> ExecuteTurnAsync(
        AssistantPrincipal principal,
        AssistantToolContext toolContext,
        AssistantTurnRequest request,
        CancellationToken cancellationToken = default);
}
