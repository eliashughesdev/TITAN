
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskTicketCommentConfiguration : IEntityTypeConfiguration<HelpdeskTicketComment>
{
    public void Configure(EntityTypeBuilder<HelpdeskTicketComment> builder)
    {
        builder.ToTable("HelpdeskTicketComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => x.TicketId);
        builder.HasOne<HelpdeskTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ExternalAuthorName)
        .HasMaxLength(200);

        builder.Property(x => x.ExternalAuthorEmail)
            .HasMaxLength(320);
    }
}
