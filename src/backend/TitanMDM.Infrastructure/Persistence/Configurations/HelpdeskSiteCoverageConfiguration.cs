using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskSiteCoverageConfiguration
    : IEntityTypeConfiguration<HelpdeskSiteCoverage>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskSiteCoverage> builder)
    {
        builder.ToTable(
            "HelpdeskSiteCoverages");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.Category)
            .HasMaxLength(
                80);

        builder.Property(
                x =>
                    x.Priority)
            .IsRequired();

        builder.Property(
                x =>
                    x.IsActive)
            .IsRequired();

        // ========================================================
        // INDEXES
        // ========================================================

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.SiteId,
                    x.SiteLocationId,
                    x.Category
                });

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.TeamId
                });

        /*
         * Un grupo no debe tener duplicada exactamente
         * la misma cobertura.
         */
        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.TeamId,
                        x.SiteId,
                        x.SiteLocationId,
                        x.Category
                    })
            .IsUnique();

        // ========================================================
        // ORGANIZATION
        // ========================================================

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);

        // ========================================================
        // TEAM
        // ========================================================

        builder.HasOne<HelpdeskTeam>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.TeamId)
            .OnDelete(
                DeleteBehavior.Restrict);

        // ========================================================
        // SITE
        //
        // NO ACTION porque TitanMDM utiliza desactivación,
        // no eliminación física de localidades operativas.
        // ========================================================

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.SiteId)
            .OnDelete(
                DeleteBehavior.NoAction);

        // ========================================================
        // SITE LOCATION
        // ========================================================

        builder.HasOne<SiteLocation>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.SiteLocationId)
            .OnDelete(
                DeleteBehavior.NoAction);
    }
}