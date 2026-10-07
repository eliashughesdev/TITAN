using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Automation;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AutomationExecutionConfiguration
    : IEntityTypeConfiguration<AutomationExecution>
{
    public void Configure(
        EntityTypeBuilder<AutomationExecution> builder)
    {
        builder.ToTable(
            "AutomationExecutions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TriggerType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.TriggerPayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ResultJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.StartedAtUtc
            });

        builder.HasOne<AutomationRule>()
            .WithMany()
            .HasForeignKey(
                x => x.AutomationRuleId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(
                x => x.DeviceId)
            .OnDelete(
                DeleteBehavior.NoAction);
    }
}