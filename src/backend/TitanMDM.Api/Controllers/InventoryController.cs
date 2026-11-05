using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using QRCoder;
using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Api.Inventory;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private const long MaxBodyBytes =
        8L * 1024L * 1024L;

    private static readonly HashSet<string> ModuleRoots =
        new(
            StringComparer.OrdinalIgnoreCase)
        {
            "assets",
            "deliveries",
            "returns",
            "receptions",
            "employees",
            "sessions",
            "counts",
            "history",
            "module"
        };

    private readonly IInventoryGateway
        _gateway;

    private readonly IInventoryService
        _inventoryService;

    private readonly ILogger<InventoryController>
        _logger;

    public InventoryController(
        IInventoryGateway gateway,
        IInventoryService inventoryService,
        ILogger<InventoryController> logger)
    {
        _gateway =
            gateway
            ??
            throw new ArgumentNullException(
                nameof(gateway));

        _inventoryService =
            inventoryService
            ??
            throw new ArgumentNullException(
                nameof(inventoryService));

        _logger =
            logger
            ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    // ============================================================
    // HEALTH
    // ============================================================

    [HttpGet("health")]
    public Task<IActionResult> Health(
        CancellationToken cancellationToken)
    {
        /*
         * Health no se cachea.
         * Debe representar el estado real del Edge Service.
         */
        return ForwardAsync(
            route:
                "/api/internal/titan/health",

            method:
                HttpMethod.Get,

            hasBody:
                false,

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    // ============================================================
    // ASSETS
    // ============================================================

    [HttpGet("assets")]
    public Task<IActionResult> Assets(
        [FromQuery] int limit = 100,

        [FromQuery] string search = "",

        [FromQuery] string status = "",

        [FromQuery] string category = "",

        [FromQuery] string branch = "",

        [FromQuery] string department = "",

        CancellationToken cancellationToken = default)
    {
        search ??= string.Empty;
        status ??= string.Empty;
        category ??= string.Empty;
        branch ??= string.Empty;
        department ??= string.Empty;

        search = search.Trim();
        status = status.Trim();
        category = category.Trim();
        branch = branch.Trim();
        department = department.Trim();

        if (limit < 1 || limit > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        if (search.Length > 120)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_SEARCH",

                        message =
                            "El criterio de bÃºsqueda supera el tamaÃ±o permitido."
                    }));
        }

        var query =
            new List<string>
            {
                $"limit={limit}"
            };

        if (!string.IsNullOrWhiteSpace(
                search))
        {
            query.Add(
                $"q={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(
                status))
        {
            query.Add(
                $"status={Uri.EscapeDataString(status)}");
        }

        if (!string.IsNullOrWhiteSpace(
                category))
        {
            query.Add(
                $"category={Uri.EscapeDataString(category)}");
        }

        if (!string.IsNullOrWhiteSpace(
                branch))
        {
            query.Add(
                $"branch={Uri.EscapeDataString(branch)}");
        }

        if (!string.IsNullOrWhiteSpace(
                department))
        {
            query.Add(
                $"department={Uri.EscapeDataString(department)}");
        }

        var queryString =
            string.Join(
                "&",
                query);

        var route =
            "/api/internal/titan/inventory/assets?" +
            queryString;

        var cacheKey =
            "inventory:assets:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    300,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    [HttpGet("assets/{assetTag}")]
    public Task<IActionResult> GetAssetByTag(
        string assetTag,

        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetTag))
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_ASSET_TAG",

                        message =
                            "El tag de activo es obligatorio."
                    }));
        }

        var route =
            "/api/internal/titan/inventory/assets/" +
            Uri.EscapeDataString(assetTag);

        var cacheKey =
            "inventory:asset:tag:" +
            assetTag;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    300,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    // ============================================================
    // ASSET QR
    // ============================================================

    [HttpGet("assets/{assetTag}/qr")]
    public async Task<IActionResult> GetAssetQr(
        string assetTag,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetTag))
        {
            return BadRequest(
                new
                {
                    code = "INVENTORY_INVALID_ASSET_TAG",
                    message = "El tag de activo es obligatorio."
                });
        }

        var asset =
            await _inventoryService
                .GetAssetByTagAsync(assetTag, cancellationToken);

        if (asset is null)
        {
            return NotFound(
                new
                {
                    code = "INVENTORY_ASSET_NOT_FOUND",
                    message = $"No existe el activo con tag '{assetTag}'."
                });
        }

        var qrContent = $"CISA-TI|{asset.AssetTag}|{asset.Id}";
        var generator = new QRCodeGenerator();
        var qrData = generator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(10);

        return File(
            pngBytes,
            "image/png",
            $"CISA-TI-{asset.AssetTag}.png");
    }

    // ============================================================
    // ASSET DEPRECIATION
    // ============================================================

    [HttpGet("assets/depreciation")]
    public async Task<IActionResult> GetAssetDepreciation(
        CancellationToken cancellationToken = default)
    {
        var report =
            await _inventoryService
                .GetDepreciationReportAsync(cancellationToken);

        return Ok(report);
    }

    // ============================================================
    // DELIVERIES
    // ============================================================

    [HttpGet("deliveries")]
    public Task<IActionResult> Deliveries(
        [FromQuery] int limit = 100,

        [FromQuery] string search = "",

        [FromQuery] bool returned = false,

        CancellationToken cancellationToken = default)
    {
        search ??= string.Empty;
        search = search.Trim();

        if (limit < 1 || limit > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        var query =
            new List<string>
            {
                $"limit={limit}"
            };

        if (!string.IsNullOrWhiteSpace(
                search))
        {
            query.Add(
                $"q={Uri.EscapeDataString(search)}");
        }

        if (returned)
        {
            query.Add(
                $"returned=true");
        }

        var queryString =
            string.Join(
                "&",
                query);

        var route =
            "/api/internal/titan/inventory/deliveries?" +
            queryString;

        var cacheKey =
            "inventory:deliveries:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    120,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    [HttpPost("deliveries")]
    public Task<IActionResult> CreateDelivery(
        CancellationToken cancellationToken = default)
    {
        return ForwardAsync(
            route:
                "/api/internal/titan/inventory/deliveries",

            method:
                HttpMethod.Post,

            hasBody:
                false,

            deviceOperation:
                false,

            cancellationToken,

            "inventory.manage");
    }

    // ============================================================
    // RETURNS
    // ============================================================

    [HttpGet("returns")]
    public Task<IActionResult> Returns(
        [FromQuery] int limit = 100,

        [FromQuery] string search = "",

        CancellationToken cancellationToken = default)
    {
        search ??= string.Empty;
        search = search.Trim();

        if (limit < 1 || limit > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        var query =
            new List<string>
            {
                $"limit={limit}"
            };

        if (!string.IsNullOrWhiteSpace(
                search))
        {
            query.Add(
                $"q={Uri.EscapeDataString(search)}");
        }

        var queryString =
            string.Join(
                "&",
                query);

        var route =
            "/api/internal/titan/inventory/returns?" +
            queryString;

        var cacheKey =
            "inventory:returns:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    120,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    // ============================================================
    // RECEPTIONS
    // ============================================================

    [HttpGet("receptions")]
    public Task<IActionResult> Receptions(
        [FromQuery] int limit = 100,

        [FromQuery] string search = "",

        CancellationToken cancellationToken = default)
    {
        search ??= string.Empty;
        search = search.Trim();

        if (limit < 1 || limit > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        var query =
            new List<string>
            {
                $"limit={limit}"
            };

        if (!string.IsNullOrWhiteSpace(
                search))
        {
            query.Add(
                $"q={Uri.EscapeDataString(search)}");
        }

        var queryString =
            string.Join(
                "&",
                query);

        var route =
            "/api/internal/titan/inventory/receptions?" +
            queryString;

        var cacheKey =
            "inventory:receptions:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    120,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    // ============================================================
    // EMPLOYEES
    // ============================================================

    [HttpGet("employees")]
    public Task<IActionResult> Employees(
        [FromQuery] string search = "",

        [FromQuery] int limit = 150,

        CancellationToken cancellationToken = default)
    {
        search ??= string.Empty;
        search = search.Trim();

        if (limit < 1 || limit > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        var query =
            new List<string>
            {
                $"limit={limit}"
            };

        if (!string.IsNullOrWhiteSpace(
                search))
        {
            query.Add(
                $"q={Uri.EscapeDataString(search)}");
        }

        var queryString =
            string.Join(
                "&",
                query);

        var route =
            "/api/internal/titan/inventory/employees?" +
            queryString;

        var cacheKey =
            "inventory:employees:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    300,
                    10,
                    600)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    // ============================================================
    // INVENTORY SESSIONS
    // ============================================================

    [HttpGet("sessions")]
    public Task<IActionResult> InventorySessions(
        [FromQuery] string search = "",

        [FromQuery] string status = "",

        [FromQuery] string branch = "",

        CancellationToken cancellationToken = default)
    {
        search ??= string.Empty;
        status ??= string.Empty;
        branch ??= string.Empty;

        search = search.Trim();
        status = status.Trim();
        branch = branch.Trim();

        var query =
            new List<string>
            {
            };

        if (!string.IsNullOrWhiteSpace(
                search))
        {
            query.Add(
                $"q={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(
                status))
        {
            query.Add(
                $"status={Uri.EscapeDataString(status)}");
        }

        if (!string.IsNullOrWhiteSpace(
                branch))
        {
            query.Add(
                $"branch={Uri.EscapeDataString(branch)}");
        }

        var queryString =
            string.Join(
                "&",
                query);

        var route =
            "/api/internal/titan/inventory/sessions?" +
            queryString;

        var cacheKey =
            "inventory:sessions:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    120,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    [HttpPost("sessions")]
    public Task<IActionResult> CreateInventorySession(
        CancellationToken cancellationToken = default)
    {
        return ForwardAsync(
            route:
                "/api/internal/titan/inventory/sessions",

            method:
                HttpMethod.Post,

            hasBody:
                false,

            deviceOperation:
                false,

            cancellationToken,

            "inventory.manage");
    }

    // ============================================================
    // INVENTORY COUNTS
    // ============================================================

    [HttpGet("counts")]
    public Task<IActionResult> InventoryCounts(
        [FromQuery] string sessionId = "",

        [FromQuery] string assetTag = "",

        [FromQuery] int limit = 100,

        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            if (!Guid.TryParse(sessionId, out _))
            {
                return Task.FromResult<IActionResult>(
                    BadRequest(
                        new
                        {
                            code =
                                "INVENTORY_INVALID_SESSION",

                            message =
                                "ID de sesiÃ³n invÃ¡lido."
                        }));
            }
        }

        if (!string.IsNullOrWhiteSpace(assetTag) &&
            assetTag.Length > 60)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_ASSET_TAG",

                        message =
                            "El tag de activo es demasiado largo."
                    }));
        }

        if (limit < 1 || limit > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        var query =
            new List<string>
            {
                $"limit={limit}"
            };

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            query.Add(
                $"sessionId={Uri.EscapeDataString(sessionId)}");
        }

        if (!string.IsNullOrWhiteSpace(assetTag))
        {
            query.Add(
                $"assetTag={Uri.EscapeDataString(assetTag)}");
        }

        var queryString =
            string.Join(
                "&",
                query);

        var route =
            "/api/internal/titan/inventory/counts?" +
            queryString;

        var cacheKey =
            "inventory:counts:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    120,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    // ============================================================
    // ASSET HISTORY
    // ============================================================

    [HttpGet("history/{assetTag}")]
    public Task<IActionResult> AssetHistory(
        string assetTag,

        [FromQuery] int limit = 100,

        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assetTag))
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_ASSET_TAG",

                        message =
                            "El tag de activo es obligatorio."
                    }));
        }

        if (limit < 1 || limit > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "INVENTORY_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        var route =
            "/api/internal/titan/inventory/history/" +
            Uri.EscapeDataString(assetTag) +
            "?limit=" +
            limit;

        var cacheKey =
            "inventory:history:" +
            assetTag +
            ":" +
            limit;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    120,
                    5,
                    300)),

            deviceOperation:
                false,

            cancellationToken,

            "inventory.view");
    }

    // ============================================================
    // MODULE BRIDGE (Legacy compatibility)
    // ============================================================

    [AcceptVerbs(
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE")]
    [Route("module/{**path}")]
    [RequestSizeLimit(MaxBodyBytes)]
    public Task<IActionResult> ModuleBridge(
        string path,

        CancellationToken cancellationToken)
    {
        var segments =
            (path ?? string.Empty)
                .Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries);

        if (!IsSafeModulePath(
                segments))
        {
            return Task.FromResult<IActionResult>(
                NotFound());
        }

        var normalizedPath =
            string.Join(
                "/",
                segments);

        var requiredPermissions =
            InventoryAccess.Required(
                normalizedPath,
                Request.Method);

        if (requiredPermissions.Length == 0)
        {
            return Task.FromResult<IActionResult>(
                NotFound());
        }

        var route =
            "/api/" +
            string.Join(
                "/",
                segments.Select(
                    Uri.EscapeDataString)) +
            Request.QueryString.Value;

        /*
         * Para GET legacy utilizamos ForwardAsync por ahora.
         *
         * El cache central se concentra en los contratos Titan
         * estables para evitar almacenar combinaciones antiguas
         * que serÃ¡n eliminadas en F9.
         */
        return ForwardAsync(
            route,
            new HttpMethod(
                Request.Method),

            hasBody:
                !HttpMethods.IsGet(
                    Request.Method),

            deviceOperation:
                IsDeviceOperation(
                    normalizedPath,
                    Request.Method),

            cancellationToken,

            requiredPermissions);
    }

    // ============================================================
    // CACHED FORWARD
    // ============================================================

    private async Task<IActionResult> ForwardCachedAsync(
        string route,
        string cacheKey,
        TimeSpan lifetime,
        bool deviceOperation,
        CancellationToken cancellationToken,

        params string[] requiredPermissions)
    {
        var authorizationResult =
            ValidateAuthorization(
                requiredPermissions);

        if (authorizationResult is not null)
        {
            return authorizationResult;
        }

        var identity =
            ResolveIdentity();

        if (!identity.IsValid)
        {
            return Unauthorized(
                new
                {
                    code =
                        "INVENTORY_INVALID_IDENTITY",

                    message =
                        "La identidad de TitanMDM no contiene un usuario u organizaciÃ³n vÃ¡lidos."
                });
        }

        var manage =
            HasPermission(
                "inventory.manage");

        var operations =
            InventoryAccess.Operations(
                GetGrantedPermissions());

        try
        {
            var result =
                await _gateway
                    .SendAsync(
                        new InventoryGatewayRequest(
                            route:
                                route,

                            method:
                                HttpMethod.Get,

                            actorUserId:
                                identity.ActorUserId!,

                            organizationId:
                                identity.OrganizationId!,

                            isAdministrator:
                                manage,

                            operations:
                                operations,

                            body:
                                null,

                            contentType:
                                null,

                            deviceOperation:
                                deviceOperation),
                        cancellationToken);

            Response.Headers.CacheControl =
                "no-store";

            return BuildGatewayResponse(
                result,
                route);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return StatusCode(
                499);
        }
        catch (InventoryNotConfiguredException exception)
        {
            _logger.LogWarning(
                exception,
                "Inventario no estÃ¡ configurado.");

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "INVENTORY_NOT_CONFIGURED",

                    message =
                        "La integraciÃ³n de Inventario no estÃ¡ configurada en este servidor."
                });
        }
        catch (InventoryTimeoutException exception)
        {
            _logger.LogWarning(
                exception,
                "Timeout consultando Inventario. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status504GatewayTimeout,
                new
                {
                    code =
                        "INVENTORY_TIMEOUT",

                    message =
                        "La operaciÃ³n de Inventario superÃ³ el tiempo permitido."
                });
        }
        catch (InventoryUnavailableException exception)
        {
            _logger.LogWarning(
                exception,
                "Inventario Edge no estÃ¡ disponible. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "INVENTORY_UNAVAILABLE",

                    message =
                        "El servicio interno de Inventario no estÃ¡ disponible."
                });
        }
    }

    // ============================================================
    // STANDARD FORWARD
    // ============================================================

    private async Task<IActionResult> ForwardAsync(
        string route,
        HttpMethod method,
        bool hasBody,
        bool deviceOperation,
        CancellationToken cancellationToken,

        params string[] requiredPermissions)
    {
        var authorizationResult =
            ValidateAuthorization(
                requiredPermissions);

        if (authorizationResult is not null)
        {
            return authorizationResult;
        }

        if (hasBody &&
            Request.ContentLength
                is > MaxBodyBytes)
        {
            return StatusCode(
                StatusCodes
                    .Status413PayloadTooLarge,
                new
                {
                    code =
                        "INVENTORY_PAYLOAD_TOO_LARGE",

                    message =
                        "El contenido supera el lÃ­mite de 8 MB."
                });
        }

        var identity =
            ResolveIdentity();

        if (!identity.IsValid)
        {
            return Unauthorized(
                new
                {
                    code =
                        "INVENTORY_INVALID_IDENTITY",

                    message =
                        "La identidad de TitanMDM no contiene un usuario u organizaciÃ³n vÃ¡lidos."
                });
        }

        var manage =
            HasPermission(
                "inventory.manage");

        var operations =
            InventoryAccess.Operations(
                GetGrantedPermissions());

        try
        {
            var result =
                await _gateway
                    .SendAsync(
                        new InventoryGatewayRequest(
                            route:
                                route,

                            method:
                                method,

                            actorUserId:
                                identity.ActorUserId!,

                            organizationId:
                                identity.OrganizationId!,

                            isAdministrator:
                                manage,

                            operations:
                                operations,

                            body:
                                hasBody
                                    ? new System.IO.StreamReader(Request.Body)
                                        .ReadToEndAsync()
                                        .Result
                                        .Select(b => (byte)b)
                                        .ToArray()
                                    : null,

                            contentType:
                                hasBody
                                    ? Request.ContentType
                                    : null,

                            deviceOperation:
                                deviceOperation),
                        cancellationToken);

            /*
             * Toda escritura exitosa invalida los datos
             * operacionales relacionados.
             *
             * Evitamos mostrar Dashboard, Employees,
             * Devices o Records obsoletos tras una acciÃ³n.
             */
            if (method != HttpMethod.Get &&
                result.StatusCode
                    is >= 200
                    and < 300)
            {
                // Se espera la invalidacion: fire-and-forget con el token de la
                // peticion se cancelaba al responder y dejaba cache obsoleta.
                try
                {
                await _gateway
                    .SendAsync(
                        new InventoryGatewayRequest(
                            route:
                                "/api/internal/titan/inventory/invalidate-operational-data",

                            method:
                                HttpMethod.Post,

                            actorUserId:
                                identity.ActorUserId!,

                            organizationId:
                                identity.OrganizationId!,

                            isAdministrator:
                                manage,

                            operations:
                                Array
                                    .Empty<string>(),

                            body:
                                null,

                            contentType:
                                null,

                            deviceOperation:
                                false),
                        cancellationToken);
                }
                catch (Exception invalidationError)
                    when (invalidationError is not OperationCanceledException)
                {
                    // La escritura ya fue confirmada: no se convierte en error.
                    _logger.LogWarning(
                        invalidationError,
                        "No se pudo invalidar la cache operacional de inventario.");
                }
            }

            /*
             * Python nunca debe decidir la polÃ­tica de cachÃ©
             * pÃºblica del API TitanMDM.
             */
            Response.Headers.CacheControl =
                "no-store";

            return BuildGatewayResponse(
                result,
                route);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return StatusCode(
                499);
        }
        catch (InventoryNotConfiguredException exception)
        {
            _logger.LogWarning(
                exception,
                "Inventario no estÃ¡ configurado.");

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "INVENTORY_NOT_CONFIGURED",

                    message =
                        "La integraciÃ³n de Inventario no estÃ¡ configurada en este servidor."
                });
        }
        catch (InventoryTimeoutException exception)
        {
            _logger.LogWarning(
                exception,
                "Timeout de Inventario. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status504GatewayTimeout,
                new
                {
                    code =
                        "INVENTORY_TIMEOUT",

                    message =
                        "La operaciÃ³n de Inventario superÃ³ el tiempo permitido. " +
                        "Si fue una escritura en activo, consulta el historial antes de repetirla."
                });
        }
        catch (InventoryUnavailableException exception)
        {
            _logger.LogWarning(
                exception,
                "Inventario Edge no estÃ¡ disponible. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "INVENTORY_UNAVAILABLE",

                    message =
                        "El servicio interno de Inventario no estÃ¡ disponible."
                });
        }
    }

    // ============================================================
    // RESPONSE
    // ============================================================

    private IActionResult BuildGatewayResponse(
        InventoryGatewayResponse result,

        string route)
    {
        if (!string.IsNullOrWhiteSpace(
                result.ContentDisposition))
        {
            Response.Headers
                .ContentDisposition =
                    result.ContentDisposition;
        }

        /*
         * Una 401 del Edge no representa que el usuario Titan
         * haya perdido sesiÃ³n.
         *
         * Significa que la autenticaciÃ³n machine-to-machine
         * TitanMDM -> Inventario Edge fue rechazada.
         */
        if (result.StatusCode ==
            StatusCodes.Status401Unauthorized)
        {
            _logger.LogWarning(
                "Inventario Edge rechazÃ³ la credencial interna. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status502BadGateway,
                new
                {
                    code =
                        "INVENTORY_INTEGRATION_AUTH",

                    message =
                        "La autenticaciÃ³n interna entre TitanMDM y Inventario Edge fue rechazada."
                });
        }

        Response.StatusCode =
            result.StatusCode;

        return File(
            result.Body,
            string.IsNullOrWhiteSpace(
                result.ContentType)
                ? "application/json"
                : result.ContentType);
    }

    // ============================================================
    // AUTHORIZATION
    // ============================================================

    private IActionResult? ValidateAuthorization(
        IReadOnlyCollection<string>
            requiredPermissions)
    {
        var manage =
            HasPermission(
                "inventory.manage");

        if (!manage &&
            !HasPermission(
                "workspace.inventory.view"))
        {
            return Forbid();
        }

        /*
         * Required() devuelve en muchos casos varias
         * alternativas vÃ¡lidas.
         *
         * Basta poseer una de ellas.
         */
        if (!manage &&
            requiredPermissions.Count > 0 &&
            !requiredPermissions.Any(
                HasPermission))
        {
            return Forbid();
        }

        return null;
    }

    private bool HasPermission(
        string permission)
    {
        return User.Claims.Any(
            claim =>
                string.Equals(
                    claim.Type,
                    "permission",
                    StringComparison.OrdinalIgnoreCase)
                &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<string>
        GetGrantedPermissions()
    {
        return User.Claims
            .Where(
                claim =>
                    string.Equals(
                        claim.Type,
                        "permission",
                        StringComparison.OrdinalIgnoreCase))
            .Select(
                claim =>
                    claim.Value)
            .Distinct(
                StringComparer.OrdinalIgnoreCase);
    }

    private InventoryIdentity ResolveIdentity()
    {
        var actor =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ??
            User.FindFirstValue(
                "sub");

        var organization =
            User.FindFirstValue(
                "organization_id")
            ??
            User.FindFirstValue(
                "organizationId");

        var validActor =
            Guid.TryParse(
                actor,
                out _);

        var validOrganization =
            Guid.TryParse(
                organization,
                out _);

        return new InventoryIdentity(
            ActorUserId:
                actor,

            OrganizationId:
                organization,

            IsValid:
                validActor &&
                validOrganization);
    }

    // ============================================================
    // LEGACY SECURITY
    // ============================================================

    private static bool IsSafeModulePath(
        IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
        {
            return false;
        }

        if (!ModuleRoots.Contains(
                segments[0]))
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                return false;
            }

            if (segment.Contains(
                    '\\'))
            {
                return false;
            }

            if (segment.Contains(
                    ':'))
            {
                return false;
            }

            if (segment.Any(
                    char.IsControl))
            {
                return false;
            }

            if (segment.Length > 120)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDeviceOperation(
        string path,

        string method)
    {
        if (HttpMethods.IsGet(
                method))
        {
            return path.Contains(
                       "history",
                       StringComparison.OrdinalIgnoreCase)
                    ||
                    path.Contains(
                        "assets",
                        StringComparison.OrdinalIgnoreCase);
        }

        return path.Contains(
                   "assets",
                   StringComparison.OrdinalIgnoreCase)
                ||
                path.Contains(
                    "counts",
                    StringComparison.OrdinalIgnoreCase)
                ||
                path.Contains(
                    "sessions",
                    StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSafeRouteForLog(
        string route)
    {
        var questionMarkIndex =
            route.IndexOf(
                '?',
                StringComparison.Ordinal);

        return questionMarkIndex >= 0
            ? route[..questionMarkIndex]
            : route;
    }

    private sealed record InventoryIdentity(
        string? ActorUserId,

        string? OrganizationId,

        bool IsValid);
}

// ================================================================
// TITANMDM <-> INVENTARIO PERMISSION TRANSLATION
// ================================================================

public static class InventoryAccess
{
    private static readonly Dictionary<
        string,
        string[]>
        Map =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                ["inventory.view"] =
                [
                    "inventory.read",
                    "devices.read",
                    "zk.read"
                ],

                ["inventory.manage"] =
                [
                    "inventory.read",
                    "inventory.write",
                    "devices.read",
                    "devices.write",
                    "zk.read"
                ],

                ["inventory.history.view"] =
                [
                    "settings.read",
                    "inventory.read"
                ],

                ["inventory.counts.view"] =
                [
                    "inventory.read"
                ],

                ["inventory.sessions.view"] =
                [
                    "inventory.read"
                ],

                ["inventory.deliveries.view"] =
                [
                    "inventory.read"
                ],

                ["inventory.returns.view"] =
                [
                    "inventory.read"
                ],

                ["inventory.receptions.view"] =
                [
                    "inventory.read"
                ]
            };

    public static string[] Operations(
        IEnumerable<string> permissions)
    {
        var granted =
            permissions.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> values =
            Map
                .Where(
                    pair =>
                        granted.Contains(
                            pair.Key))
                .SelectMany(
                    pair =>
                        pair.Value);

        if (granted.Contains(
                "inventory.manage"))
        {
            values =
                Map.Values
                    .SelectMany(
                        value =>
                            value)
                    .Concat(
                        new[]
                        {
                            "users.read",
                            "users.write",
                            "users.delete",

                            "roles.read",
                            "roles.write",

                            "devices.read",
                            "devices.write",

                            "zk.sync"
                        });
        }

        return values
            .Distinct(
                StringComparer.Ordinal)
            .OrderBy(
                value =>
                    value,
                StringComparer.Ordinal)
            .ToArray();
    }

    public static string[] Required(
        string path,
        string method)
    {
        var parts =
            path
                .ToLowerInvariant()
                .Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return [];
        }

        var root =
            parts[0];

        var action =
            parts.Length > 1
                ? parts[1]
                : string.Empty;

        var read =
            method.Equals(
                "GET",
                StringComparison.OrdinalIgnoreCase);

        // ========================================================
        // INVENTORY
        // ========================================================

        if (root == "inventory")
        {
            if (action == "assets")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "asset" &&
                parts.Length >= 3 &&
                parts[2].IsNumeric())
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "deliveries")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "returns")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "receptions")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "employees")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "sessions")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "counts")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "history")
            {
                return read
                    ? new[] { "inventory.history.view" }
                    : new[] { "inventory.manage" };
            }

            if (action == "assets" &&
                parts.Length >= 3 &&
                parts[2] == "search")
            {
                return read
                    ? new[] { "inventory.view" }
                    : new[] { "inventory.manage" };
            }

            return read
                ? new[] { "inventory.view" }
                : new[] { "inventory.manage" };
        }

        return [];
    }
}

static class StringExtensions
{
    public static bool IsNumeric(
        this string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        return int.TryParse(
            s,
            out _);
    }
}