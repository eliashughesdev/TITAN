using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskOutboundEmailConfiguration
    : IEntityTypeConfiguration<HelpdeskOutboundEmail>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskOutboundEmail> builder)
    {
        builder.ToTable(
            "HelpdeskOutboundEmails");

        builder.HasKey(
            x => x.Id);

        builder.Property(
                x => x.ToEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(
                x => x.Subject)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(
                x => x.Body)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(
                x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(
                x => x.LastError)
            .HasMaxLength(2000);

        /*
         * Un comentario público técnico sólo puede producir
         * un correo saliente.
         */
        builder.HasIndex(
                x => new
                {
                    x.OrganizationId,
                    x.CommentId
                })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.Status,
                x.NextAttemptAtUtc
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.TicketId
            });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x => x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskTicket>()
            .WithMany()
            .HasForeignKey(
                x => x.TicketId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskTicketComment>()
            .WithMany()
            .HasForeignKey(
                x => x.CommentId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}