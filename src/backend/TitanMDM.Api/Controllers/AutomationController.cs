using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TitanMDM.Api.Security;
using TitanMDM.Application.Automation;

namespace TitanMDM.Api.Controllers;

/*
 * AUT-1.8 — API del motor de automatizaciones.
 *
 * Los endpoints legacy (rules/executions) se mantienen tal
 * cual. Los endpoints de plataforma (platform/*) exponen el
 * motor empresarial y se resuelven de forma tolerante: si
 * AddTitanAutomationPlatform no está registrado todavía, se
 * responde 503 en lugar de romper el arranque.
 */
[ApiController]
[Authorize]
[Route("api/automation")]
[ServiceFilter(typeof(RbacAuditFilter))]
public sealed class AutomationController
    : ControllerBase
{
    private readonly IAutomationService _service;
    private readonly IServiceProvider _services;

    public AutomationController(
        IAutomationService service,
        IServiceProvider services)
    {
        _service = service;
        _services = services;
    }

    [HttpGet("rules")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Rules(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        return Ok(
            await _service.GetRulesAsync(
                organizationId.Value,
                userId.Value,
                cancellationToken));
    }

    [HttpGet("rules/{ruleId:guid}")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Rule(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var rule =
            await _service.GetRuleAsync(
                organizationId.Value,
                userId.Value,
                ruleId,
                cancellationToken);

        return rule is null
            ? NotFound()
            : Ok(rule);
    }

    [HttpPost("rules")]
    [RequireAnyPermission(
        AutomationPermissions.Create,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Create(
        CreateAutomationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        return await ExecuteAuthorizedAsync(
            () =>
                _service.CreateAsync(
                    organizationId.Value,
                    userId.Value,
                    request,
                    cancellationToken));
    }

    [HttpPut("rules/{ruleId:guid}")]
    [RequireAnyPermission(
        AutomationPermissions.Edit,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Update(
        Guid ruleId,
        UpdateAutomationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        return await ExecuteAuthorizedAsync(
            () =>
                _service.UpdateAsync(
                    organizationId.Value,
                    userId.Value,
                    ruleId,
                    request,
                    cancellationToken));
    }

    [HttpPost("rules/{ruleId:guid}/enable")]
    [RequireAnyPermission(
        AutomationPermissions.Edit,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Enable(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        return await ExecuteAuthorizedAsync(
            async () =>
            {
                await _service.SetEnabledAsync(
                    organizationId.Value,
                    userId.Value,
                    ruleId,
                    true,
                    cancellationToken);

                return true;
            },
            NoContent);
    }

    [HttpPost("rules/{ruleId:guid}/disable")]
    [RequireAnyPermission(
        AutomationPermissions.Edit,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Disable(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        return await ExecuteAuthorizedAsync(
            async () =>
            {
                await _service.SetEnabledAsync(
                    organizationId.Value,
                    userId.Value,
                    ruleId,
                    false,
                    cancellationToken);

                return true;
            },
            NoContent);
    }

    [HttpPost("rules/{ruleId:guid}/execute")]
    [RequireAnyPermission(
        AutomationPermissions.Execute,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Execute(
        Guid ruleId,
        ExecuteAutomationRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        try
        {
            await _service.ExecuteRuleAsync(
                organizationId.Value,
                userId.Value,
                ruleId,
                request,
                cancellationToken);

            return Accepted();
        }
        catch (AutomationAuthorizationException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (AutomationPlatformUnavailableException)
        {
            return PlatformUnavailable();
        }
    }

    [HttpDelete("rules/{ruleId:guid}")]
    [RequireAnyPermission(
        AutomationPermissions.Admin,
        AutomationPermissions.Manage)]
    public async Task<IActionResult> Delete(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        return await ExecuteAuthorizedAsync(
            async () =>
            {
                await _service.DeleteAsync(
                    organizationId.Value,
                    userId.Value,
                    ruleId,
                    cancellationToken);

                return true;
            },
            NoContent);
    }

    [HttpGet("executions")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Executions(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        return Ok(
            await _service.GetExecutionsAsync(
                organizationId.Value,
                userId.Value,
                limit,
                cancellationToken));
    }

    // ============================================================
    // AUT-1 — Plataforma empresarial
    // ============================================================

    [HttpGet("platform/events")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Events(
        CancellationToken cancellationToken)
    {
        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetEventCatalogAsync(
                cancellationToken));
    }

    [HttpGet("platform/actions")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Actions(
        CancellationToken cancellationToken)
    {
        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetActionCatalogAsync(
                cancellationToken));
    }

    [HttpGet("platform/integrations")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Integrations(
        CancellationToken cancellationToken)
    {
        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetIntegrationsAsync(
                cancellationToken));
    }

    [HttpGet("platform/tool-contracts")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> ToolContracts(
        CancellationToken cancellationToken)
    {
        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetToolContractsAsync(
                cancellationToken));
    }

    [HttpPost("platform/definitions/validate")]
    [RequireAnyPermission(
        AutomationPermissions.Simulate,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> ValidateDefinition(
        AutomationRuleDefinition definition,
        CancellationToken cancellationToken)
    {
        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.ValidateDefinitionAsync(
                definition,
                cancellationToken));
    }

    [HttpGet("platform/rules")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> EnterpriseRules(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetEnterpriseRulesAsync(
                organizationId.Value,
                userId.Value,
                cancellationToken));
    }

    [HttpPost("platform/rules")]
    [RequireAnyPermission(
        AutomationPermissions.Create,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> CreateEnterpriseRule(
        EnterpriseCreateRuleRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return await ExecuteAuthorizedAsync(
            () =>
                platform.CreateEnterpriseRuleAsync(
                    organizationId.Value,
                    userId.Value,
                    request,
                    cancellationToken));
    }

    [HttpPut("platform/rules/{ruleId:guid}")]
    [RequireAnyPermission(
        AutomationPermissions.Edit,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> UpdateEnterpriseRule(
        Guid ruleId,
        EnterpriseUpdateRuleRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return await ExecuteAuthorizedAsync(
            () =>
                platform.UpdateEnterpriseRuleAsync(
                    organizationId.Value,
                    ruleId,
                    userId.Value,
                    request,
                    cancellationToken));
    }

    [HttpPost("platform/rules/{ruleId:guid}/simulate")]
    [RequireAnyPermission(
        AutomationPermissions.Simulate,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Simulate(
        Guid ruleId,
        SimulateRuleRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return await ExecuteAuthorizedAsync(
            () =>
                platform.SimulateAsync(
                    organizationId.Value,
                    userId.Value,
                    ruleId,
                    request,
                    cancellationToken));
    }

    [HttpGet("platform/dashboard")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Dashboard(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetDashboardAsync(
                organizationId.Value,
                userId.Value,
                cancellationToken));
    }

    [HttpGet("platform/queue")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> QueueStats(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetQueueStatsAsync(
                organizationId.Value,
                userId.Value,
                cancellationToken));
    }

    [HttpGet("platform/executions/{executionId:guid}")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> ExecutionDetail(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        var detail =
            await platform.GetExecutionDetailAsync(
                organizationId.Value,
                userId.Value,
                executionId,
                cancellationToken);

        return detail is null
            ? NotFound()
            : Ok(detail);
    }

    [HttpPost(
        "platform/executions/{executionId:guid}/retry")]
    [RequireAnyPermission(
        AutomationPermissions.Execute,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> RetryExecution(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        try
        {
            var result =
                await platform.RetryExecutionAsync(
                    organizationId.Value,
                    userId.Value,
                    executionId,
                    cancellationToken);

            return result
                ? Accepted()
                : NotFound();
        }
        catch (AutomationAuthorizationException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("platform/notifications")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> Notifications(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetNotificationsAsync(
                organizationId.Value,
                userId.Value,
                limit,
                cancellationToken));
    }

    [HttpPost(
        "platform/notifications/{notificationId:guid}/read")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> MarkNotificationRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        var result =
            await platform.MarkNotificationReadAsync(
                organizationId.Value,
                userId.Value,
                notificationId,
                cancellationToken);

        return result
            ? NoContent()
            : NotFound();
    }

    [HttpGet("platform/rules/{ruleId:guid}/schedule")]
    [RequireAnyPermission(
        AutomationPermissions.View,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> GetSchedule(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        var schedule =
            await platform.GetScheduleAsync(
                organizationId.Value,
                userId.Value,
                ruleId,
                cancellationToken);

        return schedule is null
            ? NotFound()
            : Ok(schedule);
    }

    [HttpPut("platform/rules/{ruleId:guid}/schedule")]
    [RequireAnyPermission(
        AutomationPermissions.Edit,
        AutomationPermissions.Manage,
        AutomationPermissions.Admin)]
    public async Task<IActionResult> SaveSchedule(
        Guid ruleId,
        AutomationScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return await ExecuteAuthorizedAsync(
            () =>
                platform.SaveScheduleAsync(
                    organizationId.Value,
                    ruleId,
                    userId.Value,
                    request,
                    cancellationToken));
    }

    [HttpDelete("platform/rules/{ruleId:guid}/schedule")]
    [RequireAnyPermission(
        AutomationPermissions.Admin,
        AutomationPermissions.Manage)]
    public async Task<IActionResult> DeleteSchedule(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        var userId =
            GetUserId();

        if (organizationId is null ||
            userId is null)
        {
            return Unauthorized();
        }

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        try
        {
            await platform.DeleteScheduleAsync(
                organizationId.Value,
                userId.Value,
                ruleId,
                cancellationToken);

            return NoContent();
        }
        catch (AutomationAuthorizationException ex)
        {
            return Forbidden(ex.Message);
        }
    }

    private IAutomationPlatformService? GetPlatform()
    {
        return _services.GetService<
            IAutomationPlatformService>();
    }

    private ObjectResult PlatformUnavailable()
    {
        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new
            {
                error =
                    "The enterprise automation platform is not registered. Call AddTitanAutomationPlatform during startup."
            });
    }

    /*
     * AUT-1.8 — Traduce una denegación de autorización del
     * motor (permiso insuficiente o alcance de localidad) a
     * HTTP 403 en lugar de un error 500 genérico.
     */
    private async Task<IActionResult>
        ExecuteAuthorizedAsync<T>(
            Func<Task<T>> action)
    {
        try
        {
            var result = await action();

            return Ok(result);
        }
        catch (AutomationAuthorizationException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<IActionResult>
        ExecuteAuthorizedAsync<T>(
            Func<Task<T>> action,
            Func<IActionResult> onSuccess)
    {
        try
        {
            await action();

            return onSuccess();
        }
        catch (AutomationAuthorizationException ex)
        {
            return Forbidden(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private ObjectResult Forbidden(string message)
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            new { error = message });
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id")
            ?? User.FindFirstValue(
                "organizationId");

        return Guid.TryParse(
            value,
            out var result)
            ? result
            : null;
    }

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(
            value,
            out var result)
            ? result
            : null;
    }
}
