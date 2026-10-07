using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskMailSettingsConfiguration
    : IEntityTypeConfiguration<HelpdeskMailSettings>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskMailSettings> builder)
    {
        builder.ToTable(
            "HelpdeskMailSettings");

        builder.HasKey(
            x =>
                x.OrganizationId);

        builder.Property(
                x =>
                    x.Mailbox)
            .HasMaxLength(
                320);

        builder.Property(
                x =>
                    x.AcceptedRecipients)
            .HasMaxLength(
                4000);

        builder.Property(
                x =>
                    x.BlockedSenders)
            .HasMaxLength(
                4000);

        builder.Property(
                x =>
                    x.BlockedDomains)
            .HasMaxLength(
                4000);

        builder.Property(
                x =>
                    x.AllowedSenders)
            .HasMaxLength(
                4000);

        builder.Property(
                x =>
                    x.AllowedDomains)
            .HasMaxLength(
                4000);

        builder.Property(
                x =>
                    x.IgnoredSubjectPatterns)
            .HasMaxLength(
                4000);

        builder.Property(
                x =>
                    x.InboundEnabled)
            .IsRequired();

        builder.Property(
                x =>
                    x.OutboundEnabled)
            .IsRequired();

        builder.Property(
                x =>
                    x.IgnoreAutomaticMessages)
            .IsRequired()
            .HasDefaultValue(
                true);

        builder.Property(
                x =>
                    x.IgnoreBulkMessages)
            .IsRequired()
            .HasDefaultValue(
                true);

        builder.Property(
                x =>
                    x.IgnoreBounceMessages)
            .IsRequired()
            .HasDefaultValue(
                true);

        builder.Property(
                x =>
                    x.IgnoreNoReplyMessages)
            .IsRequired()
            .HasDefaultValue(
                true);

        builder.Property(
                x =>
                    x.InboundPollSeconds)
            .IsRequired();

        builder.Property(
                x =>
                    x.OutboundPollSeconds)
            .IsRequired();

        builder.Property(
                x =>
                    x.BatchSize)
            .IsRequired();

        builder.Property(
                x =>
                    x.MaxAttempts)
            .IsRequired();

        builder.Property(
                x =>
                    x.LastInboundError)
            .HasMaxLength(
                2000);

        builder.Property(
                x =>
                    x.LastOutboundError)
            .HasMaxLength(
                2000);

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

        builder.HasIndex(
            x =>
                x.ActorUserId);
    }
}