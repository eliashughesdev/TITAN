using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Helpdesk;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed class EntraIdDirectoryService : IEntraIdDirectoryService
{
    private readonly TitanMdmDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _secretProtector;

    public EntraIdDirectoryService(
        TitanMdmDbContext db,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _secretProtector = dataProtectionProvider.CreateProtector(
            "TitanMDM.Helpdesk.Entra.ClientSecret.v1");
    }

    public async Task<EntraIdSettingsDto> GetSettingsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(
            organizationId,
            cancellationToken);

        return Map(settings);
    }

    public async Task<EntraIdSettingsDto> SaveSettingsAsync(
        Guid organizationId,
        SaveEntraIdSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(
            organizationId,
            cancellationToken);

        var protectedSecret = string.IsNullOrWhiteSpace(request.ClientSecret)
            ? settings.ClientSecretProtected
            : _secretProtector.Protect(request.ClientSecret.Trim());

        if (request.IsEnabled &&
            string.IsNullOrWhiteSpace(request.ClientSecret) &&
            !string.IsNullOrWhiteSpace(protectedSecret))
        {
            _ = ReadSecret(protectedSecret);
        }

        settings.Configure(
            request.TenantId,
            request.ClientId,
            protectedSecret,
            request.AllowedGroupIds,
            request.SyncRequestersOnly,
            request.IsEnabled);

        await _db.SaveChangesAsync(cancellationToken);
        return Map(settings);
    }

    public async Task<EntraSyncResultDto> SyncDirectoryAsync(
    Guid organizationId,
    CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(
            organizationId,
            cancellationToken);

        if (!settings.IsEnabled)
        {
            throw new InvalidOperationException(
                "Entra ID no está habilitado.");
        }

        if (string.IsNullOrWhiteSpace(settings.TenantId) ||
            string.IsNullOrWhiteSpace(settings.ClientId) ||
            string.IsNullOrWhiteSpace(settings.ClientSecretProtected))
        {
            throw new InvalidOperationException(
                "Falta Tenant ID, Client ID o Client Secret.");
        }

        var token = await RequestTokenAsync(
            settings,
            cancellationToken);

        var graphUsers = await FetchUsersAsync(
            token,
            settings.AllowedGroupIds,
            cancellationToken);

        var existing = await _db.EntraDirectoryUsers
            .Where(x => x.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        var byObjectId = existing.ToDictionary(
            x => x.EntraObjectId,
            StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var updated = 0;
        var linked = 0;

        foreach (var graphUser in graphUsers)
        {
            if (byObjectId.TryGetValue(
                    graphUser.Id,
                    out var current))
            {
                // El vínculo fue asignado desde TitanMDM a este Object ID.
                // Nunca se sustituye por una coincidencia de correo.
                current.Update(
                    graphUser.DisplayName,
                    graphUser.Mail,
                    graphUser.JobTitle,
                    graphUser.Department,
                    current.LinkedTitanUserId,
                    graphUser.AccountEnabled);

                if (current.LinkedTitanUserId.HasValue)
                    linked++;

                updated++;
            }
            else
            {
                var created = new EntraDirectoryUser(
                    organizationId,
                    graphUser.Id,
                    graphUser.DisplayName,
                    graphUser.UserPrincipalName,
                    graphUser.Mail);

                created.Update(
                    graphUser.DisplayName,
                    graphUser.Mail,
                    graphUser.JobTitle,
                    graphUser.Department,
                    linkedTitanUserId: null,
                    graphUser.AccountEnabled);

                _db.EntraDirectoryUsers.Add(created);
                imported++;
            }
        }

        settings.MarkSync(
            $"ok imported={imported} updated={updated}");

        await _db.SaveChangesAsync(cancellationToken);

        return new EntraSyncResultDto(
            imported,
            updated,
            linked,
            settings.LastSyncStatus ?? "ok");
    }

    public async Task<IReadOnlyList<EntraDirectoryUserDto>> SearchDirectoryAsync(
        Guid organizationId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.EntraDirectoryUsers
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.DisplayName.Contains(term) ||
                x.UserPrincipalName.Contains(term) ||
                (x.Mail != null && x.Mail.Contains(term)));
        }

        var rows = await query
            .OrderBy(x => x.DisplayName)
            .Take(50)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new EntraDirectoryUserDto(
            x.Id,
            x.EntraObjectId,
            x.DisplayName,
            x.UserPrincipalName,
            x.Mail,
            x.JobTitle,
            x.Department,
            x.LinkedTitanUserId,
            x.IsActive)).ToList();
    }

    private async Task<EntraIdSettings> GetOrCreateSettingsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var settings = await _db.EntraIdSettings
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId,
                cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new EntraIdSettings(organizationId);
        _db.EntraIdSettings.Add(settings);

        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static EntraIdSettingsDto Map(
        EntraIdSettings settings) =>
        new(
            settings.IsEnabled,
            settings.TenantId,
            settings.ClientId,
            !string.IsNullOrWhiteSpace(
                settings.ClientSecretProtected),
            settings.AllowedGroupIds,
            settings.SyncRequestersOnly,
            settings.LastSyncAtUtc,
            settings.LastSyncStatus);

    private string ReadSecret(string protectedSecret)
    {
        try
        {
            return _secretProtector.Unprotect(
                protectedSecret);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidOperationException(
                "El secreto de Entra no está protegido con la clave actual. " +
                "Guarde nuevamente el Client Secret antes de sincronizar.");
        }
    }

    private async Task<string> RequestTokenAsync(
        EntraIdSettings settings,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(
            "entra-id");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/" +
            $"{settings.TenantId}/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = settings.ClientId!,
                    ["client_secret"] = ReadSecret(
                        settings.ClientSecretProtected!),
                    ["grant_type"] = "client_credentials",
                    ["scope"] =
                        "https://graph.microsoft.com/.default"
                })
        };

        using var response = await client.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Entra rechazó la autenticación " +
                $"(HTTP {(int)response.StatusCode}). " +
                "Revise Tenant ID, Client ID, Client Secret " +
                "y permisos de Graph.");
        }

        var payload = await response.Content
            .ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(payload);

        return doc.RootElement
                   .GetProperty("access_token")
                   .GetString()
               ?? throw new InvalidOperationException(
                   "Token Entra vacío.");
    }

    private async Task<List<GraphUser>> FetchUsersAsync(
        string token,
        string? allowedGroupIds,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(
            "entra-id");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var urls = new List<string>();

        if (string.IsNullOrWhiteSpace(allowedGroupIds))
        {
            urls.Add(
                "https://graph.microsoft.com/v1.0/users" +
                "?$select=id,displayName,userPrincipalName," +
                "mail,jobTitle,department,accountEnabled&$top=999");
        }
        else
        {
            foreach (var groupId in allowedGroupIds.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(groupId, out var parsedGroupId))
                {
                    throw new InvalidOperationException(
                        $"El identificador de grupo '{groupId}' " +
                        "no es un GUID válido.");
                }

                urls.Add(
                    "https://graph.microsoft.com/v1.0/groups/" +
                    $"{parsedGroupId:D}/members/microsoft.graph.user" +
                    "?$select=id,displayName,userPrincipalName," +
                    "mail,jobTitle,department,accountEnabled&$top=999");
            }
        }

        var results = new Dictionary<string, GraphUser>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var startUrl in urls)
        {
            var url = startUrl;

            while (!string.IsNullOrWhiteSpace(url))
            {
                using var response = await client.GetAsync(
                    url,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Graph rechazó la sincronización " +
                        $"(HTTP {(int)response.StatusCode}). " +
                        "Revise los permisos de lectura y los grupos configurados.");
                }

                var payload = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                using var doc = JsonDocument.Parse(payload);

                if (doc.RootElement.TryGetProperty(
                        "value",
                        out var value))
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetString();

                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        results[id] = new GraphUser(
                            id,
                            item.TryGetProperty(
                                "displayName",
                                out var displayName)
                                ? displayName.GetString() ?? id
                                : id,
                            item.TryGetProperty(
                                "userPrincipalName",
                                out var upn)
                                ? upn.GetString() ?? id
                                : id,
                            item.TryGetProperty(
                                "mail",
                                out var mail)
                                ? mail.GetString()
                                : null,
                            item.TryGetProperty(
                                "jobTitle",
                                out var title)
                                ? title.GetString()
                                : null,
                            item.TryGetProperty(
                                "department",
                                out var department)
                                ? department.GetString()
                                : null,
                            !item.TryGetProperty(
                                "accountEnabled",
                                out var enabled) ||
                            enabled.ValueKind != JsonValueKind.False);
                    }
                }

                url = doc.RootElement.TryGetProperty(
                    "@odata.nextLink",
                    out var next)
                    ? next.GetString()
                    : null;
            }
        }

        return results.Values.ToList();
    }

    private sealed record GraphUser(
        string Id,
        string DisplayName,
        string UserPrincipalName,
        string? Mail,
        string? JobTitle,
        string? Department,
        bool AccountEnabled);
}