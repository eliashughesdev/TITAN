using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TitanMDM.Api.Security;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Security;
using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Android.Policies;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(RbacAuditFilter))]
[Route("api/android-enterprise")]
public sealed class AndroidEnterpriseController
    : ControllerBase
{
    private readonly IAndroidEnterpriseService _service;
    private readonly AndroidManagementOptions _options;
    private readonly LauncherAssetStore _assets;

    public AndroidEnterpriseController(
        IAndroidEnterpriseService service,
        IOptions<AndroidManagementOptions> options,
        LauncherAssetStore assets)
    {
        _service = service;
        _options = options.Value;
        _assets = assets;
    }

    [HttpGet("status")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetStatus(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var result =
            await _service.GetStatusAsync(
                organizationId.Value,
                cancellationToken);

        return Ok(result);
    }

    [HttpGet("audit")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetAudit(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var result =
            await _service.GetAuditPageAsync(
                organizationId.Value,
                search,
                page,
                pageSize,
                cancellationToken);

        return Ok(result);
    }

    [HttpPost("signup")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> CreateSignup(
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
            var result =
                await _service.CreateSignupUrlAsync(
                    organizationId.Value,
                    userId.Value,
                    cancellationToken);

            return Ok(result);
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state,
        [FromQuery] string? enterpriseToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state) ||
            string.IsNullOrWhiteSpace(
                enterpriseToken))
        {
            return Redirect(
                BuildFrontendRedirect(
                    _options.FrontendErrorUrl,
                    "invalid_callback"));
        }

        try
        {
            await _service.CompleteSignupAsync(
                state,
                enterpriseToken,
                cancellationToken);

            return Redirect(
                _options.FrontendSuccessUrl);
        }
        catch
        {
            return Redirect(
                BuildFrontendRedirect(
                    _options.FrontendErrorUrl,
                    "signup_failed"));
        }
    }

    [HttpGet("enrollments")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetEnrollments(
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        return Ok(
            await _service.GetEnrollmentsAsync(
                organizationId.Value,
                cancellationToken));
    }

    [HttpPost("enrollments")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> CreateEnrollment(
        [FromBody]
        CreateAndroidEnrollmentRequest request,
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
            var result =
                await _service.CreateEnrollmentAsync(
                    organizationId.Value,
                    userId.Value,
                    request,
                    cancellationToken);

            return Created(
                $"/api/android-enterprise/enrollments/{result.Id}",
                result);
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpDelete("enrollments/{id:guid}")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> RevokeEnrollment(
        Guid id,
        CancellationToken cancellationToken)
    {
        var organizationId =
            GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        try
        {
            var revoked =
                await _service.RevokeEnrollmentAsync(
                    organizationId.Value,
                    id,
                    cancellationToken);

            return revoked
                ? NoContent()
                : NotFound();
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpPost("managed-play/web-token")]
    [RequireAnyPermission(PermissionCodes.AndroidEnterprise.Manage)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CreateManagedPlayToken(
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null || userId is null)
            return Unauthorized();

        // No aceptar origen arbitrario desde el JSON del cliente.
        // Usar el encabezado Origin del navegador y validarlo contra la
        // lista de origenes del frontend aprobados por el servidor.
        var originHeader = Request.Headers.Origin.ToString();

        if (!TryResolveOrigin(originHeader, out var originAuthority))
        {
            return BadRequest(new
            {
                message = "Managed Google Play requiere un origen frontend valido."
            });
        }

        // Comparar con la lista de origenes del frontend aprobados.
        // No confiar unicamente en un encabezado Origin proporcionado
        // por el cliente: debe coincidir con configuracion del servidor.
        var allowed = ResolveManagedPlayAllowedOrigins();

        if (!allowed.Any(value =>
            string.Equals(
                value.TrimEnd('/'),
                originAuthority,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Forbid();
        }

        var result = await _service.CreateManagedPlayWebTokenAsync(
            organizationId.Value,
            userId.Value,
            new CreateAndroidWebTokenRequest(originAuthority),
            cancellationToken);

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        return Ok(result);
    }

    [HttpGet("managed-play/applications")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> GetManagedPlayApplications(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var result = await _service.GetManagedPlayApplicationsAsync(
            organizationId.Value,
            search,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("managed-play/applications")]
    [RequireAnyPermission(PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> RegisterManagedPlayApplication(
        [FromBody] RegisterAndroidManagedPlayApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        var userId = GetUserId();

        if (organizationId is null || userId is null)
            return Unauthorized();

        try
        {
            var result = await _service.RegisterManagedPlayApplicationAsync(
                organizationId.Value,
                userId.Value,
                request ?? throw new ArgumentNullException(nameof(request)),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("managed-play/applications/{applicationId:guid}")]
    [RequireAnyPermission(PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> RemoveManagedPlayApplication(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();

        if (organizationId is null)
            return Unauthorized();

        var removed = await _service.RemoveManagedPlayApplicationAsync(
            organizationId.Value,
            applicationId,
            cancellationToken);

        return removed ? NoContent() : NotFound();
    }

    [HttpPost("launcher-assets")]
    [RequireAnyPermission(PermissionCodes.AndroidEnterprise.Manage)]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> UploadLauncherAsset(
        [FromForm] string kind,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (organizationId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Adjunta una imagen PNG, JPEG o WebP." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var asset = await _assets.SaveAsync(
                organizationId.Value,
                kind,
                stream,
                cancellationToken);
            return Ok(new
            {
                id = asset.Id,
                kind = asset.Kind,
                format = asset.Format,
                size = asset.Size,
                sha256 = asset.Sha256,
                token = "titan-asset:" + asset.Id.ToString("D"),
                createdAtUtc = asset.CreatedAtUtc
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("launcher-assets")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public async Task<IActionResult> ListLauncherAssets(
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (organizationId is null)
        {
            return Unauthorized();
        }

        var assets = await _assets.ListWithUsageAsync(
            organizationId.Value,
            cancellationToken);

        return Ok(assets.Select(usage => new
        {
            id = usage.Info.Id,
            kind = usage.Info.Kind,
            format = usage.Info.Format,
            size = usage.Info.Size,
            sha256 = usage.Info.Sha256,
            isReferenced = usage.IsReferenced,
            isPublished = usage.IsPublished,
            token = "titan-asset:" + usage.Info.Id.ToString("D"),
            createdAtUtc = usage.Info.CreatedAtUtc
        }));
    }

    [HttpGet("launcher-assets/{assetId:guid}")]
    [RequireAnyPermission(
        PermissionCodes.AndroidEnterprise.View,
        PermissionCodes.Workspaces.AndroidView)]
    public IActionResult DownloadLauncherAsset(Guid assetId)
    {
        var organizationId = GetOrganizationId();
        if (organizationId is null)
        {
            return Unauthorized();
        }

        var content = _assets.ReadBytes(organizationId.Value, assetId);
        if (content is null)
        {
            return NotFound();
        }

        return File(content.Value.Bytes, ContentType(content.Value.Info.Format));
    }

    [HttpDelete("launcher-assets/{assetId:guid}")]
    [RequireAnyPermission(PermissionCodes.AndroidEnterprise.Manage)]
    public IActionResult DeleteLauncherAsset(Guid assetId)
    {
        var organizationId = GetOrganizationId();
        if (organizationId is null)
        {
            return Unauthorized();
        }

        return _assets.Delete(organizationId.Value, assetId)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("launcher-assets/cleanup")]
    [RequireAnyPermission(PermissionCodes.AndroidEnterprise.Manage)]
    public async Task<IActionResult> CleanupLauncherAssets(
        CancellationToken cancellationToken)
    {
        var organizationId = GetOrganizationId();
        if (organizationId is null)
        {
            return Unauthorized();
        }

        var removed = await _assets.CleanupAsync(organizationId.Value, cancellationToken);
        return Ok(new { removed });
    }

    private static string ContentType(string format) => format switch
    {
        "png" => "image/png",
        "webp" => "image/webp",
        _ => "image/jpeg"
    };

    private string[] ResolveManagedPlayAllowedOrigins()
    {
        var configuration = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>();

        var configured = configuration
            .GetSection("AndroidManagement:ManagedPlayAllowedOrigins")
            .Get<string[]>();

        if (configured is { Length: > 0 })
        {
            return configured;
        }

        // Fallback: reutilizar los origenes de CORS del frontend.
        return configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();
    }

    private static bool TryResolveOrigin(
        string? raw,
        out string authority)
    {
        authority = string.Empty;

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var origin) ||
            (origin.Scheme != Uri.UriSchemeHttps &&
             origin.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(origin.UserInfo) ||
            origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment))
        {
            return false;
        }

        authority = origin.GetLeftPart(UriPartial.Authority);
        return authority.Length > 0;
    }

    private Guid? GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var id)
            ? id
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
            out var id)
            ? id
            : null;
    }

    private static string BuildFrontendRedirect(
        string url,
        string error)
    {
        var separator =
            url.Contains(
                '?',
                StringComparison.Ordinal)
                ? "&"
                : "?";

        return
            $"{url}{separator}error=" +
            Uri.EscapeDataString(error);
    }
}