using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        // Usar el host del frontend validado por el proxy HTTPS.
        // La solicitud debe llegar mediante el mismo origen de la consola.
        var originHeader = Request.Headers.Origin.ToString();

        if (!Uri.TryCreate(originHeader, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(origin.UserInfo) ||
            origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment))
        {
            return BadRequest(new
            {
                message = "Managed Google Play requiere un origen frontend HTTPS."
            });
        }

        // Comparar con la lista de orÃ­genes del frontend aprobados.
        // No confiar Ãºnicamente en un encabezado Origin proporcionado
        // por el cliente: debe coincidir con configuraciÃ³n del servidor.
        var allowed = HttpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            .GetSection("AndroidManagement:ManagedPlayAllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (!allowed.Any(value =>
            string.Equals(
                value.TrimEnd('/'),
                origin.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase)))
        {
            return Forbid();
        }

        var result = await _service.CreateManagedPlayWebTokenAsync(
            organizationId.Value,
            userId.Value,
            new CreateAndroidWebTokenRequest(
                origin.GetLeftPart(UriPartial.Authority)),
            cancellationToken);

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        return Ok(result);
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
    public IActionResult ListLauncherAssets()
    {
        var organizationId = GetOrganizationId();
        return organizationId is null
            ? Unauthorized()
            : Ok(_assets.List(organizationId.Value).Select(asset => new
            {
                id = asset.Id,
                kind = asset.Kind,
                format = asset.Format,
                size = asset.Size,
                sha256 = asset.Sha256,
                token = "titan-asset:" + asset.Id.ToString("D"),
                createdAtUtc = asset.CreatedAtUtc
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