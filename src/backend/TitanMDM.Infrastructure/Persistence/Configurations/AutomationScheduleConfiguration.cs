using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Automation;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AutomationScheduleConfiguration
    : IEntityTypeConfiguration<AutomationSchedule>
{
    public void Configure(
        EntityTypeBuilder<AutomationSchedule> builder)
    {
        builder.ToTable("AutomationSchedules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Recurrence)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TimeZoneId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.TimeOfDay)
            .HasMaxLength(10);

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.IsEnabled,
                x.NextRunAtUtc
            });

        builder.HasIndex(x => x.NextRunAtUtc);
    }
}
