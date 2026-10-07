using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Authentication;
using TitanMDM.Application.Interfaces;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Authentication;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth/entra")]
public sealed class EntraLoginController : ControllerBase
{
    private const string FlowCookie =
        "__TitanEntraFlow";

    private const string SessionCookie =
        "__TitanEntraSession";

    private const string SecretProtectorPurpose =
        "TitanMDM.Helpdesk.Entra.ClientSecret.v1";

    private const string LoginFlowProtectorPurpose =
        "TitanMDM.Entra.Login.Flow.v1";

    private const string LoginSessionProtectorPurpose =
        "TitanMDM.Entra.Login.Session.v1";

    private readonly TitanMdmDbContext _db;
    private readonly ITokenService _tokens;
    private readonly JwtOptions _jwt;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly IDataProtector _secretProtector;
    private readonly IDataProtector _flowProtector;
    private readonly IDataProtector _sessionProtector;

    public EntraLoginController(
        TitanMdmDbContext db,
        ITokenService tokens,
        IOptions<JwtOptions> jwt,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db =
            db;

        _tokens =
            tokens;

        _jwt =
            jwt.Value;

        _configuration =
            configuration;

        _httpClientFactory =
            httpClientFactory;

        _secretProtector =
            dataProtectionProvider
                .CreateProtector(
                    SecretProtectorPurpose);

        _flowProtector =
            dataProtectionProvider
                .CreateProtector(
                    LoginFlowProtectorPurpose);

        _sessionProtector =
            dataProtectionProvider
                .CreateProtector(
                    LoginSessionProtectorPurpose);
    }

    // ============================================================
    // START MICROSOFT LOGIN
    // ============================================================

    [HttpGet("start")]
    public async Task<IActionResult> Start(
        CancellationToken cancellationToken)
    {
        EntraIdSettings? settings;

        try
        {
            settings =
                await GetEnabledSettingsAsync(
                    cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(
                StatusCodes.Status409Conflict,
                new
                {
                    message =
                        exception.Message
                });
        }

        if (settings is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "Microsoft Entra ID todavía no está habilitado en TitanMDM."
                });
        }

