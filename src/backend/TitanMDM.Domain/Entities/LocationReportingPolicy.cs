using System;

namespace TitanMDM.Domain.Entities;

public sealed class LocationReportingPolicy
{
    private LocationReportingPolicy()
    {
    }

    public LocationReportingPolicy(
        Guid organizationId,
        bool isEnabled = false,
        int reportingIntervalMinutes = 15,
        int retentionDays = 30)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (reportingIntervalMinutes < 1 || reportingIntervalMinutes > 1440)
            throw new ArgumentOutOfRangeException(
                nameof(reportingIntervalMinutes),
                "Interval must be between 1 and 1440 minutes.");

        if (retentionDays < 1 || retentionDays > 365)
            throw new ArgumentOutOfRangeException(
                nameof(retentionDays),
                "Retention must be between 1 and 365 days.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        IsEnabled = isEnabled;
        ReportingIntervalMinutes = reportingIntervalMinutes;
        RetentionDays = retentionDays;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public bool IsEnabled { get; private set; }

    public int ReportingIntervalMinutes { get; private set; }

    public int RetentionDays { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        bool isEnabled,
        int reportingIntervalMinutes,
        int retentionDays)
    {
        if (reportingIntervalMinutes < 1 || reportingIntervalMinutes > 1440)
            throw new ArgumentOutOfRangeException(
                nameof(reportingIntervalMinutes),
                "Interval must be between 1 and 1440 minutes.");

        if (retentionDays < 1 || retentionDays > 365)
            throw new ArgumentOutOfRangeException(
                nameof(retentionDays),
                "Retention must be between 1 and 365 days.");

        IsEnabled = isEnabled;
        ReportingIntervalMinutes = reportingIntervalMinutes;
        RetentionDays = retentionDays;

        UpdatedAtUtc = DateTime.UtcNow;
    }
}
