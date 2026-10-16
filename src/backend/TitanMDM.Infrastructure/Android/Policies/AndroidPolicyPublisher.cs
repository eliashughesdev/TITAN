using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TitanMDM.Application.Android.Policies;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Android.Policies;

/// <summary>
/// Servicio responsable de compilar, publicar, consultar y verificar
/// políticas Android de TitanMDM contra Google Android Management API.
/// </summary>
public sealed class AndroidPolicyPublisher
    : IAndroidPolicyPublisher
{
    private readonly TitanMdmDbContext _dbContext;
    private readonly IAndroidPolicyCompiler _compiler;
    private readonly AndroidManagementClient _androidManagementClient;
    private readonly AndroidManagementOptions _options;
    private readonly LauncherAssetStore _assets;
    private readonly ILogger<AndroidPolicyPublisher> _logger;

    public AndroidPolicyPublisher(
        TitanMdmDbContext dbContext,
        IAndroidPolicyCompiler compiler,
        AndroidManagementClient androidManagementClient,
        IOptions<AndroidManagementOptions> options,
        LauncherAssetStore assets,
        ILogger<AndroidPolicyPublisher> logger)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));

        _compiler =
            compiler ??
            throw new ArgumentNullException(
                nameof(compiler));

        _androidManagementClient =
            androidManagementClient ??
            throw new ArgumentNullException(
                nameof(androidManagementClient));

        _options =
            options?.Value ??
            throw new ArgumentNullException(
                nameof(options));

        _assets =
            assets ??
            throw new ArgumentNullException(
                nameof(assets));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    // ============================================================
    // PUBLICAR POLÍTICA EN GOOGLE ANDROID MANAGEMENT API
    // ============================================================

    public async Task<AndroidPolicyPublishResult> PublishAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(
            organizationId,
            policyId);

        // --------------------------------------------------------
        // 1. Obtener política TitanMDM
        // --------------------------------------------------------

        var policy =
            await _dbContext.Policies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == policyId &&
                        x.OrganizationId == organizationId,
                    cancellationToken);

        if (policy is null)
        {
            throw new InvalidOperationException(
                "La política TitanMDM no existe.");
        }

        if (policy.Platform != PolicyPlatform.Android)
        {
            throw new InvalidOperationException(
                "Solamente las políticas Android pueden publicarse en Android Management API.");
        }

        // --------------------------------------------------------
        // 2. Obtener versión actual
        // --------------------------------------------------------

        var policyVersion =
            await _dbContext.PolicyVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policy.Id &&
                        x.VersionNumber == policy.CurrentVersion,
                    cancellationToken);

        if (policyVersion is null)
        {
            throw new InvalidOperationException(
                $"No existe la versión {policy.CurrentVersion} de la política.");
        }

        // --------------------------------------------------------
        // 3. Validar Android Enterprise
        // --------------------------------------------------------

        var enterprise =
            await GetActiveEnterpriseAsync(
                organizationId,
                cancellationToken);

        // --------------------------------------------------------
        // 4. Compilar TitanMDM JSON -> Google AMAPI Policy JSON
        // --------------------------------------------------------

        var compilation =
            _compiler.Compile(
                policyVersion.ConfigurationJson);

        if (!compilation.IsValid ||
            compilation.GooglePolicy is null)
        {
            var message =
                compilation.Errors.Count == 0
                    ? "La política Android no pudo compilarse."
                    : string.Join(
                        " | ",
                        compilation.Errors);

            throw new InvalidOperationException(
                message);
        }

        // --------------------------------------------------------
        // 5. Generar Google Policy ID estable por versión
        // --------------------------------------------------------

        /*
         * Cada versión TitanMDM obtiene una Policy Google propia.
         *
         * Ejemplo:
         *
         * Policy TitanMDM
         *      |
         *      +-- Version 1
         *      |     |
         *      |     +-- titanmdm-{guid}-v1
         *      |
         *      +-- Version 2
         *            |
         *            +-- titanmdm-{guid}-v2
         *
         * Esto evita modificar silenciosamente una versión anterior
         * y mantiene trazabilidad completa.
         */

        var googlePolicyId =
            BuildGooglePolicyId(
                policy.Id,
                policyVersion.VersionNumber);

        // --------------------------------------------------------
        // 6. Buscar publicación existente
        // --------------------------------------------------------

        var publication =
            await _dbContext
                .AndroidPolicyPublications
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyVersionId == policyVersion.Id,
                    cancellationToken);

        // --------------------------------------------------------
        // 7. Crear publicación si no existe
        // --------------------------------------------------------

        if (publication is null)
        {
            publication =
                new AndroidPolicyPublication(
                    organizationId,
                    policy.Id,
                    policyVersion.Id,
                    policyVersion.VersionNumber,
                    googlePolicyId);

            _dbContext
                .AndroidPolicyPublications
                .Add(publication);

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        else if (!string.Equals(
                     publication.GooglePolicyId,
                     googlePolicyId,
                     StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La publicación existente utiliza un GooglePolicyId diferente al esperado.");
        }

        // --------------------------------------------------------
        // 8. Sellar identidad de publicación en managed config
        //    Google aceptar el upsert no marca la política aplicada.
        // --------------------------------------------------------

        var googlePolicy =
            compilation.GooglePolicy
            ?? throw new InvalidOperationException(
                "La política Android compilada no contiene payload.");

        AndroidPolicyCompiler.StampLauncherPublication(
            googlePolicy,
            policy.Id,
            publication.Id,
            policyVersion.VersionNumber,
            policyVersion.CreatedAtUtc);

        ResolveLauncherAssets(organizationId, googlePolicy);

        await EnsureLauncherResourcesAuthorizedAsync(
            organizationId,
            googlePolicy,
            cancellationToken);

        var compiledPolicyJson =
            googlePolicy.ToJsonString();

        publication.BeginPublication(
            compiledPolicyJson);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        // --------------------------------------------------------
        // 9. Publicar realmente en Google AMAPI
        // --------------------------------------------------------

        try
        {
            var googleResponse =
                await _androidManagementClient
                    .UpsertPolicyAsync(
                        enterprise.EnterpriseName!,
                        googlePolicyId,
                        googlePolicy,
                        updateMask: null,
                        cancellationToken);

            // ----------------------------------------------------
            // 10. Obtener nombre del recurso Google
            // ----------------------------------------------------

            var googlePolicyName =
                GetJsonString(
                    googleResponse,
                    "name");

            if (string.IsNullOrWhiteSpace(
                    googlePolicyName))
            {
                googlePolicyName =
                    $"{enterprise.EnterpriseName}/policies/{googlePolicyId}";
            }

            // ----------------------------------------------------
            // 11. Registrar publicación exitosa
            // ----------------------------------------------------

            publication.MarkPublished(
                googlePolicyName,
                googleResponse.ToJsonString());

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Android policy published successfully. " +
                "OrganizationId={OrganizationId}, " +
                "PolicyId={PolicyId}, " +
                "PolicyVersion={PolicyVersion}, " +
                "GooglePolicyName={GooglePolicyName}",
                organizationId,
                policy.Id,
                policyVersion.VersionNumber,
                googlePolicyName);

            return new AndroidPolicyPublishResult(
                policy.Id,
                policyVersion.Id,
                policyVersion.VersionNumber,
                publication.Id,
                publication.GooglePolicyId,
                googlePolicyName,
                publication.Status.ToString(),
                compilation.Warnings,
                publication.PublishedAtUtc ??
                    DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            // ----------------------------------------------------
            // 12. Registrar error de publicación
            // ----------------------------------------------------

            publication.MarkFailed(
                "GOOGLE_AMAPI_POLICY_PUBLISH_FAILED",
                exception.Message);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            _logger.LogError(
                exception,
                "Android policy publication failed. " +
                "OrganizationId={OrganizationId}, " +
                "PolicyId={PolicyId}, " +
                "PolicyVersion={PolicyVersion}",
                organizationId,
                policy.Id,
                policyVersion.VersionNumber);

            throw;
        }
    }

    // ============================================================
    // CONSULTAR PUBLICACIÓN ACTUAL EN TITANMDM
    // ============================================================

    public async Task<AndroidPolicyPublicationDto?>
        GetCurrentPublicationAsync(
            Guid organizationId,
            Guid policyId,
            CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty ||
            policyId == Guid.Empty)
        {
            return null;
        }

        // --------------------------------------------------------
        // Obtener política actual
        // --------------------------------------------------------

        var policy =
            await _dbContext.Policies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == policyId,
                    cancellationToken);

        if (policy is null)
        {
            return null;
        }

        if (policy.Platform != PolicyPlatform.Android)
        {
            return null;
        }

        // --------------------------------------------------------
        // Buscar publicación correspondiente a CurrentVersion
        // --------------------------------------------------------

        var publication =
            await _dbContext
                .AndroidPolicyPublications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policyId &&
                        x.PolicyVersion == policy.CurrentVersion,
                    cancellationToken);

        if (publication is null)
        {
            return null;
        }

        return MapPublication(
            publication);
    }

    // ============================================================
    // VERIFICAR POLÍTICA DIRECTAMENTE CONTRA GOOGLE AMAPI
    // ============================================================

    public async Task<AndroidPolicyRemoteVerificationResult>
        VerifyRemoteAsync(
            Guid organizationId,
            Guid policyId,
            CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(
            organizationId,
            policyId);

        // --------------------------------------------------------
        // 1. Obtener política TitanMDM
        // --------------------------------------------------------

        var policy =
            await _dbContext.Policies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Id == policyId,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "La política TitanMDM no existe.");

        if (policy.Platform != PolicyPlatform.Android)
        {
            throw new InvalidOperationException(
                "La política especificada no pertenece a Android.");
        }

        // --------------------------------------------------------
        // 2. Obtener publicación de la versión actual
        // --------------------------------------------------------

        var publication =
            await _dbContext
                .AndroidPolicyPublications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.PolicyId == policyId &&
                        x.PolicyVersion == policy.CurrentVersion,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "La versión actual todavía no ha sido publicada en Android Enterprise.");

        if (publication.Status !=
            AndroidPolicyPublicationStatus.Published)
        {
            throw new InvalidOperationException(
                $"La publicación se encuentra en estado {publication.Status}.");
        }

        // --------------------------------------------------------
        // 3. Obtener Enterprise
        // --------------------------------------------------------

        var enterprise =
            await GetActiveEnterpriseAsync(
                organizationId,
                cancellationToken);

        // --------------------------------------------------------
        // 4. Consultar Google AMAPI realmente
        // --------------------------------------------------------

        var remote =
            await _androidManagementClient
                .GetPolicyAsync(
                    enterprise.EnterpriseName!,
                    publication.GooglePolicyId,
                    cancellationToken);

        // --------------------------------------------------------
        // 5. Obtener nombre Google
        // --------------------------------------------------------

        var googlePolicyName =
            GetJsonString(
                remote,
                "name");

        if (string.IsNullOrWhiteSpace(
                googlePolicyName))
        {
            googlePolicyName =
                publication.GooglePolicyName;

            if (string.IsNullOrWhiteSpace(
                    googlePolicyName))
            {
                googlePolicyName =
                    $"{enterprise.EnterpriseName}/policies/{publication.GooglePolicyId}";
            }
        }

        _logger.LogInformation(
            "Android policy verified against Google AMAPI. " +
            "OrganizationId={OrganizationId}, " +
            "PolicyId={PolicyId}, " +
            "PolicyVersion={PolicyVersion}, " +
            "GooglePolicyName={GooglePolicyName}",
            organizationId,
            policy.Id,
            policy.CurrentVersion,
            googlePolicyName);

        return new AndroidPolicyRemoteVerificationResult(
            policy.Id,
            policy.CurrentVersion,
            publication.GooglePolicyId,
            googlePolicyName,
            true,
            remote.ToJsonString());
    }

    // ============================================================
    // OBTENER ANDROID ENTERPRISE ACTIVO
    // ============================================================

    private void ResolveLauncherAssets(
        Guid organizationId,
        JsonNode googlePolicy)
    {
        if (googlePolicy is not JsonObject policy ||
            policy["applications"] is not JsonArray applications)
        {
            return;
        }

        foreach (var configuration in applications
                     .OfType<JsonObject>()
                     .Select(application => application["managedConfiguration"] as JsonObject)
                     .Where(configuration => configuration is not null))
        {
            ResolveAssetKey(organizationId, configuration!, "titan_wallpaper_url");
            ResolveAssetKey(organizationId, configuration!, "titan_logo_url");
        }
    }

    private void ResolveAssetKey(
        Guid organizationId,
        JsonObject configuration,
        string key)
    {
        var raw = configuration[key]?.GetValue<string>();
        var token = LauncherAssetStore.AssetToken(raw);
        if (token is null)
        {
            return;
        }

        if (!Guid.TryParse(token, out var assetId))
        {
            throw new InvalidOperationException("El recurso del launcher no es válido.");
        }

        var asset = _assets.Find(organizationId, assetId)
            ?? throw new InvalidOperationException(
                "El recurso del launcher no pertenece a esta organización.");

        if (!Uri.TryCreate(_options.LauncherAssetBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "LauncherAssetBaseUrl debe ser HTTPS público para distribuir el recurso.");
        }

        var download = new Uri(
            baseUri,
            $"api/device/launcher-assets/{asset.Id:D}.{asset.Format}");
        if (!LauncherResourceUrlRules.TryValidate(download.ToString(), out var normalized, out var error))
        {
            throw new InvalidOperationException(error ?? "La URL de distribución del recurso no es válida.");
        }

        configuration[key] = normalized;
    }

    private async Task EnsureLauncherResourcesAuthorizedAsync(
        Guid organizationId,
        JsonNode googlePolicy,
        CancellationToken cancellationToken)
    {
        var urls =
            AndroidPolicyCompiler.ReadLauncherResourceUrls(
                googlePolicy);

        if (urls.Count == 0)
        {
            return;
        }

        var organization =
            await _dbContext.Organizations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == organizationId,
                    cancellationToken);

        var authorizedHosts =
            _options.LauncherResourceHosts
                .Append(organization?.PrimaryDomain ?? string.Empty)
                .ToArray();

        if (authorizedHosts.All(string.IsNullOrWhiteSpace))
        {
            _logger.LogWarning(
                "Launcher resources published without a corporate host allowlist. OrganizationId={OrganizationId}",
                organizationId);
            return;
        }

        foreach (var url in urls)
        {
            if (!LauncherResourceUrlRules.HostIsAuthorized(
                    url,
                    authorizedHosts))
            {
                throw new InvalidOperationException(
                    "El recurso del launcher no pertenece a un host corporativo autorizado.");
            }
        }
    }

    private async Task<AndroidEnterpriseConfiguration>
        GetActiveEnterpriseAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
    {
        var enterprise =
            await _dbContext
                .AndroidEnterpriseConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == organizationId,
                    cancellationToken);

        if (enterprise is null)
        {
            throw new InvalidOperationException(
                "Android Enterprise no está configurado para esta organización.");
        }

        if (enterprise.Status !=
            AndroidEnterpriseStatus.Active)
        {
            throw new InvalidOperationException(
                $"Android Enterprise no está activo. Estado actual: {enterprise.Status}.");
        }

        if (string.IsNullOrWhiteSpace(
                enterprise.EnterpriseName))
        {
            throw new InvalidOperationException(
                "Android Enterprise está activo pero no contiene EnterpriseName.");
        }

        return enterprise;
    }

    // ============================================================
    // MAPEO ENTITY -> DTO
    // ============================================================

    private static AndroidPolicyPublicationDto
        MapPublication(
            AndroidPolicyPublication publication)
    {
        return new AndroidPolicyPublicationDto(
            publication.Id,
            publication.PolicyId,
            publication.PolicyVersionId,
            publication.PolicyVersion,
            publication.GooglePolicyId,
            publication.GooglePolicyName,
            publication.Status.ToString(),
            publication.CompiledPolicyJson,
            publication.GoogleResponseJson,
            publication.ErrorCode,
            publication.ErrorMessage,
            publication.CreatedAtUtc,
            publication.UpdatedAtUtc,
            publication.PublishedAtUtc,
            publication.LastAttemptAtUtc,
            publication.DeletedAtUtc);
    }

    // ============================================================
    // GENERAR GOOGLE POLICY ID
    // ============================================================

    private static string BuildGooglePolicyId(
        Guid policyId,
        int version)
    {
        if (policyId == Guid.Empty)
        {
            throw new ArgumentException(
                "PolicyId is required.",
                nameof(policyId));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version));
        }

        /*
         * Formato:
         *
         * titanmdm-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx-v1
         */

        return
            $"titanmdm-{policyId:N}-v{version}";
    }

    // ============================================================
    // VALIDACIÓN DE IDENTIFICADORES
    // ============================================================

    private static void ValidateIdentifiers(
        Guid organizationId,
        Guid policyId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "La organización no es válida.");
        }

        if (policyId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "La política no es válida.");
        }
    }

    // ============================================================
    // JSON HELPERS
    // ============================================================

    private static string? GetJsonString(
        JsonNode node,
        string propertyName)
    {
        if (node[propertyName] is not
            JsonValue value)
        {
            return null;
        }

        return value.TryGetValue<string>(
            out var result)
            ? result
            : null;
    }
}
