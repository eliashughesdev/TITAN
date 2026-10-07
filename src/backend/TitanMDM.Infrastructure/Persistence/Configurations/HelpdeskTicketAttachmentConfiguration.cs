using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskTicketAttachmentConfiguration
    : IEntityTypeConfiguration<HelpdeskTicketAttachment>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskTicketAttachment> builder)
    {
        builder.ToTable(
            "HelpdeskTicketAttachments");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.FileName)
            .HasMaxLength(
                180)
            .IsRequired();

        builder.Property(
                x =>
                    x.StorageName)
            .HasMaxLength(
                80)
            .IsRequired();

        builder.Property(
                x =>
                    x.ContentType)
            .HasMaxLength(
                80)
            .IsRequired();

        builder.Property(
                x =>
                    x.SizeBytes)
            .IsRequired();

        builder.Property(
                x =>
                    x.IsInternal)
            .IsRequired();

        builder.Property(
                x =>
                    x.IsInline)
            .IsRequired()
            .HasDefaultValue(
                false);

        builder.Property(
                x =>
                    x.ContentId)
            .HasMaxLength(
                500);

        builder.Property(
                x =>
                    x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.TicketId,
                    x.CreatedAtUtc
                });

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.TicketId,
                    x.IsInline
                });

        builder.HasIndex(
                x =>
                    x.StorageName)
            .IsUnique();

        builder.HasOne<HelpdeskTicket>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.TicketId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}