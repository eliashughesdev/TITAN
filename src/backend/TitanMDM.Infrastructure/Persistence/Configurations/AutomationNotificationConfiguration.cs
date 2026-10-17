using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Automation;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AutomationNotificationConfiguration
    : IEntityTypeConfiguration<AutomationNotification>
{
    public void Configure(
        EntityTypeBuilder<AutomationNotification> builder)
    {
        builder.ToTable("AutomationNotifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.IsRead,
                x.CreatedAtUtc
            });
    }
}
