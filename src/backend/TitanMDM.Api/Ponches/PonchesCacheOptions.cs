namespace TitanMDM.Api.Ponches;

public sealed class PonchesCacheOptions
{
    public const string SectionName =
        "Ponches:Cache";

    public int DashboardSeconds { get; init; } =
        20;

    public int DeviceHealthSeconds { get; init; } =
        10;

    public int RecordsSeconds { get; init; } =
        15;

    public int EmployeesSeconds { get; init; } =
        120;

    public int DevicesSeconds { get; init; } =
        30;

    public int CatalogSeconds { get; init; } =
        300;

    public int ReportsSeconds { get; init; } =
        30;
}