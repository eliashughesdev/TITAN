using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Automation;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AutomationRuleConfiguration
    : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(
        EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("AutomationRules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.TriggerType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ActionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ConditionsJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.ActionPayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Name
            })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.TriggerType,
                x.IsEnabled
            });
    }
}