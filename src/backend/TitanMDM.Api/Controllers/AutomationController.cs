using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Automation;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/automation")]
public sealed class AutomationController
    : ControllerBase
{
    private readonly IAutomationService _service;

    public AutomationController(
        IAutomationService service)
    {
        _service = service;
    }

    [HttpGet("rules")]
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

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

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