using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskRequestTemplateConfiguration
    : IEntityTypeConfiguration<HelpdeskRequestTemplate>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskRequestTemplate> builder)
    {
        builder.ToTable("HelpdeskRequestTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.TicketType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.QuestionsJson)
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(x => x.Revision)
            .IsConcurrencyToken();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.IsActive
            });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}