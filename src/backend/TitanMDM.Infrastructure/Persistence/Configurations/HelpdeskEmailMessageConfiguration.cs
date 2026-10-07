using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskEmailMessageConfiguration
    : IEntityTypeConfiguration<HelpdeskEmailMessage>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskEmailMessage> builder)
    {
        builder.ToTable("HelpdeskEmailMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Mailbox)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.InternetMessageId)
            .HasMaxLength(998)
            .IsRequired();

        builder.Property(x => x.MessageKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ConversationId)
            .HasMaxLength(512);

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.Mailbox,
            x.MessageKey
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.Mailbox,
            x.ConversationId
        });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskTicket>()
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}