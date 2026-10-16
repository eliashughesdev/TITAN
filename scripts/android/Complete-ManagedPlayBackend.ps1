
# TITANMDM — AE-PLAY B1/B2
# Completa servicio + endpoint existente.
# No modifica main, no genera migraciones y no hace push.

$ErrorActionPreference = "Stop"

$root = "C:\TitanMDM"
$servicePath = Join-Path $root "src\backend\TitanMDM.Infrastructure\Android\AndroidEnterpriseService.cs"
$controllerPath = Join-Path $root "src\backend\TitanMDM.Api\Controllers\AndroidEnterpriseController.cs"

Set-Location $root

if ((git branch --show-current).Trim() -ne "feat/remote-support-rs-h") {
    throw "No estas en la rama de desarrollo."
}

foreach ($path in @($servicePath, $controllerPath)) {
    if (!(Test-Path -LiteralPath $path)) {
        throw "Archivo inexistente: $path"
    }
}

$service = Get-Content -LiteralPath $servicePath -Raw -Encoding UTF8
$controller = Get-Content -LiteralPath $controllerPath -Raw -Encoding UTF8

$serviceMarker = "    private static void ValidateOrganization("
$controllerMarker = "    private Guid? GetOrganizationId()"

if (!$service.Contains($serviceMarker) -or !$controller.Contains($controllerMarker)) {
    throw "La estructura cambio. Se detiene para evitar modificaciones incorrectas."
}

$serviceMethod = @'
    // AE-PLAY: token efimero para Managed Google Play.
    public async Task<AndroidWebTokenDto> CreateManagedPlayWebTokenAsync(
        Guid organizationId,
        Guid userId,
        CreateAndroidWebTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateOrganization(organizationId);

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId requerido.", nameof(userId));

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.IframeOrigin))
            throw new ArgumentException("Origen HTTPS requerido.");

        if (!Uri.TryCreate(request.IframeOrigin, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment) ||
            origin.AbsolutePath != "/")
        {
            throw new ArgumentException("El origen del iframe debe ser HTTPS y no contener ruta, consulta ni fragmento.");
        }

        var config = await _dbContext.AndroidEnterpriseConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId,
                cancellationToken);

        if (config is null ||
            config.Status != AndroidEnterpriseStatus.Active ||
            string.IsNullOrWhiteSpace(config.EnterpriseName))
        {
            throw new InvalidOperationException(
                "La organizacion no tiene Android Enterprise activo.");
        }

        // Permisos y paginas se limitan desde servidor.
        var features = new JsonArray(
            "PLAY_SEARCH",
            "PRIVATE_APPS",
            "WEB_APPS",
            "STORE_BUILDER",
            "MANAGED_CONFIGURATIONS");

        var payload = new JsonObject
        {
            ["parentFrameUrl"] = origin.GetLeftPart(UriPartial.Authority),
            ["enabledFeatures"] = features
        };

        var response = await _client.CreateWebTokenAsync(
            config.EnterpriseName,
            payload,
            cancellationToken);

        var token = response["value"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "Google no devolvio un Web Token valido.");

        var url =
            "https://play.google.com/work/embedded/search" +
            "?token=" + Uri.EscapeDataString(token) +
            "&mode=SELECT";

        return new AndroidWebTokenDto(
            token,
            url,
            null,
            DateTime.UtcNow);
    }

'@

$controllerMethod = @'
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

        // Comparar con la lista de orígenes del frontend aprobados.
        // No confiar únicamente en un encabezado Origin proporcionado
        // por el cliente: debe coincidir con configuración del servidor.
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

'@

if ($service.Contains("public async Task<AndroidWebTokenDto> CreateManagedPlayWebTokenAsync(")) {
    throw "El metodo ya existe. Revisar antes de aplicar nuevamente."
}

if ($controller.Contains('[HttpPost("managed-play/web-token")]')) {
    throw "El endpoint ya existe. Revisar antes de aplicar nuevamente."
}

$newService = $service.Replace($serviceMarker, $serviceMethod + $serviceMarker)
$newController = $controller.Replace($controllerMarker, $controllerMethod + $controllerMarker)

# Guardar backups locales, fuera del commit.
Copy-Item $servicePath "$servicePath.aeplay.bak"
Copy-Item $controllerPath "$controllerPath.aeplay.bak"

try {
    Set-Content -LiteralPath $servicePath -Value $newService -Encoding utf8 -NoNewline
    Set-Content -LiteralPath $controllerPath -Value $newController -Encoding utf8 -NoNewline

    dotnet build (Join-Path $root "TitanMDM.slnx") --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "Error compilando backend."
    }

    Write-Host "AE-PLAY BACKEND COMPILADO" -ForegroundColor Green
}
catch {
    Copy-Item "$servicePath.aeplay.bak" $servicePath -Force
    Copy-Item "$controllerPath.aeplay.bak" $controllerPath -Force
    throw
}

git diff --check
git status --short

Write-Host "Backend B1/B2 preparado. No se realizo commit." -ForegroundColor Green
