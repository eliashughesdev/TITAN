using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TitanMDM.Application.Location;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Location;

public sealed class LocationCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LocationCleanupBackgroundService> _logger;

    public LocationCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<LocationCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LocationCleanupBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var locationService = scope.ServiceProvider.GetRequiredService<IDeviceLocationService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<TitanMdmDbContext>();

                var organizationIds = await dbContext.Organizations
                    .Select(x => x.Id)
                    .ToListAsync(stoppingToken);

                foreach (var orgId in organizationIds)
                {
                    await locationService.PurgeOldLocationsAsync(orgId, stoppingToken);
                }

                _logger.LogDebug("Location history cleanup completed for all organizations.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during location history cleanup.");
            }

            // Run cleanup once every 24 hours.
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
