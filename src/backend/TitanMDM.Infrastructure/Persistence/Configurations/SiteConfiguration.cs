using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class SiteConfiguration
    : IEntityTypeConfiguration<Site>
{
    public void Configure(
        EntityTypeBuilder<Site> builder)
    {
        builder.ToTable(
            "Sites");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(
                x =>
                    x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x =>
                    x.Description)
            .HasMaxLength(1000);

        builder.Property(
                x =>
                    x.Address)
            .HasMaxLength(500);

        builder.Property(
                x =>
                    x.City)
            .HasMaxLength(150);

        builder.Property(
                x =>
                    x.Province)
            .HasMaxLength(150);

        builder.Property(
                x =>
                    x.Country)
            .HasMaxLength(150);

        builder.Property(
                x =>
                    x.TimeZoneId)
            .HasMaxLength(150);

        builder.Property(
                x =>
                    x.IsActive)
            .IsRequired();

        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.Code
                    })
            .IsUnique();

        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.Name
                    });

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
    }
}