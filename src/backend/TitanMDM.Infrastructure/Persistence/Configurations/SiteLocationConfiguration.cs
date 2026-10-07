using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class SiteLocationConfiguration
    : IEntityTypeConfiguration<SiteLocation>
{
    public void Configure(
        EntityTypeBuilder<SiteLocation> builder)
    {
        builder.ToTable(
            "SiteLocations");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x =>
                    x.Description)
            .HasMaxLength(1000);

        builder.HasIndex(
                x =>
                    new
                    {
                        x.SiteId,
                        x.Name
                    })
            .IsUnique();

        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.IsActive
                    });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.SiteId)
            .OnDelete(
                DeleteBehavior.Cascade);
    }
}