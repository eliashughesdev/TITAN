using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Android;

public sealed class AndroidEnterpriseService
    : IAndroidEnterpriseService
{
    private readonly TitanMdmDbContext _dbContext;
    private readonly AndroidManagementClient _client;
    private readonly IGoogleAndroidAccessTokenProvider _tokenProvider;
    private readonly AndroidManagementOptions _options;

    public AndroidEnterpriseService(
        TitanMdmDbContext dbContext,
        AndroidManagementClient client,
        IGoogleAndroidAccessTokenProvider tokenProvider,
        IOptions<AndroidManagementOptions> options)
    {
        _dbContext = dbContext;
        _client = client;
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    // ============================================================
    // STATUS
    // ============================================================

    public async Task<AndroidEnterpriseStatusDto>
        GetStatusAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(organizationId);

        var configuration =
            await _dbContext
                .AndroidEnterpriseConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.OrganizationId == organizationId,
                    cancellationToken);

        var canAuthenticate =
            await _tokenProvider
                .CanAuthenticateAsync(cancellationToken);

        return new AndroidEnterpriseStatusDto(
            _options.IsConfigured,
            canAuthenticate,
            _options.HasSignupCallback,
            _options.ProjectId,
            configuration?.EnterpriseName,
            configuration?.EnterpriseDisplayName,
            configuration?.Status.ToString()
                ?? AndroidEnterpriseStatus.NotConfigured.ToString(),
            configuration?.ConnectedAtUtc,
            configuration?.LastError);
    }

    // ============================================================
    // ANDROID ENTERPRISE SIGNUP
    // ============================================================

    public async Task<AndroidSignupResponse>
        CreateSignupUrlAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(organizationId);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "UserId is required.",
                nameof(userId));
        }

        _options.ValidateSignup();

        var configuration =
            await GetOrCreateConfigurationAsync(
                organizationId,
                cancellationToken);

        if (configuration.Status ==
                AndroidEnterpriseStatus.Active &&
            !string.IsNullOrWhiteSpace(
                configuration.EnterpriseName))
        {
            throw new InvalidOperationException(
                "La organización ya está conectada con Android Enterprise.");
        }

        var session =
            new AndroidEnterpriseSignupSession(
                organizationId,
                userId,
                DateTime.UtcNow.AddMinutes(30));

        _dbContext
            .AndroidEnterpriseSignupSessions
            .Add(session);

        configuration.MarkPending();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        try
        {
            var callbackUrl =
                AddQueryParameter(
                    _options.CallbackUrl,
                    "state",
                    session.State);

            var response =
                await _client
                    .CreateSignupUrlAsync(
                        callbackUrl,
                        cancellationToken);

            var signupUrl =
                response["url"]?
                    .GetValue<string>();

            var signupUrlName =
                response["name"]?
                    .GetValue<string>();

            if (string.IsNullOrWhiteSpace(signupUrl) ||
                string.IsNullOrWhiteSpace(signupUrlName))
            {
                throw new InvalidOperationException(
                    "Google no devolvió un SignupUrl válido.");
            }

            session.AttachSignupUrl(
                signupUrlName);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return new AndroidSignupResponse(
                signupUrl,
                session.ExpiresAtUtc);
        }
        catch (Exception exception)
        {
            configuration.MarkError(
                exception.Message);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            throw;
        }
    }

    // ============================================================
    // ANDROID ENTERPRISE CALLBACK
    // ============================================================

    public async Task CompleteSignupAsync(
        string state,
        string enterpriseToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            state);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            enterpriseToken);

        var session =
            await _dbContext
                .AndroidEnterpriseSignupSessions
                .SingleOrDefaultAsync(
                    x => x.State == state,
                    cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException(
                "La sesión de vinculación Android Enterprise no existe.");
        }

        if (session.IsCompleted)
        {
            throw new InvalidOperationException(
                "Esta sesión de vinculación Android Enterprise ya fue utilizada.");
        }

        if (session.IsExpired)
        {
            throw new InvalidOperationException(
                "La sesión de vinculación Android Enterprise expiró.");
        }

        if (string.IsNullOrWhiteSpace(
                session.SignupUrlName))
        {
            throw new InvalidOperationException(
                "La sesión de vinculación no contiene un SignupUrl válido.");
        }

        var configuration =
            await GetOrCreateConfigurationAsync(
                session.OrganizationId,
                cancellationToken);

        try
        {
            var enterprise =
                await _client
                    .CreateEnterpriseAsync(
                        enterpriseToken,
                        session.SignupUrlName,
                        "TitanMDM Enterprise",
                        cancellationToken);

            var enterpriseName =
                enterprise["name"]?
                    .GetValue<string>();

            var enterpriseDisplayName =
                enterprise["enterpriseDisplayName"]?
                    .GetValue<string>();

            if (string.IsNullOrWhiteSpace(
                    enterpriseName))
            {
                throw new InvalidOperationException(
                    "Google no devolvió el identificador de Android Enterprise.");
            }

            configuration.Activate(
                enterpriseName,
                enterpriseDisplayName);

            session.Complete();

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (Exception exception)
        {
            configuration.MarkError(
                exception.Message);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            throw;
        }
    }

    // ============================================================
    // LIST ENROLLMENTS
    // ============================================================

    public async Task<
        IReadOnlyCollection<AndroidEnrollmentDto>>
        GetEnrollmentsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        return await _dbContext
            .AndroidEnrollments
            .AsNoTracking()
            .Where(
                x => x.OrganizationId ==
                     organizationId)
            .OrderByDescending(
                x => x.CreatedAtUtc)
            .Select(
                x => new AndroidEnrollmentDto(
                    x.Id,
                    x.Mode.ToString(),
                    x.GoogleEnrollmentTokenName,
                    x.PolicyId,
                    x.ExpiresAtUtc,
                    x.CreatedAtUtc,
                    x.RevokedAtUtc,
                    x.IsRevoked,
                    x.ExpiresAtUtc <= DateTime.UtcNow))
            .ToListAsync(
                cancellationToken);
    }

    // ============================================================
    // CREATE ENROLLMENT
    // ============================================================

    public async Task<CreatedAndroidEnrollmentDto>
        CreateEnrollmentAsync(
            Guid organizationId,
            Guid userId,
            CreateAndroidEnrollmentRequest request,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        ArgumentNullException.ThrowIfNull(
            request);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "UserId is required.",
                nameof(userId));
        }

        if (!_options.EnableEnrollment)
        {
            throw new InvalidOperationException(
                "Android enrollment is disabled.");
        }

        if (!Enum.TryParse<AndroidEnrollmentMode>(
                request.Mode,
                true,
                out var mode))
        {
            throw new ArgumentException(
                "Modo Android inválido.",
                nameof(request.Mode));
        }

        if (request.ExpirationMinutes <= 0 ||
            request.ExpirationMinutes >
            _options.MaximumEnrollmentTokenLifetimeMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ExpirationMinutes),
                $"La vigencia debe estar entre 1 y {_options.MaximumEnrollmentTokenLifetimeMinutes} minutos.");
        }

        var configuration =
            await _dbContext
                .AndroidEnterpriseConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.OrganizationId ==
                         organizationId,
                    cancellationToken);

        if (configuration is null ||
            configuration.Status !=
                AndroidEnterpriseStatus.Active ||
            string.IsNullOrWhiteSpace(
                configuration.EnterpriseName))
        {
            throw new InvalidOperationException(
                "Android Enterprise todavía no está conectado.");
        }

        var googlePolicyName =
            await ValidateEnrollmentPolicyAsync(
            organizationId,
            configuration.EnterpriseName,
            mode,
            request.PolicyId,
            cancellationToken);

        var expiresAtUtc =
            DateTime.UtcNow.AddMinutes(
                request.ExpirationMinutes);

        JsonObject body = new()
        {
            ["duration"] =
                $"{request.ExpirationMinutes * 60}s"
        };

        if (!string.IsNullOrWhiteSpace(
                googlePolicyName))
        {
            body["policyName"] =
                googlePolicyName;
        }

        switch (mode)
        {
            case AndroidEnrollmentMode.FullyManaged:
            case AndroidEnrollmentMode.Dedicated:
                body["allowPersonalUsage"] =
                    "PERSONAL_USAGE_DISALLOWED";
                break;

            case AndroidEnrollmentMode.WorkProfile:
                body["allowPersonalUsage"] =
                    "PERSONAL_USAGE_ALLOWED";
                break;

            default:
                throw new InvalidOperationException(
                    "Modo Android no soportado.");
        }

        var googleToken =
            await _client
                .CreateEnrollmentTokenAsync(
                    configuration.EnterpriseName,
                    body,
                    cancellationToken);

        var googleName =
            googleToken["name"]?
                .GetValue<string>();

        var enrollmentToken =
            googleToken["value"]?
                .GetValue<string>();

        var qrCode =
            googleToken["qrCode"]?
                .GetValue<string>();

        var expirationTimestamp =
            googleToken["expirationTimestamp"]?
                .GetValue<string>();

        if (string.IsNullOrWhiteSpace(
                googleName) ||
            string.IsNullOrWhiteSpace(
                enrollmentToken) ||
            string.IsNullOrWhiteSpace(
                qrCode))
        {
            throw new InvalidOperationException(
                "Google devolvió un EnrollmentToken incompleto.");
        }

        if (!string.IsNullOrWhiteSpace(
                expirationTimestamp) &&
            DateTime.TryParse(
                expirationTimestamp,
                out var googleExpiration))
        {
            expiresAtUtc =
                googleExpiration.ToUniversalTime();
        }

        var enrollment =
            new AndroidEnrollment(
                organizationId,
                mode,
                googleName,
                expiresAtUtc,
                userId,
                request.PolicyId);

        _dbContext
            .AndroidEnrollments
            .Add(enrollment);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return new CreatedAndroidEnrollmentDto(
            enrollment.Id,
            enrollment.Mode.ToString(),
            googleName,
            enrollmentToken,
            qrCode,
            enrollment.PolicyId,
            enrollment.ExpiresAtUtc,
            enrollment.CreatedAtUtc);
    }

    // ============================================================
    // REVOKE ENROLLMENT
    // ============================================================

    public async Task<bool>
        RevokeEnrollmentAsync(
            Guid organizationId,
            Guid enrollmentId,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        if (enrollmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "EnrollmentId is required.",
                nameof(enrollmentId));
        }

        var enrollment =
            await _dbContext
                .AndroidEnrollments
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == enrollmentId &&
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken);

        if (enrollment is null)
            return false;

        if (enrollment.IsRevoked)
            return true;

        var configuration =
            await _dbContext
                .AndroidEnterpriseConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken);

        if (configuration is null ||
            string.IsNullOrWhiteSpace(
                configuration.EnterpriseName))
        {
            throw new InvalidOperationException(
                "Android Enterprise no está configurado.");
        }

        var tokenId =
            enrollment
                .GoogleEnrollmentTokenName
                .Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries)
                .Last();

        await _client
            .DeleteEnrollmentTokenAsync(
                configuration.EnterpriseName,
                tokenId,
                cancellationToken);

        enrollment.Revoke();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    // ============================================================
    // ANDROID AUDIT QUERY
    // ============================================================

    /*
     * Los eventos administrativos de Android Enterprise son los
     * escritos por RbacAuditFilter (RBAC.Android*) y por las
     * acciones remotas (Android.RemoteAction*).
     *
     * La consulta nunca sale de la organización autenticada y se
     * pagina en servidor para no cargar flotillas completas.
     */
    public async Task<AndroidAuditPageDto>
        GetAuditPageAsync(
            Guid organizationId,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        var normalizedPage =
            Math.Max(page, 1);

        var normalizedPageSize =
            Math.Clamp(pageSize, 1, 100);

        var query =
            _dbContext
                .AdministrativeAuditEvents
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId &&
                        (x.Action.StartsWith(
                             "Android.") ||
                         x.Action.StartsWith(
                             "RBAC.Android")));

        var term =
            search?.Trim();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query =
                query.Where(
                    x =>
                        x.Action.Contains(term) ||
                        x.TargetType.Contains(term) ||
                        (x.TargetId != null &&
                         x.TargetId.Contains(term)) ||
                        x.Result.Contains(term));
        }

        var totalCount =
            await query
                .CountAsync(
                    cancellationToken);

        var rows =
            await query
                .OrderByDescending(
                    x =>
                        x.CreatedAtUtc)
                .Skip(
                    (normalizedPage - 1) *
                    normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync(
                    cancellationToken);

        var actors =
            await LoadAuditActorsAsync(
                rows,
                cancellationToken);

        var items =
            rows
                .Select(
                    x =>
                    {
                        var actor =
                            x.ActorUserId.HasValue &&
                            actors.TryGetValue(
                                x.ActorUserId.Value,
                                out var resolved)
                                ? resolved
                                : (
                                    Name:
                                        "Usuario desconocido",
                                    Email: (string?)null);

                        return new AndroidAuditEventDto(
                            x.Id,
                            x.Action,
                            x.TargetType,
                            x.TargetId,
                            x.Result,
                            actor.Name,
                            actor.Email,
                            x.CorrelationId,
                            x.IpAddress,
                            x.CreatedAtUtc);
                    })
                .ToList();

        return new AndroidAuditPageDto(
            items,
            totalCount,
            normalizedPage,
            normalizedPageSize,
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)normalizedPageSize));
    }

    private async Task<
        IReadOnlyDictionary<
            Guid,
            (string Name, string? Email)>>
        LoadAuditActorsAsync(
        IReadOnlyCollection<
            AdministrativeAuditEvent>
            rows,
            CancellationToken cancellationToken)
    {
        var actorIds =
            rows
                .Select(x => x.ActorUserId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray();

        var result =
            new Dictionary<
                Guid,
                (string Name, string? Email)>();

        if (actorIds.Length == 0)
        {
            return result;
        }

        var users =
            await _dbContext
                .Users
                .AsNoTracking()
                .Where(
                    x =>
                        actorIds.Contains(x.Id))
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.FirstName,
                            x.LastName,
                            x.Email
                        })
                .ToListAsync(
                    cancellationToken);

        foreach (var user in users)
        {
            result[user.Id] =
                (
                    string.Join(
                        ' ',
                        new[]
                        {
                            user.FirstName,
                            user.LastName
                        }
                        .Where(
                            part =>
                                !string.IsNullOrWhiteSpace(
                                    part)))
                        .Trim(),
                    user.Email);
        }

        return result;
    }

    // ============================================================
    // ENROLLMENT POLICY VALIDATION
    // ============================================================

    private async Task<string?> ValidateEnrollmentPolicyAsync(
        Guid organizationId,
        string enterpriseName,
        AndroidEnrollmentMode mode,
        Guid? policyId,
        CancellationToken cancellationToken)
    {
        /*
         * Un enrollment token solo puede emitirse para una
         * política Android que pertenezca a la organización y
         * que YA esté publicada en Google Android Management API.
         *
         * Esto evita provisionar dispositivos contra políticas
         * inexistentes, de otra organización, de otra plataforma
         * o de la versión actual sin publicar.
         */
        if (policyId is null ||
            policyId.Value == Guid.Empty)
        {
            if (mode is
                AndroidEnrollmentMode.FullyManaged or
                AndroidEnrollmentMode.Dedicated)
            {
                throw new InvalidOperationException(
                    "Debe seleccionar una política Android publicada para el aprovisionamiento corporativo.");
            }

            return null;
        }

        var policy =
            await _dbContext
                .Policies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId &&
                        x.Id == policyId.Value,
                    cancellationToken);

        if (policy is null)
        {
            throw new InvalidOperationException(
                "La política Android indicada no existe en la organización.");
        }

        if (policy.Platform !=
            PolicyPlatform.Android)
        {
            throw new InvalidOperationException(
                "La política indicada no pertenece a Android.");
        }

        var publication =
            await _dbContext
                .AndroidPolicyPublications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId &&
                        x.PolicyId ==
                            policyId.Value &&
                        x.PolicyVersion ==
                            policy.CurrentVersion,
                    cancellationToken);

        if (publication is null)
        {
            throw new InvalidOperationException(
                "La versión actual de la política todavía no ha sido publicada en Android Enterprise.");
        }

        if (publication.Status !=
            AndroidPolicyPublicationStatus.Published)
        {
            throw new InvalidOperationException(
                $"La publicación de la política no está disponible (estado {publication.Status}).");
        }

        if (string.IsNullOrWhiteSpace(
                publication.GooglePolicyName))
        {
            throw new InvalidOperationException(
                "La publicación Android no contiene el nombre de política de Google.");
        }

        /*
         * La política publicada debe pertenecer a la MISMA
         * empresa Android Enterprise activa de la organización.
         * Google devuelve nombres con el patrón
         *   enterprises/{enterpriseId}/policies/{policyId}
         * por lo que validar el prefijo evita provisionar un
         * dispositivo contra una política de otra empresa.
         */
        var expectedPrefix =
            $"{enterpriseName.TrimEnd('/')}/policies/";

        if (!publication.GooglePolicyName.StartsWith(
                expectedPrefix,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "La política publicada no pertenece a la empresa Android Enterprise activa.");
        }

        return publication.GooglePolicyName;
    }

    // ============================================================
    // CONFIGURATION
    // ============================================================

    private async Task<AndroidEnterpriseConfiguration>
        GetOrCreateConfigurationAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
    {
        var configuration =
            await _dbContext
                .AndroidEnterpriseConfigurations
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId ==
                        organizationId,
                    cancellationToken);

        if (configuration is not null)
            return configuration;

        configuration =
            new AndroidEnterpriseConfiguration(
                organizationId,
                _options.ProjectId);

        _dbContext
            .AndroidEnterpriseConfigurations
            .Add(configuration);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return configuration;
    }

    // ============================================================
    // CALLBACK URL
    // ============================================================

    private static string AddQueryParameter(
        string url,
        string name,
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            url);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            name);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var separator =
            url.Contains(
                '?',
                StringComparison.Ordinal)
                ? "&"
                : "?";

        return
            $"{url}{separator}" +
            $"{Uri.EscapeDataString(name)}=" +
            $"{Uri.EscapeDataString(value)}";
    }

    // ============================================================
    // VALIDATION
    // ============================================================

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
    private static void ValidateOrganization(
        Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));
        }
    }
}
