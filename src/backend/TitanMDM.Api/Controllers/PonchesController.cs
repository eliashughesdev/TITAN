using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using TitanMDM.Api.Ponches;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ponches")]
public sealed class PonchesController : ControllerBase
{
    private const long MaxBodyBytes =
        8L * 1024L * 1024L;

    private static readonly HashSet<string> ModuleRoots =
        new(
            StringComparer.OrdinalIgnoreCase)
        {
            "records",
            "devices",
            "collaborators",
            "payroll",
            "exports",
            "schema",
            "settings",
            "users",
            "remote"
        };

    private readonly IPonchesGateway
        _gateway;

    private readonly IPonchesQueryCache
        _cache;

    private readonly PonchesCacheOptions
        _cacheOptions;

    private readonly ILogger<PonchesController>
        _logger;

    public PonchesController(
        IPonchesGateway gateway,
        IPonchesQueryCache cache,
        IOptions<PonchesCacheOptions> cacheOptions,
        ILogger<PonchesController> logger)
    {
        _gateway =
            gateway
            ??
            throw new ArgumentNullException(
                nameof(gateway));

        _cache =
            cache
            ??
            throw new ArgumentNullException(
                nameof(cache));

        _cacheOptions =
            cacheOptions?.Value
            ??
            new PonchesCacheOptions();

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

            "workspace.ponches.view");
    }

    // ============================================================
    // DASHBOARD
    // ============================================================

    [HttpGet("dashboard")]
    public Task<IActionResult> Dashboard(
        CancellationToken cancellationToken)
    {
        return ForwardCachedAsync(
            route:
                "/api/internal/titan/dashboard",

            cacheKey:
                "dashboard:main",

            lifetime:
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        _cacheOptions
                            .DashboardSeconds,
                        5,
                        300)),

            deviceOperation:
                false,

            cancellationToken,

            "ponches.dashboard.view");
    }

    /*
     * Contrato temporal de compatibilidad.
     * Lo retiraremos en F9 cuando el Dashboard nuevo ya cubra
     * completamente al antiguo.
     */
    [HttpGet("dashboard-original")]
    public Task<IActionResult> OriginalDashboard(
        CancellationToken cancellationToken)
    {
        return ForwardCachedAsync(
            route:
                "/api/internal/titan/dashboard-original",

            cacheKey:
                "dashboard:original",

            lifetime:
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        _cacheOptions
                            .DashboardSeconds,
                        5,
                        300)),

            deviceOperation:
                false,

            cancellationToken,

            "ponches.dashboard.view");
    }

    // ============================================================
    // DEVICE HEALTH
    // ============================================================

    [HttpGet("device-health")]
    public Task<IActionResult> DeviceHealth(
        CancellationToken cancellationToken)
    {
        return ForwardCachedAsync(
            route:
                "/api/internal/titan/device-health",

            cacheKey:
                "devices:health",

            lifetime:
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        _cacheOptions
                            .DeviceHealthSeconds,
                        5,
                        120)),

            deviceOperation:
                true,

            cancellationToken,

            "ponches.devices.view");
    }

    // ============================================================
    // RECORDS
    //
    // Contrato pÃºblico TitanMDM.
    //
    // Internamente todavÃ­a utiliza records/search del Edge
    // porque F3-F9 estÃ¡n eliminando gradualmente el API legado.
    // El frontend ya no debe conocer esa ruta.
    // ============================================================

    [HttpGet("records")]
    public Task<IActionResult> Records(
        [FromQuery]
        int limit = 100,

        [FromQuery]
        string search = "",

        [FromQuery]
        string fecha = "",

        [FromQuery]
        string dispositivo = "",

        CancellationToken cancellationToken = default)
    {
        search ??=
            string.Empty;

        fecha ??=
            string.Empty;

        dispositivo ??=
            string.Empty;

        search =
            search.Trim();

        fecha =
            fecha.Trim();

        dispositivo =
            dispositivo.Trim();

        if (limit is < 1 or > 500)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "PONCHES_INVALID_LIMIT",

                        message =
                            "El lÃ­mite debe estar entre 1 y 500."
                    }));
        }

        if (search.Length > 80)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "PONCHES_INVALID_SEARCH",

                        message =
                            "El criterio de bÃºsqueda supera el tamaÃ±o permitido."
                    }));
        }

        if (fecha.Length > 20)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "PONCHES_INVALID_DATE",

                        message =
                            "La fecha suministrada no es vÃ¡lida."
                    }));
        }

        if (dispositivo.Length > 160)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "PONCHES_INVALID_DEVICE",

                        message =
                            "El dispositivo suministrado no es vÃ¡lido."
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
                fecha))
        {
            query.Add(
                $"fecha={Uri.EscapeDataString(fecha)}");
        }

        if (!string.IsNullOrWhiteSpace(
                dispositivo) &&
            !string.Equals(
                dispositivo,
                "todos",
                StringComparison.OrdinalIgnoreCase))
        {
            query.Add(
                $"dispositivo={Uri.EscapeDataString(dispositivo)}");
        }

        var queryString =
            string.Join(
                "&",
                query);

        /*
         * Ruta interna temporal.
         *
         * La UI sÃ³lo conoce:
         * /api/ponches/records
         */
        var route =
            "/api/records/search?" +
            queryString;

        var cacheKey =
            "records:" +
            queryString;

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    _cacheOptions
                        .RecordsSeconds,
                    5,
                    120)),
            deviceOperation:
                false,
            cancellationToken,
            "ponches.records.view");
    }

    // ============================================================
    // EMPLOYEES
    // ============================================================

    [HttpGet("employees")]
    public Task<IActionResult> Employees(
        [FromQuery]
        string search = "",

        [FromQuery]
        int limit = 150,

        CancellationToken cancellationToken = default)
    {
        search ??=
            string.Empty;

        search =
            search.Trim();

        if (search.Length > 80)
        {
            return Task.FromResult<IActionResult>(
                BadRequest(
                    new
                    {
                        code =
                            "PONCHES_INVALID_SEARCH",

                        message =
                            "El criterio de bÃºsqueda supera el tamaÃ±o permitido."
                    }));
        }

        limit =
            Math.Clamp(
                limit,
                1,
                500);

        var route =
            "/api/records/employees" +
            $"?q={Uri.EscapeDataString(search)}" +
            $"&limit={limit}";

        var cacheKey =
            $"employees:{search}:{limit}";

        return ForwardCachedAsync(
            route,
            cacheKey,
            TimeSpan.FromSeconds(
                Math.Clamp(
                    _cacheOptions
                        .EmployeesSeconds,
                    10,
                    600)),
            deviceOperation:
                false,
            cancellationToken,
            "ponches.employees.view");
    }

    // ============================================================
    // DEVICES
    // ============================================================

    [HttpGet("devices")]
    public Task<IActionResult> Devices(
        CancellationToken cancellationToken)
    {
        return ForwardCachedAsync(
            route:
                "/api/records/managed-devices",

            cacheKey:
                "devices:catalog",

            lifetime:
                TimeSpan.FromSeconds(
                    Math.Clamp(
                        _cacheOptions
                            .DevicesSeconds,
                        10,
                        300)),

            deviceOperation:
                true,

            cancellationToken,

            "ponches.devices.view");
    }

    // ============================================================
    // MODULE COMPATIBILITY ADAPTER
    //
    // IMPORTANTE:
    //
    // Este bridge permanece mientras F4-F9 migran completamente
    // las pantallas histÃ³ricas de Ponches hacia contratos estables
    // TitanMDM.
    //
    // En F9 serÃ¡ reducido o eliminado.
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
            PonchesAccess.Required(
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
                        "PONCHES_INVALID_IDENTITY",

                    message =
                        "La identidad de TitanMDM no contiene un usuario u organizaciÃ³n vÃ¡lidos."
                });
        }

        var manage =
            HasPermission(
                "ponches.manage");

        var operations =
            PonchesAccess.Operations(
                GetGrantedPermissions());

        try
        {
            var result =
                await _cache
                    .GetOrCreateAsync(
                        identity.OrganizationId!,
                        cacheKey,
                        lifetime,
                        async () =>
                            await _gateway
                                .SendAsync(
                                    new PonchesGatewayRequest(
                                        Route:
                                            route,

                                        Method:
                                            HttpMethod.Get,

                                        ActorUserId:
                                            identity.ActorUserId!,

                                        OrganizationId:
                                            identity.OrganizationId!,

                                        IsAdministrator:
                                            manage,

                                        Operations:
                                            operations,

                                        Body:
                                            null,

                                        ContentType:
                                            null,

                                        DeviceOperation:
                                            deviceOperation),
                                    cancellationToken),
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
        catch (PonchesNotConfiguredException exception)
        {
            _logger.LogWarning(
                exception,
                "Ponches no estÃ¡ configurado.");

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "PONCHES_NOT_CONFIGURED",

                    message =
                        "La integraciÃ³n de Ponches no estÃ¡ configurada en este servidor."
                });
        }
        catch (PonchesTimeoutException exception)
        {
            _logger.LogWarning(
                exception,
                "Timeout consultando Ponches. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status504GatewayTimeout,
                new
                {
                    code =
                        "PONCHES_TIMEOUT",

                    message =
                        "La operaciÃ³n de Ponches superÃ³ el tiempo permitido."
                });
        }
        catch (PonchesUnavailableException exception)
        {
            _logger.LogWarning(
                exception,
                "Ponches Edge no estÃ¡ disponible. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "PONCHES_UNAVAILABLE",

                    message =
                        "El servicio interno de Ponches no estÃ¡ disponible."
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

        /*
         * Eliminar o sincronizar colaboradores puede afectar
         * directamente relojes fÃ­sicos.
         */
        if (!HasPermission(
                "ponches.manage") &&
            RequiresCollaboratorSyncPermission(
                route) &&
            !HasPermission(
                "ponches.collaborators.sync"))
        {
            return Forbid();
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
                        "PONCHES_PAYLOAD_TOO_LARGE",

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
                        "PONCHES_INVALID_IDENTITY",

                    message =
                        "La identidad de TitanMDM no contiene un usuario u organizaciÃ³n vÃ¡lidos."
                });
        }

        var manage =
            HasPermission(
                "ponches.manage");

        var operations =
            PonchesAccess.Operations(
                GetGrantedPermissions());

        try
        {
            var result =
                await _gateway.SendAsync(
                    new PonchesGatewayRequest(
                        Route:
                            route,

                        Method:
                            method,

                        ActorUserId:
                            identity.ActorUserId!,

                        OrganizationId:
                            identity.OrganizationId!,

                        IsAdministrator:
                            manage,

                        Operations:
                            operations,

                        Body:
                            hasBody
                                ? Request.Body
                                : null,

                        ContentType:
                            hasBody
                                ? Request.ContentType
                                : null,

                        DeviceOperation:
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
                _cache
                    .InvalidateOperationalData(
                        identity.OrganizationId!);
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
        catch (PonchesNotConfiguredException exception)
        {
            _logger.LogWarning(
                exception,
                "Ponches no estÃ¡ configurado.");

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "PONCHES_NOT_CONFIGURED",

                    message =
                        "La integraciÃ³n de Ponches no estÃ¡ configurada en este servidor."
                });
        }
        catch (PonchesTimeoutException exception)
        {
            _logger.LogWarning(
                exception,
                "Timeout de Ponches. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status504GatewayTimeout,
                new
                {
                    code =
                        "PONCHES_TIMEOUT",

                    message =
                        "La operaciÃ³n de Ponches superÃ³ el tiempo permitido. " +
                        "Si fue una escritura en reloj, consulta el historial antes de repetirla."
                });
        }
        catch (PonchesUnavailableException exception)
        {
            _logger.LogWarning(
                exception,
                "Ponches Edge no estÃ¡ disponible. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status503ServiceUnavailable,
                new
                {
                    code =
                        "PONCHES_UNAVAILABLE",

                    message =
                        "El servicio interno de Ponches no estÃ¡ disponible."
                });
        }
    }

    // ============================================================
    // RESPONSE
    // ============================================================

    private IActionResult BuildGatewayResponse(
        PonchesGatewayResponse result,
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
         * TitanMDM -> Ponches Edge fue rechazada.
         */
        if (result.StatusCode ==
            StatusCodes.Status401Unauthorized)
        {
            _logger.LogWarning(
                "Ponches Edge rechazÃ³ la credencial interna. Route={Route}",
                GetSafeRouteForLog(
                    route));

            return StatusCode(
                StatusCodes
                    .Status502BadGateway,
                new
                {
                    code =
                        "PONCHES_INTEGRATION_AUTH",

                    message =
                        "La autenticaciÃ³n interna entre TitanMDM y Ponches Edge fue rechazada."
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
                "ponches.manage");

        if (!manage &&
            !HasPermission(
                "workspace.ponches.view"))
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

    private PonchesIdentity ResolveIdentity()
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

        return new PonchesIdentity(
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

    private static bool RequiresCollaboratorSyncPermission(
        string route)
    {
        return route.StartsWith(
                   "/api/records/collaborator-delete",
                   StringComparison.OrdinalIgnoreCase)
               ||
               route.StartsWith(
                   "/api/collaborators/delete",
                   StringComparison.OrdinalIgnoreCase)
               ||
               route.StartsWith(
                   "/api/records/collab-push",
                   StringComparison.OrdinalIgnoreCase)
               ||
               route.StartsWith(
                   "/api/records/collab-clone",
                   StringComparison.OrdinalIgnoreCase)
               ||
               route.StartsWith(
                   "/api/records/collab-delete-clocks",
                   StringComparison.OrdinalIgnoreCase)
               ||
               route.StartsWith(
                   "/api/records/collab-reconcile",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeviceOperation(
        string path,
        string method)
    {
        if (HttpMethods.IsGet(
                method))
        {
            return path.Contains(
                       "device-health",
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   path.Contains(
                       "devices/test",
                       StringComparison.OrdinalIgnoreCase);
        }

        return path.Contains(
                   "device",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "zk",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "collab-push",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "collab-clone",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "collab-delete-clocks",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "collab-reconcile",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "bulk-enroll",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "sync-now",
                   StringComparison.OrdinalIgnoreCase)
               ||
               path.Contains(
                   "remote-punch",
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

    private sealed record PonchesIdentity(
        string? ActorUserId,
        string? OrganizationId,
        bool IsValid);
}

// ================================================================
// TITANMDM <-> PONCHES PERMISSION TRANSLATION
// ================================================================

public static class PonchesAccess
{
    private static readonly Dictionary<
        string,
        string[]>
        Map =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                ["ponches.dashboard.view"] =
                [
                    "attendance.read",
                    "devices.read"
                ],

                ["ponches.records.view"] =
                [
                    "attendance.read"
                ],

                ["ponches.history.view"] =
                [
                    "settings.read",
                    "attendance.read"
                ],

                ["ponches.remote.create"] =
                [
                    "remote_punch",
                    "devices.read",
                    "attendance.read"
                ],

                ["ponches.devices.view"] =
                [
                    "devices.read",
                    "zk.read"
                ],

                ["ponches.devices.manage"] =
                [
                    "devices.read",
                    "devices.write",
                    "devices.delete",
                    "zk.read"
                ],

                ["ponches.employees.view"] =
                [
                    "attendance.read"
                ],

                ["ponches.collaborators.view"] =
                [
                    "collaborators.read",
                    "devices.read",
                    "schedules.read"
                ],

                ["ponches.collaborators.manage"] =
                [
                    "collaborators.read",
                    "collaborators.write",
                    "devices.read",
                    "schedules.read"
                ],

                ["ponches.collaborators.sync"] =
                [
                    "collaborators.read",
                    "collaborators.sync",
                    "devices.read",
                    "zk.read",
                    "zk.push",
                    "zk.clone",
                    "zk.move",
                    "zk.delete"
                ],

                ["ponches.schedules.view"] =
                [
                    "schedules.read"
                ],

                ["ponches.schedules.manage"] =
                [
                    "schedules.read",
                    "schedules.write"
                ],

                ["ponches.inventory.view"] =
                [
                    "inventory.read",
                    "devices.read",
                    "zk.read"
                ],

                ["ponches.inventory.manage"] =
                [
                    "inventory.read",
                    "inventory.write",
                    "devices.read",
                    "devices.write",
                    "zk.read"
                ],

                ["ponches.bulk.execute"] =
                [
                    "bulk.execute",
                    "devices.read",
                    "zk.read",
                    "zk.enroll"
                ],

                ["ponches.reports.view"] =
                [
                    "reports.read",
                    "attendance.read",
                    "exports.read",
                    "schedules.read"
                ],

                ["ponches.advanced-reports.view"] =
                [
                    "reports.read",
                    "attendance.read",
                    "exports.read",
                    "schedules.read"
                ],

                ["ponches.export"] =
                [
                    "exports.read",
                    "reports.export",
                    "attendance.read"
                ],

                ["ponches.sync.view"] =
                [
                    "sync.read"
                ],

                ["ponches.sync.run"] =
                [
                    "sync.read",
                    "sync.run"
                ],

                ["ponches.settings.view"] =
                [
                    "settings.read"
                ],

                ["ponches.settings.manage"] =
                [
                    "settings.read",
                    "settings.write"
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
                "ponches.manage"))
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

                            "payroll.run",

                            "schema.admin",

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
        // SETTINGS
        // ========================================================

        if (root == "settings")
        {
            return
            [
                read
                    ? "ponches.settings.view"
                    : "ponches.settings.manage"
            ];
        }

        // ========================================================
        // SCHEMA
        // ========================================================

        if (root == "schema")
        {
            return read
                ?
                [
                    "ponches.history.view"
                ]
                :
                [];
        }

        // ========================================================
        // PAYROLL / REPORTS
        // ========================================================

        if (root == "payroll" &&
            action == "overtime" &&
            method.Equals(
                "POST",
                StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "ponches.reports.view",
                "ponches.advanced-reports.view"
            ];
        }

        if (root == "payroll" &&
            action == "rotations")
        {
            return
            [
                read
                    ? "ponches.schedules.view"
                    : "ponches.schedules.manage"
            ];
        }

        // ========================================================
        // RECORDS
        // ========================================================

        if (root == "records")
        {
            if (action == "export" &&
                parts.Length == 3 &&
                parts[2] is "excel" or "pdf")
            {
                return
                [
                    "ponches.export"
                ];
            }

            if (action == "export-devices" &&
                read)
            {
                return
                [
                    "ponches.export",
                    "ponches.reports.view",
                    "ponches.advanced-reports.view"
                ];
            }

            /*
             * GestiÃ³n de usuarios TitanMDM pertenece
             * al mÃ³dulo central, no a Ponches legacy.
             */
            if (action is
                "app-users"
                or
                "app-roles")
            {
                return [];
            }

            if (action ==
                "inventory-devices")
            {
                return
                [
                    read
                        ? "ponches.inventory.view"
                        : "ponches.inventory.manage"
                ];
            }

            if (action ==
                "schedules")
            {
                return
                [
                    read
                        ? "ponches.schedules.view"
                        : "ponches.schedules.manage"
                ];
            }

            if (action ==
                    "sync-history" &&
                read)
            {
                return
                [
                    "ponches.sync.view",
                    "ponches.sync.run"
                ];
            }

            if (action ==
                    "sync-now" &&
                method.Equals(
                    "POST",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    "ponches.sync.run"
                ];
            }

            if (action is
                "remote-punch"
                or
                "remote-devices")
            {
                return
                [
                    "ponches.remote.create"
                ];
            }

            if (action is
                "collab-push"
                or
                "collab-clone"
                or
                "collab-delete-clocks"
                or
                "collab-reconcile")
            {
                return read
                    ?
                    [
                        "ponches.collaborators.view",
                        "ponches.collaborators.sync"
                    ]
                    :
                    [
                        "ponches.collaborators.sync"
                    ];
            }

            if (action ==
                    "collaborator-copy" &&
                !read)
            {
                return
                [
                    "ponches.collaborators.sync"
                ];
            }

            if (action.StartsWith(
                    "collaborator",
                    StringComparison.Ordinal))
            {
                return
                [
                    read
                        ? "ponches.collaborators.view"
                        : "ponches.collaborators.manage"
                ];
            }

            if (action ==
                "bulk-enroll")
            {
                return
                [
                    "ponches.bulk.execute"
                ];
            }

            /*
             * CatÃ¡logo de relojes reutilizado por varias
             * pantallas. La autorizaciÃ³n puede provenir
             * de cualquiera de esos mÃ³dulos.
             */
            if (action ==
                    "managed-devices" &&
                read)
            {
                return
                [
                    "ponches.devices.view",
                    "ponches.devices.manage",

                    "ponches.inventory.view",
                    "ponches.inventory.manage",

                    "ponches.collaborators.view",
                    "ponches.collaborators.manage",
                    "ponches.collaborators.sync",

                    "ponches.bulk.execute"
                ];
            }

            if (action ==
                "managed-devices")
            {
                return
                [
                    "ponches.devices.manage",
                    "ponches.inventory.manage"
                ];
            }

            if (action ==
                    "device-health" &&
                read)
            {
                return
                [
                    "ponches.devices.view",
                    "ponches.dashboard.view"
                ];
            }

            if (action ==
                    "employees" &&
                read)
            {
                return
                [
                    "ponches.employees.view"
                ];
            }

            if (action ==
                    "devices" &&
                read)
            {
                return
                [
                    "ponches.records.view",
                    "ponches.export",
                    "ponches.reports.view",
                    "ponches.advanced-reports.view"
                ];
            }

            if (action is
                "dashboard-combined"
                or
                "summary"
                or
                "stats"
                or
                "ops-overview")
            {
                return read
                    ?
                    [
                        "ponches.dashboard.view"
                    ]
                    :
                    [];
            }

            if (action is
                "recent"
                or
                "search"
                or
                "columns")
            {
                return read
                    ?
                    [
                        "ponches.records.view",
                        "ponches.dashboard.view"
                    ]
                    :
                    [];
            }
        }

        // ========================================================
        // DEVICES
        // ========================================================

        if (root == "devices")
        {
            if (parts.Length == 3 &&
                parts[2] == "test" &&
                method.Equals(
                    "POST",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    "ponches.devices.view",
                    "ponches.inventory.view"
                ];
            }

            if (action ==
                    "enroll-bio" &&
                method.Equals(
                    "POST",
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    "ponches.bulk.execute"
                ];
            }

            return read
                ?
                [
                    "ponches.inventory.view",
                    "ponches.devices.view",
                    "ponches.collaborators.sync"
                ]
                :
                [
                    "ponches.collaborators.sync"
                ];
        }

        // ========================================================
        // COLLABORATORS
        // ========================================================

        if (root ==
            "collaborators")
        {
            if (!read &&
                action is
                    "collaborator-sync"
                    or
                    "copy-device"
                    or
                    "collab-push")
            {
                return
                [
                    "ponches.collaborators.sync"
                ];
            }

            return
            [
                read
                    ? "ponches.collaborators.view"
                    : "ponches.collaborators.manage"
            ];
        }

        return [];
    }
}