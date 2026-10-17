using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Automation;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AutomationQueueItemConfiguration
    : IEntityTypeConfiguration<AutomationQueueItem>
{
    public void Configure(
        EntityTypeBuilder<AutomationQueueItem> builder)
    {
        builder.ToTable("AutomationQueueItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TriggerType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.TriggerPayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.State)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.State,
                x.NextAttemptAtUtc
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.State
            });

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique();
    }
}
