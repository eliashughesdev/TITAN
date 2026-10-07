using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskAutomationSettingsConfiguration
    : IEntityTypeConfiguration<HelpdeskAutomationSettings>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskAutomationSettings> builder)
    {
        builder.ToTable(
            "HelpdeskAutomationSettings");

        builder.HasKey(
            x =>
                x.OrganizationId);

        builder.Property(
                x =>
                    x.ClassificationEnabled)
            .IsRequired();

        builder.Property(
                x =>
                    x.EscalationDelayMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.ReopenDays)
            .IsRequired();

        builder.Property(
                x =>
                    x.LowFirstResponseMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.LowResolutionMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.MediumFirstResponseMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.MediumResolutionMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.HighFirstResponseMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.HighResolutionMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.CriticalFirstResponseMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.CriticalResolutionMinutes)
            .IsRequired();

        builder.Property(
                x =>
                    x.PauseSlaWhenWaitingUser)
            .IsRequired();

        builder.Property(
                x =>
                    x.Revision)
            .IsConcurrencyToken();

        builder.Property(
                x =>
                    x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}