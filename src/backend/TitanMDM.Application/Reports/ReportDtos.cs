namespace TitanMDM.Application.Reports;

public sealed record FleetOverviewDto(
    int TotalDevices,
    int OnlineDevices,
    int OfflineDevices,
    int ManagedDevices,
    int UnmanagedDevices,
    int WindowsDevices,
    int AndroidDevices,
    int CompliantDevices,
    int NonCompliantDevices,
    int QuarantinedDevices);

public sealed record CommandOverviewDto(
    int TotalLast30Days,
    int Successful,
    int Failed,
    int Timeout,
    int Cancelled,
    int Active,
    decimal SuccessRate);

public sealed record SecurityOverviewDto(
    int EvaluatedDevices,
    decimal AverageComplianceScore,
    int CriticalRiskDevices,
    int HighRiskDevices);

public sealed record ReportBreakdownDto(
    string Label,
    int Value);

public sealed record ReportsOverviewDto(
    DateTime GeneratedAtUtc,
    FleetOverviewDto Fleet,
    CommandOverviewDto Commands,
    SecurityOverviewDto Security,
    IReadOnlyCollection<ReportBreakdownDto>
        OperatingSystems,
    IReadOnlyCollection<ReportBreakdownDto>
        Departments,
    IReadOnlyCollection<ReportBreakdownDto>
        CommandTypes);