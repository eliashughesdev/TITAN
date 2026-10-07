using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskMailProcessingLogConfiguration
    : IEntityTypeConfiguration<HelpdeskMailProcessingLog>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskMailProcessingLog> builder)
    {
        builder.ToTable(
            "HelpdeskMailProcessingLogs");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.Mailbox)
            .HasMaxLength(
                320)
            .IsRequired();

        builder.Property(
                x =>
                    x.InternetMessageId)
            .HasMaxLength(
                500)
            .IsRequired();

        builder.Property(
                x =>
                    x.ConversationId)
            .HasMaxLength(
                500);

        builder.Property(
                x =>
                    x.FromEmail)
            .HasMaxLength(
                320)
            .IsRequired();

        builder.Property(
                x =>
                    x.Subject)
            .HasMaxLength(
                250);

        builder.Property(
                x =>
                    x.Decision)
            .HasMaxLength(
                30)
            .IsRequired();

        builder.Property(
                x =>
                    x.ReasonCode)
            .HasMaxLength(
                80)
            .IsRequired();

        builder.Property(
                x =>
                    x.Reason)
            .HasMaxLength(
                1000)
            .IsRequired();

        builder.Property(
                x =>
                    x.ProcessedAtUtc)
            .IsRequired();

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.ProcessedAtUtc
                });

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.Decision
                });

        /*
         * Un mensaje solo debe aparecer una vez
         * en el historial de procesamiento.
         */
        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.Mailbox,
                        x.InternetMessageId
                    })
            .IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskTicket>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.TicketId)
            .OnDelete(
                DeleteBehavior.SetNull);
    }
}