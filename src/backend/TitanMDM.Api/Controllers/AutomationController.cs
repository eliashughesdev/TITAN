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
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> Rules(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _service.GetRulesAsync(
                organizationId.Value,
                cancellationToken));
    }

    [HttpGet("rules/{ruleId:guid}")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> Rule(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var rule =
            await _service.GetRuleAsync(
                organizationId.Value,
                ruleId,
                cancellationToken);

        return rule is null
            ? NotFound()
            : Ok(rule);
    }

    [HttpPost("rules")]
    [RequirePermission(AutomationPermissions.Manage)]
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

        return Ok(
            await _service.CreateAsync(
                organizationId.Value,
                userId.Value,
                request,
                cancellationToken));
    }

    [HttpPut("rules/{ruleId:guid}")]
    [RequirePermission(AutomationPermissions.Manage)]
    public async Task<IActionResult> Update(
        Guid ruleId,
        UpdateAutomationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _service.UpdateAsync(
                organizationId.Value,
                ruleId,
                request,
                cancellationToken));
    }

    [HttpPost("rules/{ruleId:guid}/enable")]
    [RequirePermission(AutomationPermissions.Manage)]
    public async Task<IActionResult> Enable(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        await _service.SetEnabledAsync(
            organizationId.Value,
            ruleId,
            true,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("rules/{ruleId:guid}/disable")]
    [RequirePermission(AutomationPermissions.Manage)]
    public async Task<IActionResult> Disable(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        await _service.SetEnabledAsync(
            organizationId.Value,
            ruleId,
            false,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("rules/{ruleId:guid}/execute")]
    [RequirePermission(AutomationPermissions.Execute)]
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

        await _service.ExecuteRuleAsync(
            organizationId.Value,
            userId.Value,
            ruleId,
            request,
            cancellationToken);

        return Accepted();
    }

    [HttpDelete("rules/{ruleId:guid}")]
    [RequirePermission(AutomationPermissions.Manage)]
    public async Task<IActionResult> Delete(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        await _service.DeleteAsync(
            organizationId.Value,
            ruleId,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("executions")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> Executions(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _service.GetExecutionsAsync(
                organizationId.Value,
                limit,
                cancellationToken));
    }

    // ============================================================
    // AUT-1 — Plataforma empresarial
    // ============================================================

    [HttpGet("platform/events")]
    [RequirePermission(AutomationPermissions.View)]
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
    [RequirePermission(AutomationPermissions.View)]
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

    [HttpPost("platform/definitions/validate")]
    [RequirePermission(AutomationPermissions.Manage)]
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
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> EnterpriseRules(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetEnterpriseRulesAsync(
                organizationId.Value,
                cancellationToken));
    }

    [HttpPost("platform/rules")]
    [RequirePermission(AutomationPermissions.Manage)]
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

        return Ok(
            await platform.CreateEnterpriseRuleAsync(
                organizationId.Value,
                userId.Value,
                request,
                cancellationToken));
    }

    [HttpPut("platform/rules/{ruleId:guid}")]
    [RequirePermission(AutomationPermissions.Manage)]
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

        return Ok(
            await platform.UpdateEnterpriseRuleAsync(
                organizationId.Value,
                ruleId,
                userId.Value,
                request,
                cancellationToken));
    }

    [HttpPost("platform/rules/{ruleId:guid}/simulate")]
    [RequirePermission(AutomationPermissions.Simulate)]
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

        return Ok(
            await platform.SimulateAsync(
                organizationId.Value,
                userId.Value,
                ruleId,
                request,
                cancellationToken));
    }

    [HttpGet("platform/dashboard")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> Dashboard(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetDashboardAsync(
                organizationId.Value,
                cancellationToken));
    }

    [HttpGet("platform/queue")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> QueueStats(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetQueueStatsAsync(
                organizationId.Value,
                cancellationToken));
    }

    [HttpGet("platform/executions/{executionId:guid}")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> ExecutionDetail(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        var detail =
            await platform.GetExecutionDetailAsync(
                organizationId.Value,
                executionId,
                cancellationToken);

        return detail is null
            ? NotFound()
            : Ok(detail);
    }

    [HttpPost(
        "platform/executions/{executionId:guid}/retry")]
    [RequirePermission(AutomationPermissions.Execute)]
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

    [HttpGet("platform/notifications")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> Notifications(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        return Ok(
            await platform.GetNotificationsAsync(
                organizationId.Value,
                limit,
                cancellationToken));
    }

    [HttpPost(
        "platform/notifications/{notificationId:guid}/read")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> MarkNotificationRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        var result =
            await platform.MarkNotificationReadAsync(
                organizationId.Value,
                notificationId,
                cancellationToken);

        return result
            ? NoContent()
            : NotFound();
    }

    [HttpGet("platform/rules/{ruleId:guid}/schedule")]
    [RequirePermission(AutomationPermissions.View)]
    public async Task<IActionResult> GetSchedule(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        var schedule =
            await platform.GetScheduleAsync(
                organizationId.Value,
                ruleId,
                cancellationToken);

        return schedule is null
            ? NotFound()
            : Ok(schedule);
    }

    [HttpPut("platform/rules/{ruleId:guid}/schedule")]
    [RequirePermission(AutomationPermissions.Manage)]
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

        return Ok(
            await platform.SaveScheduleAsync(
                organizationId.Value,
                ruleId,
                userId.Value,
                request,
                cancellationToken));
    }

    [HttpDelete("platform/rules/{ruleId:guid}/schedule")]
    [RequirePermission(AutomationPermissions.Manage)]
    public async Task<IActionResult> DeleteSchedule(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var platform = GetPlatform();

        if (platform is null)
            return PlatformUnavailable();

        await platform.DeleteScheduleAsync(
            organizationId.Value,
            ruleId,
            cancellationToken);

        return NoContent();
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
