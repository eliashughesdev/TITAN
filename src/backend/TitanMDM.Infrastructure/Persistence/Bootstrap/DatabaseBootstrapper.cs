using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TitanMDM.Infrastructure.Persistence.Seed;

namespace TitanMDM.Infrastructure.Persistence.Bootstrap;

public sealed class DatabaseBootstrapper
{
    private readonly TitanMdmDbContext _dbContext;
    private readonly TitanMdmSeeder _seeder;
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseBootstrapper> _logger;

    public DatabaseBootstrapper(
        TitanMdmDbContext dbContext,
        TitanMdmSeeder seeder,
        IOptions<DatabaseOptions> options,
        ILogger<DatabaseBootstrapper> logger)
    {
        _dbContext = dbContext;
        _seeder = seeder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task BootstrapAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting TitanMDM database bootstrap.");

        await ValidateDatabaseConnectivityAsync(
            cancellationToken);

        if (_options.AutoMigrate)
        {
            await ApplyMigrationsAsync(
                cancellationToken);
        }
        else
        {
            await ValidateNoPendingMigrationsAsync(
                cancellationToken);
        }

        if (_options.SeedOnStartup)
        {
            await _seeder.SeedAsync(
                cancellationToken);
        }

        _logger.LogInformation(
            "TitanMDM database bootstrap completed successfully.");
    }

    private async Task ValidateDatabaseConnectivityAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var canConnect =
                await _dbContext.Database.CanConnectAsync(
                    cancellationToken);

            if (!canConnect)
            {
                throw new InvalidOperationException(
                    "TitanMDM cannot establish a connection to SQL Server.");
            }
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "TitanMDM could not connect to its database.");

            throw new InvalidOperationException(
                "TitanMDM could not connect to SQL Server. " +
                "Review the configured database endpoint, service account " +
                "and SQL Server availability.",
                exception);
        }
    }

    private async Task ApplyMigrationsAsync(
        CancellationToken cancellationToken)
    {
        var originalTimeout =
            _dbContext.Database.GetCommandTimeout();

        try
        {
            _dbContext.Database.SetCommandTimeout(
                TimeSpan.FromSeconds(
                    Math.Max(
                        30,
                        _options.MigrationTimeoutSeconds)));

            var pendingMigrations =
                (
                    await _dbContext.Database
                        .GetPendingMigrationsAsync(
                            cancellationToken)
                )
                .ToArray();

            if (pendingMigrations.Length == 0)
            {
                _logger.LogInformation(
                    "Database schema is already up to date.");

                return;
            }

            _logger.LogInformation(
                "Applying {MigrationCount} pending database migration(s).",
                pendingMigrations.Length);

            foreach (var migration in pendingMigrations)
            {
                _logger.LogInformation(
                    "Pending migration: {MigrationName}",
                    migration);
            }

            await _dbContext.Database.MigrateAsync(
                cancellationToken);

            _logger.LogInformation(
                "Database migrations applied successfully.");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Database migration failed.");

            throw new InvalidOperationException(
                "TitanMDM could not upgrade the database schema.",
                exception);
        }
        finally
        {
            _dbContext.Database.SetCommandTimeout(
                originalTimeout);
        }
    }

    private async Task ValidateNoPendingMigrationsAsync(
        CancellationToken cancellationToken)
    {
        var pendingMigrations =
            (
                await _dbContext.Database
                    .GetPendingMigrationsAsync(
                        cancellationToken)
            )
            .ToArray();

        if (pendingMigrations.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"TitanMDM detected {pendingMigrations.Length} " +
            "pending database migration(s), but automatic migrations " +
            "are disabled.");
    }
}