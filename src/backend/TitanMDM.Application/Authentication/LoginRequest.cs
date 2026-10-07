namespace TitanMDM.Application.Authentication;

public sealed record LoginRequest(
    string Email,
    string Password);