        if (!Guid.TryParse(
                settings.TenantId,
                out _)
            ||
            !Guid.TryParse(
                settings.ClientId,
                out _)
            ||
            string.IsNullOrWhiteSpace(
                settings.ClientSecretProtected))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "La configuración de Entra ID está incompleta. " +
                        "Revisa Tenant ID, Client ID y Client Secret."
                });
        }

        var backendBase =
            ResolveBackendBaseUrl();

        if (backendBase is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "Application:BackendUrl no contiene una URL válida."
                });
        }

        var callbackUri =
            new Uri(
                backendBase,
                "/api/auth/entra/callback");

        var state =
            RandomToken(
                32);

        var verifier =
            RandomToken(
                64);

        var challenge =
            CreateCodeChallenge(
                verifier);

        var flow =
            new EntraLoginFlow(
                settings.OrganizationId,
                settings.TenantId!,
                settings.ClientId!,
                state,
                verifier,
                DateTime.UtcNow
                    .AddMinutes(
                        5));

        var protectedFlow =
            _flowProtector.Protect(
                JsonSerializer.Serialize(
                    flow));

        Response.Cookies.Append(
            FlowCookie,
            protectedFlow,
            CreateCookieOptions(
                callbackUri.Scheme,
                TimeSpan.FromMinutes(
                    5)));

        var authorizationUrl =
            BuildAuthorizationUrl(
                settings.TenantId!,
                settings.ClientId!,
                callbackUri.ToString(),
                state,
                challenge);

        return Redirect(
            authorizationUrl);
    }

    // ============================================================
    // MICROSOFT CALLBACK
    // ============================================================

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery]
        string? code,

        [FromQuery]
        string? state,

        [FromQuery]
        string? error,

        [FromQuery(Name = "error_description")]
        string? errorDescription,

        CancellationToken cancellationToken)
    {
        var frontendBase =
            ResolveFrontendBaseUrl();

        if (frontendBase is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "Application:FrontendUrl no contiene una URL válida."
                });
        }

        var frontendReturn =
            new Uri(
                frontendBase,
                "/login/entra");

        if (!string.IsNullOrWhiteSpace(
                error))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    errorDescription
                    ??
                    error));
        }

        if (string.IsNullOrWhiteSpace(
                code)
            ||
            string.IsNullOrWhiteSpace(
                state))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "Microsoft no devolvió un código de autorización válido."));
        }

        if (!Request.Cookies
                .TryGetValue(
                    FlowCookie,
                    out var protectedFlow)
            ||
            string.IsNullOrWhiteSpace(
                protectedFlow))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "La sesión temporal de autenticación expiró o la cookie del flujo no regresó al backend."));
        }

        EntraLoginFlow flow;

        try
        {
            var json =
                _flowProtector.Unprotect(
                    protectedFlow);

            flow =
                JsonSerializer
                    .Deserialize<EntraLoginFlow>(
                        json)
                ??
                throw new InvalidOperationException();
        }
        catch
        {
            DeleteCookie(
                FlowCookie);

            return Redirect(
                AddError(
                    frontendReturn,
                    "La sesión temporal de autenticación no es válida."));
        }

        DeleteCookie(
            FlowCookie);

        if (flow.ExpiresAtUtc <
            DateTime.UtcNow)
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "La sesión temporal de autenticación expiró."));
        }

        if (!CryptographicEquals(
                flow.State,
                state))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "El estado devuelto por Microsoft no coincide con la solicitud original."));
        }

        var settings =
            await _db.EntraIdSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            flow.OrganizationId
                        &&
                        x.IsEnabled,
                    cancellationToken);

        if (settings is null)
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "La configuración de Entra ID ya no está habilitada."));
        }

        if (!string.Equals(
                settings.TenantId,
                flow.TenantId,
                StringComparison.OrdinalIgnoreCase)
            ||
            !string.Equals(
                settings.ClientId,
                flow.ClientId,
                StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "La configuración de Entra ID cambió durante el inicio de sesión."));
        }

        if (string.IsNullOrWhiteSpace(
                settings.ClientSecretProtected))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "El Client Secret ya no está configurado."));
        }

        string clientSecret;

        try
        {
            clientSecret =
                _secretProtector.Unprotect(
                    settings.ClientSecretProtected);
        }
        catch
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "El Client Secret guardado no puede descifrarse. " +
                    "Guárdalo nuevamente desde Configuración."));
        }

        var backendBase =
            ResolveBackendBaseUrl();

        if (backendBase is null)
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "Application:BackendUrl no contiene una URL válida."));
        }

        var callbackUri =
            new Uri(
                backendBase,
                "/api/auth/entra/callback");

        var token =
            await ExchangeCodeAsync(
                flow.TenantId,
                flow.ClientId,
                clientSecret,
                code,
                flow.CodeVerifier,
                callbackUri.ToString(),
                cancellationToken);

        if (!token.Success
            ||
            string.IsNullOrWhiteSpace(
                token.AccessToken))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    token.Message));
        }

        var microsoftUser =
            await ReadMicrosoftUserAsync(
                token.AccessToken,
                cancellationToken);

        if (microsoftUser is null
            ||
            string.IsNullOrWhiteSpace(
                microsoftUser.Id))
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "Microsoft autenticó la cuenta, pero TitanMDM no pudo leer la identidad del usuario mediante Microsoft Graph."));
        }

        var directoryUser =
            await _db.EntraDirectoryUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            flow.OrganizationId
                        &&
                        x.EntraObjectId ==
                            microsoftUser.Id
                        &&
                        x.IsActive,
                    cancellationToken);

        if (directoryUser is null)
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "La cuenta Microsoft fue autenticada correctamente, " +
                    "pero todavía no existe en el directorio sincronizado de TitanMDM. " +
                    "Ejecuta una sincronización de Entra ID."));
        }

        if (!directoryUser.LinkedTitanUserId
                .HasValue)
        {
            return Redirect(
                AddError(
                    frontendReturn,
                    "Tu cuenta Microsoft está autenticada, " +
                    "pero todavía no tiene acceso individual asignado en TitanMDM. " +
                    "Vincula la cuenta y asígnale un rol."));
        }

        var temporarySession =
            new EntraLoginSession(
                flow.OrganizationId,
                directoryUser.Id,
                microsoftUser.Id,
                DateTime.UtcNow
                    .AddMinutes(
                        2));

        var protectedSession =
            _sessionProtector.Protect(
                JsonSerializer.Serialize(
                    temporarySession));

        Response.Cookies.Append(
            SessionCookie,
            protectedSession,
            CreateCookieOptions(
                frontendReturn.Scheme,
                TimeSpan.FromMinutes(
                    2)));

        return Redirect(
            frontendReturn.ToString());
    }

    // ============================================================
    // EXCHANGE TEMPORARY ENTRA SESSION FOR TITAN JWT
    // ============================================================

    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange(
        CancellationToken cancellationToken)
    {
        if (!Request.Cookies
                .TryGetValue(
                    SessionCookie,
                    out var protectedSession)
            ||
            string.IsNullOrWhiteSpace(
                protectedSession))
        {
            return Unauthorized(
                new
                {
                    message =
                        "No existe una sesión Microsoft pendiente."
                });
        }

        DeleteCookie(
            SessionCookie);

        EntraLoginSession session;

        try
        {
            var json =
                _sessionProtector.Unprotect(
                    protectedSession);

            session =
                JsonSerializer
                    .Deserialize<EntraLoginSession>(
                        json)
                ??
                throw new InvalidOperationException();
        }
        catch
        {
            return Unauthorized(
                new
                {
                    message =
                        "La sesión Microsoft no es válida."
                });
        }

        if (session.ExpiresAtUtc <
            DateTime.UtcNow)
        {
            return Unauthorized(
                new
                {
                    message =
                        "La sesión Microsoft expiró."
                });
        }

        var account =
            await (
                from directoryUser
                    in _db.EntraDirectoryUsers

                join settings
                    in _db.EntraIdSettings
                    on directoryUser.OrganizationId
                    equals settings.OrganizationId

                join user
                    in _db.Users
                    on directoryUser.LinkedTitanUserId
                    equals user.Id

                where directoryUser.Id ==
                          session.DirectoryUserId
                      &&
                      directoryUser.OrganizationId ==
                          session.OrganizationId
                      &&
                      directoryUser.EntraObjectId ==
                          session.EntraObjectId
                      &&
                      directoryUser.IsActive
                      &&
                      settings.IsEnabled
                      &&
                      user.OrganizationId ==
                          session.OrganizationId
                      &&
                      user.IsActive

                select user
            )
            .SingleOrDefaultAsync(
                cancellationToken);

        if (account is null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "La cuenta Microsoft no tiene acceso activo en TitanMDM."
                });
        }

        var roles =
            await (
                from userRole
                    in _db.UserRoles

                join role
                    in _db.Roles
                    on userRole.RoleId
                    equals role.Id

                where userRole.UserId ==
                          account.Id
                      &&
                      role.OrganizationId ==
                          account.OrganizationId
                      &&
                      role.IsActive

                select role.Name
            )
            .Distinct()
            .ToArrayAsync(
                cancellationToken);

        if (roles.Length ==
            0)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "Esta cuenta no tiene un rol activo en TitanMDM."
                });
        }

        var permissions =
            await (
                from userRole
                    in _db.UserRoles

                join role
                    in _db.Roles
                    on userRole.RoleId
                    equals role.Id

                join rolePermission
                    in _db.RolePermissions
                    on role.Id
                    equals rolePermission.RoleId

                join permission
                    in _db.Permissions
                    on rolePermission.PermissionId
                    equals permission.Id

                where userRole.UserId ==
                          account.Id
                      &&
                      role.OrganizationId ==
                          account.OrganizationId
                      &&
                      role.IsActive
                      &&
                      permission.IsActive

                select permission.Code
            )
            .Distinct()
            .ToArrayAsync(
                cancellationToken);

        var accessExpiresAtUtc =
            DateTime.UtcNow
                .AddMinutes(
                    _jwt.AccessTokenMinutes);

        var refreshExpiresAtUtc =
            DateTime.UtcNow
                .AddDays(
                    _jwt.RefreshTokenDays);

        var accessToken =
            _tokens.GenerateAccessToken(
                account,
                roles,
                permissions,
                accessExpiresAtUtc);

        var refreshToken =
            _tokens.GenerateRefreshToken();

        _db.RefreshTokens.Add(
            new RefreshToken(
                account.Id,
                _tokens.HashRefreshToken(
                    refreshToken),
                refreshExpiresAtUtc,
                HttpContext.Connection
                    .RemoteIpAddress
                    ?.ToString()));

        account.RegisterLogin();

        await _db.SaveChangesAsync(
            cancellationToken);

        return Ok(
            new LoginResponse(
                accessToken,
                accessExpiresAtUtc,
                refreshToken,
                refreshExpiresAtUtc,
                new AuthenticatedUserDto(
                    account.Id,
                    account.OrganizationId,
                    account.FirstName,
                    account.LastName,
                    account.Email,
                    roles,
                    permissions)));
    }

    // ============================================================
    // LOGIN STATUS / DIAGNOSTIC
    // ============================================================

    [HttpGet("status")]
    public async Task<IActionResult> Status(
        CancellationToken cancellationToken)
    {
        EntraIdSettings? settings;

        string? configurationError =
            null;

        try
        {
            settings =
                await GetEnabledSettingsAsync(
                    cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            settings =
                null;

            configurationError =
                exception.Message;
        }

        var frontendBase =
            ResolveFrontendBaseUrl();

        var backendBase =
            ResolveBackendBaseUrl();

        return Ok(
            new
            {
                enabled =
                    settings is not null,

                tenantConfigured =
                    settings is not null
                    &&
                    Guid.TryParse(
                        settings.TenantId,
                        out _),

                clientConfigured =
                    settings is not null
                    &&
                    Guid.TryParse(
                        settings.ClientId,
                        out _),

                secretConfigured =
                    settings is not null
                    &&
                    !string.IsNullOrWhiteSpace(
                        settings.ClientSecretProtected),

                frontendConfigured =
                    frontendBase is not null,

                backendConfigured =
                    backendBase is not null,

                configurationError,

                frontendUrl =
                    frontendBase?.ToString(),

                backendUrl =
                    backendBase?.ToString(),

                callbackUrl =
                    backendBase is null
                        ? null
                        : new Uri(
                            backendBase,
                            "/api/auth/entra/callback")
                            .ToString(),

                frontendReturnUrl =
                    frontendBase is null
                        ? null
                        : new Uri(
                            frontendBase,
                            "/login/entra")
                            .ToString()
            });
    }

    // ============================================================
    // SETTINGS RESOLUTION
    // ============================================================

    private async Task<EntraIdSettings?>
        GetEnabledSettingsAsync(
            CancellationToken cancellationToken)
    {
        var enabled =
            await _db.EntraIdSettings
                .AsNoTracking()
                .Where(
                    x =>
                        x.IsEnabled)
                .Take(
                    2)
                .ToListAsync(
                    cancellationToken);

        if (enabled.Count ==
            0)
        {
            return null;
        }

        /*
         * TitanMDM actualmente opera como una instalación
         * empresarial single-organization.
         *
         * Si luego evoluciona a SaaS/multi-tenant, el tenant
         * deberá resolverse antes del challenge mediante dominio,
         * organización, slug o equivalente.
         */
        if (enabled.Count >
            1)
        {
            throw new InvalidOperationException(
                "Existe más de una configuración Entra habilitada. " +
                "El login corporativo requiere exactamente una organización activa.");
        }

        return enabled[0];
    }

    // ============================================================
    // AUTHORIZATION CODE -> ACCESS TOKEN
    // ============================================================

    private async Task<TokenExchangeResult>
        ExchangeCodeAsync(
            string tenantId,
            string clientId,
            string clientSecret,
            string code,
            string codeVerifier,
            string redirectUri,
            CancellationToken cancellationToken)
    {
        var client =
            _httpClientFactory
                .CreateClient(
                    "entra-id");

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"https://login.microsoftonline.com/" +
                $"{Uri.EscapeDataString(tenantId)}" +
                "/oauth2/v2.0/token")
            {
                Content =
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["client_id"] =
                                clientId,

                            ["client_secret"] =
                                clientSecret,

                            ["grant_type"] =
                                "authorization_code",

                            ["code"] =
                                code,

                            ["redirect_uri"] =
                                redirectUri,

                            ["code_verifier"] =
                                codeVerifier,

                            ["scope"] =
                                "openid profile email User.Read"
                        })
            };

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        var content =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new TokenExchangeResult(
                false,
                null,
                ParseMicrosoftError(
                    content)
                ??
                $"Microsoft Entra rechazó el código de autorización. " +
                $"HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var json =
                JsonDocument.Parse(
                    content);

            if (!json.RootElement
                    .TryGetProperty(
                        "access_token",
                        out var accessToken)
                ||
                string.IsNullOrWhiteSpace(
                    accessToken.GetString()))
            {
                return new TokenExchangeResult(
                    false,
                    null,
                    "Microsoft Entra no devolvió access_token.");
            }

            return new TokenExchangeResult(
                true,
                accessToken.GetString(),
                "OK");
        }
        catch
        {
            return new TokenExchangeResult(
                false,
                null,
                "La respuesta del endpoint de token de Microsoft Entra no pudo interpretarse.");
        }
    }

    // ============================================================
    // MICROSOFT GRAPH /ME
    // ============================================================

    private async Task<MicrosoftUser?>
        ReadMicrosoftUserAsync(
            string accessToken,
            CancellationToken cancellationToken)
    {
        var client =
            _httpClientFactory
                .CreateClient(
                    "entra-id");

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "https://graph.microsoft.com/v1.0/me" +
                "?$select=id,displayName,userPrincipalName,mail");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers
            .TryAddWithoutValidation(
                "client-request-id",
                Guid.NewGuid()
                    .ToString());

        using var response =
            await client.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken);

        try
        {
            using var json =
                JsonDocument.Parse(
                    content);

            var root =
                json.RootElement;

            if (!root.TryGetProperty(
                    "id",
                    out var idValue))
            {
                return null;
            }

            var id =
                idValue.GetString();

            if (string.IsNullOrWhiteSpace(
                    id))
            {
                return null;
            }

            return new MicrosoftUser(
                id,

                root.TryGetProperty(
                    "displayName",
                    out var displayName)
                    ? displayName.GetString()
                    : null,

                root.TryGetProperty(
                    "userPrincipalName",
                    out var upn)
                    ? upn.GetString()
                    : null,

                root.TryGetProperty(
                    "mail",
                    out var mail)
                    ? mail.GetString()
                    : null);
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // APPLICATION URLS
    // ============================================================

    private Uri? ResolveFrontendBaseUrl()
    {
        return ResolveApplicationUrl(
            "Application:FrontendUrl");
    }

    private Uri? ResolveBackendBaseUrl()
    {
        return ResolveApplicationUrl(
            "Application:BackendUrl");
    }

    private Uri? ResolveApplicationUrl(
        string configurationKey)
    {
        var value =
            _configuration[
                configurationKey];

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri))
        {
            return null;
        }

        if (!string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase)
            &&
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new Uri(
            uri.GetLeftPart(
                UriPartial.Authority));
    }

    // ============================================================
    // MICROSOFT AUTHORIZATION URL
    // ============================================================

    private static string
        BuildAuthorizationUrl(
            string tenantId,
            string clientId,
            string redirectUri,
            string state,
            string challenge)
    {
        var parameters =
            new Dictionary<string, string>
            {
                ["client_id"] =
                    clientId,

                ["response_type"] =
                    "code",

                ["redirect_uri"] =
                    redirectUri,

                ["response_mode"] =
                    "query",

                ["scope"] =
                    "openid profile email User.Read",

                ["state"] =
                    state,

                ["code_challenge"] =
                    challenge,

                ["code_challenge_method"] =
                    "S256",

                ["prompt"] =
                    "select_account"
            };

        var query =
            string.Join(
                "&",
                parameters.Select(
                    pair =>
                        $"{Uri.EscapeDataString(pair.Key)}=" +
                        $"{Uri.EscapeDataString(pair.Value)}"));

        return
            $"https://login.microsoftonline.com/" +
            $"{Uri.EscapeDataString(tenantId)}" +
            $"/oauth2/v2.0/authorize?{query}";
    }

    // ============================================================
    // FRONTEND ERROR REDIRECT
    // ============================================================

    private static string AddError(
        Uri frontendReturn,
        string message)
    {
        var separator =
            string.IsNullOrWhiteSpace(
                frontendReturn.Query)
                ? "?"
                : "&";

        return
            frontendReturn +
            separator +
            "error=" +
            Uri.EscapeDataString(
                message);
    }

    // ============================================================
    // TEMPORARY COOKIE SECURITY
    // ============================================================

    private static CookieOptions
        CreateCookieOptions(
            string scheme,
            TimeSpan lifetime)
    {
        return new CookieOptions
        {
            HttpOnly =
                true,

            Secure =
                string.Equals(
                    scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase),

            SameSite =
                SameSiteMode.Lax,

            Path =
                "/api/auth/entra",

            MaxAge =
                lifetime,

            IsEssential =
                true
        };
    }

    private void DeleteCookie(
        string name)
    {
        Response.Cookies.Delete(
            name,
            new CookieOptions
            {
                Path =
                    "/api/auth/entra"
            });
    }

    // ============================================================
    // PKCE / STATE HELPERS
    // ============================================================

    private static string RandomToken(
        int bytes)
    {
        return Base64UrlEncode(
            RandomNumberGenerator
                .GetBytes(
                    bytes));
    }

    private static string
        CreateCodeChallenge(
            string verifier)
    {
        return Base64UrlEncode(
            SHA256.HashData(
                Encoding.ASCII
                    .GetBytes(
                        verifier)));
    }

    private static string Base64UrlEncode(
        byte[] value)
    {
        return Convert
            .ToBase64String(
                value)
            .TrimEnd(
                '=')
            .Replace(
                '+',
                '-')
            .Replace(
                '/',
                '_');
    }

    private static bool CryptographicEquals(
        string left,
        string right)
    {
        var leftBytes =
            Encoding.UTF8
                .GetBytes(
                    left);

        var rightBytes =
            Encoding.UTF8
                .GetBytes(
                    right);

        return leftBytes.Length ==
               rightBytes.Length
               &&
               CryptographicOperations
                   .FixedTimeEquals(
                       leftBytes,
                       rightBytes);
    }

    // ============================================================
    // MICROSOFT ERROR PARSER
    // ============================================================

    private static string?
        ParseMicrosoftError(
            string content)
    {
        if (string.IsNullOrWhiteSpace(
                content))
        {
            return null;
        }

        try
        {
            using var json =
                JsonDocument.Parse(
                    content);

            var root =
                json.RootElement;

            var code =
                root.TryGetProperty(
                    "error",
                    out var error)
                    ? error.GetString()
                    : null;

            var description =
                root.TryGetProperty(
                    "error_description",
                    out var detail)
                    ? detail.GetString()
                    : null;

            if (!string.IsNullOrWhiteSpace(
                    description))
            {
                return string.IsNullOrWhiteSpace(
                    code)
                    ? description
                    : $"{code}: {description}";
            }

            return code;
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // INTERNAL MODELS
    // ============================================================

    private sealed record EntraLoginFlow(
        Guid OrganizationId,
        string TenantId,
        string ClientId,
        string State,
        string CodeVerifier,
        DateTime ExpiresAtUtc);

    private sealed record EntraLoginSession(
        Guid OrganizationId,
        Guid DirectoryUserId,
        string EntraObjectId,
        DateTime ExpiresAtUtc);

    private sealed record TokenExchangeResult(
        bool Success,
        string? AccessToken,
        string Message);

    private sealed record MicrosoftUser(
        string Id,
        string? DisplayName,
        string? UserPrincipalName,
        string? Mail);
}