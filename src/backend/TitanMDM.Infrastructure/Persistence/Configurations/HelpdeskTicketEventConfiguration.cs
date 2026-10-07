
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskTicketEventConfiguration : IEntityTypeConfiguration<HelpdeskTicketEvent>
{
    public void Configure(EntityTypeBuilder<HelpdeskTicketEvent> builder)
    {
        builder.ToTable("HelpdeskTicketEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.TicketId);
        builder.HasOne<HelpdeskTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
