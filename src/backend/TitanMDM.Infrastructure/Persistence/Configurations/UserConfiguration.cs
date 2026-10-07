using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration
    : IEntityTypeConfiguration<User>
{
    public void Configure(
        EntityTypeBuilder<User> builder)
    {
        builder.ToTable(
            "Users");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                x =>
                    x.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                x =>
                    x.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(
                x =>
                    x.PasswordHash)
            .HasMaxLength(1000);

        builder.Property(
                x =>
                    x.JobTitle)
            .HasMaxLength(150);

        builder.Property(
                x =>
                    x.IsActive)
            .IsRequired();

        builder.Property(
                x =>
                    x.MfaEnabled)
            .IsRequired();

        builder.Ignore(
            x =>
                x.FullName);

        // ========================================================
        // INDEXES
        // ========================================================

        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.Email
                    })
            .IsUnique();

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.SiteId
                });

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.SiteLocationId
                });

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
        // DEPARTMENT
        // ========================================================

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.DepartmentId)
            .OnDelete(
                DeleteBehavior.SetNull);

        // ========================================================
        // MULTI-SITE
        // ========================================================

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.SiteId)
            .OnDelete(
                DeleteBehavior.NoAction);

        builder.HasOne<SiteLocation>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.SiteLocationId)
            .OnDelete(
                DeleteBehavior.NoAction);
    }
}