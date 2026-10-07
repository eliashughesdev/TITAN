using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Applications;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Applications;

public sealed class ApplicationInventoryService
    : IApplicationInventoryService
{
    private readonly TitanMdmDbContext _dbContext;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApplicationInventoryService(TitanMdmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ProcessInventoryAsync(
        Guid deviceId,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "DeviceId no es válido.");
        }

        if (string.IsNullOrWhiteSpace(resultJson))
        {
            throw new InvalidOperationException(
                "El resultado APP_INVENTORY está vacío.");
        }

        var device = await _dbContext.Devices
            .SingleOrDefaultAsync(
                x => x.Id == deviceId && !x.IsDeleted,
                cancellationToken);

        if (device is null)
        {
            throw new InvalidOperationException(
                "El dispositivo no existe.");
        }

        var payload = DeserializePayload(resultJson);

        var reportedPackages = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var currentApplications = await _dbContext.DeviceApplications
            .Where(x => x.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        var currentByPackage = currentApplications.ToDictionary(
            x => x.PackageName,
            StringComparer.OrdinalIgnoreCase);

        foreach (var app in payload.Applications)
        {
            if (string.IsNullOrWhiteSpace(app.PackageName))
                continue;

            var packageName = app.PackageName.Trim();
            reportedPackages.Add(packageName);

            var firstInstall =
                FromUnixMilliseconds(app.FirstInstallTimeUtc);

            var lastUpdate =
                FromUnixMilliseconds(app.LastUpdateTimeUtc);

            if (currentByPackage.TryGetValue(
                packageName,
                out var existing))
            {
                existing.Synchronize(
                    app.ApplicationName,
                    app.VersionName,
                    app.VersionCode,
                    app.IsSystemApp,
                    app.IsEnabled,
                    firstInstall,
                    lastUpdate,
                    app.InstallerPackageName);

                continue;
            }

            var entity = new DeviceApplication(
                device.OrganizationId,
                device.Id,
                packageName,
                app.ApplicationName,
                app.VersionName,
                app.VersionCode,
                app.IsSystemApp,
                app.IsEnabled,
                firstInstall,
                lastUpdate,
                app.InstallerPackageName);

            _dbContext.DeviceApplications.Add(entity);
        }

        foreach (var existing in currentApplications)
        {
            if (!reportedPackages.Contains(existing.PackageName))
            {
                existing.MarkMissing();
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeviceApplicationDto>>
        GetDeviceApplicationsAsync(
            Guid organizationId,
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        return await (
            from app in _dbContext.DeviceApplications.AsNoTracking()
            join device in _dbContext.Devices.AsNoTracking()
                on app.DeviceId equals device.Id
            where
                app.OrganizationId == organizationId &&
                app.DeviceId == deviceId
            orderby app.IsPresent descending, app.ApplicationName
            select new DeviceApplicationDto(
                app.Id,
                app.DeviceId,
                device.DeviceName,
                app.PackageName,
                app.ApplicationName,
                app.VersionName,
                app.VersionCode,
                app.IsSystemApp,
                app.IsEnabled,
                app.IsPresent,
                app.InstallerPackageName,
                app.FirstInstallTimeUtc,
                app.LastUpdateTimeUtc,
                app.FirstSeenAtUtc,
                app.LastSeenAtUtc)
        ).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ApplicationSummaryDto>>
        GetApplicationsAsync(
            Guid organizationId,
            string? search,
            bool? systemApp,
            CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DeviceApplications
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.IsPresent);

        if (systemApp.HasValue)
        {
            query = query.Where(
                x => x.IsSystemApp == systemApp.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();

            query = query.Where(x =>
                x.ApplicationName.Contains(value) ||
                x.PackageName.Contains(value));
        }

        var rows = await query
            .GroupBy(x => new
            {
                x.PackageName,
                x.ApplicationName,
                x.VersionName,
                x.VersionCode,
                x.IsSystemApp
            })
            .Select(group => new
            {
                group.Key.PackageName,
                group.Key.ApplicationName,
                group.Key.VersionName,
                group.Key.VersionCode,
                group.Key.IsSystemApp,
                DeviceCount = group
                    .Select(x => x.DeviceId)
                    .Distinct()
                    .Count(),
                EnabledCount = group.Count(x => x.IsEnabled),
                LastSeenAtUtc = group.Max(x => x.LastSeenAtUtc)
            })
            .OrderBy(x => x.ApplicationName)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new ApplicationSummaryDto(
            x.PackageName,
            x.ApplicationName,
            x.VersionName,
            x.VersionCode,
            x.IsSystemApp,
            x.DeviceCount,
            x.EnabledCount,
            x.LastSeenAtUtc)).ToArray();
    }

    private static AppInventoryPayload DeserializePayload(
        string resultJson)
    {
        var payload = JsonSerializer.Deserialize<AppInventoryPayload>(
            resultJson,
            JsonOptions);

        return payload ?? throw new InvalidOperationException(
            "APP_INVENTORY contiene JSON inválido.");
    }

    private static DateTime? FromUnixMilliseconds(long? milliseconds)
    {
        if (!milliseconds.HasValue || milliseconds.Value <= 0)
            return null;

        try
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(milliseconds.Value)
                .UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    private sealed class AppInventoryPayload
    {
        public Guid CommandId { get; set; }
        public string CommandType { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public int ApplicationCount { get; set; }
        public List<AppInventoryItem> Applications { get; set; } = [];
    }

    private sealed class AppInventoryItem
    {
        public string PackageName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string? VersionName { get; set; }
        public long VersionCode { get; set; }
        public bool IsSystemApp { get; set; }
        public bool IsEnabled { get; set; }
        public long? FirstInstallTimeUtc { get; set; }
        public long? LastUpdateTimeUtc { get; set; }
        public string? InstallerPackageName { get; set; }
    }
}