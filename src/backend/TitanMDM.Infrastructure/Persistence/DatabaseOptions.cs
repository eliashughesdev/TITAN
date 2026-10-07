namespace TitanMDM.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool AutoMigrate { get; init; } = true;

    public bool SeedOnStartup { get; init; } = true;

    public int CommandTimeoutSeconds { get; init; } = 60;

    public int MigrationTimeoutSeconds { get; init; } = 300;

    public int MaxRetryCount { get; init; } = 5;

    public int MaxRetryDelaySeconds { get; init; } = 10;
}