using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TitanMDM.Application.Authentication;
using TitanMDM.Application.Interfaces;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController
    : ControllerBase
{
    private readonly IAuthenticationService
        _authenticationService;

    public AuthController(
        IAuthenticationService authenticationService)
    {
        _authenticationService =
            authenticationService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                request.Email) ||
            string.IsNullOrWhiteSpace(
                request.Password))
        {
            return BadRequest(
                new
                {
                    message =
                        "Email and password are required."
                });
        }

        var result =
            await _authenticationService.LoginAsync(
                request,
                GetIpAddress(),
                cancellationToken);

        if (result is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "Invalid email or password."
                });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await _authenticationService.RefreshAsync(
                request.RefreshToken,
                GetIpAddress(),
                cancellationToken);

        if (result is null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "Invalid or expired refresh token."
                });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        await _authenticationService.LogoutAsync(
            request.RefreshToken,
            GetIpAddress(),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(
        CancellationToken cancellationToken)
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            return Unauthorized();
        }

        var result =
            await _authenticationService
                .GetCurrentUserAsync(
                    userId,
                    cancellationToken);

        if (result is null)
            return Unauthorized();

        return Ok(result);
    }

    private string? GetIpAddress()
    {
        return HttpContext
            .Connection
            .RemoteIpAddress?
            .ToString();
    }
}

public sealed record RefreshRequest(
    string RefreshToken);