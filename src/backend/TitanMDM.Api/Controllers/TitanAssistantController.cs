using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using TitanMDM.Api.AI.Assistant;
using TitanMDM.Application.Assistant;
using TitanMDM.Application.Security;

namespace TitanMDM.Api.Controllers;

/// <summary>
/// Superficie HTTP del asistente empresarial. Toda ejecución de
/// herramientas se revalida en el backend; el modelo nunca
/// decide permisos ni ejecuta acciones.
/// </summary>
[ApiController]
[Authorize]
[Route("api/titan-assistant")]
public sealed class TitanAssistantController
    : ControllerBase
{
    private readonly AssistantPrincipalResolver _resolver;
    private readonly IAssistantOrchestrator _orchestrator;
    private readonly IAssistantSessionStore _sessionStore;
    private readonly IScopeAccessService _scopeAccess;

    public TitanAssistantController(
        AssistantPrincipalResolver resolver,
        IAssistantOrchestrator orchestrator,
        IAssistantSessionStore sessionStore,
        IScopeAccessService scopeAccess)
    {
        _resolver =
            resolver;

        _orchestrator =
            orchestrator;

        _sessionStore =
            sessionStore;

        _scopeAccess =
            scopeAccess;
    }

    [HttpGet("capabilities")]
    public async Task<IActionResult> GetCapabilities(
        CancellationToken cancellationToken)
    {
        var principal =
            await _resolver.ResolveAsync(
                User,
                cancellationToken);

        if (principal is null)
        {
            return Unauthorized();
        }

        if (!principal.AssistantEnabled)
        {
            return Forbid();
        }

        var capabilities =
            await _orchestrator.GetCapabilitiesAsync(
                principal,
                cancellationToken);

        return Ok(
            capabilities);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage(
        [FromBody] AssistantMessageRequest request,
        CancellationToken cancellationToken)
    {
        var principal =
            await _resolver.ResolveAsync(
                User,
                cancellationToken);

        if (principal is null)
        {
            return Unauthorized();
        }

        if (!principal.AssistantEnabled)
        {
            return Forbid();
        }

        var module =
            request.Module?.Trim();

        if (module is { Length: > 60 })
        {
            module =
                module[..60];
        }

        var scope =
            await _scopeAccess.GetScopeSnapshotAsync(
                principal.OrganizationId,
                principal.UserId,
                cancellationToken);

        var accessibleSiteIds =
            scope.OrganizationWide
                ? Array.Empty<Guid>()
                : (await _scopeAccess.GetAccessibleSiteIdsAsync(
                        principal.OrganizationId,
                        principal.UserId,
                        cancellationToken))
                    .ToArray();

        var toolContext =
            new AssistantToolContext(
                principal,
                scope,
                accessibleSiteIds,
                module,
                HttpContext.Connection
                    .RemoteIpAddress
                    ?.ToString());

        try
        {
            var response =
                await _orchestrator.ExecuteTurnAsync(
                    principal,
                    toolContext,
                    new AssistantTurnRequest(
                        request.ConversationId,
                        request.Message ?? string.Empty,
                        module),
                    cancellationToken);

            return Ok(
                response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    message =
                        exception.Message
                });
        }
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> ListConversations(
        CancellationToken cancellationToken)
    {
        var principal =
            await _resolver.ResolveAsync(
                User,
                cancellationToken);

        if (principal is null)
        {
            return Unauthorized();
        }

        if (!principal.AssistantEnabled)
        {
            return Forbid();
        }

        var conversations =
            _sessionStore.List(
                principal.OrganizationId,
                principal.UserId);

        return Ok(
            new
            {
                conversations
            });
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var principal =
            await _resolver.ResolveAsync(
                User,
                cancellationToken);

        if (principal is null)
        {
            return Unauthorized();
        }

        if (!principal.AssistantEnabled)
        {
            return Forbid();
        }

        var conversation =
            _sessionStore.Get(
                principal.OrganizationId,
                principal.UserId,
                conversationId);

        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(
            conversation);
    }

    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var principal =
            await _resolver.ResolveAsync(
                User,
                cancellationToken);

        if (principal is null)
        {
            return Unauthorized();
        }

        if (!principal.AssistantEnabled)
        {
            return Forbid();
        }

        var deleted =
            _sessionStore.Delete(
                principal.OrganizationId,
                principal.UserId,
                conversationId);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    public sealed record AssistantMessageRequest(
        Guid? ConversationId,
        string? Message,
        string? Module);
}
