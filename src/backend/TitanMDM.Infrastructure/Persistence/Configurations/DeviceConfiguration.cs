using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceConfiguration
    : IEntityTypeConfiguration<Device>
{
    public void Configure(
        EntityTypeBuilder<Device> builder)
    {
        builder.ToTable(
            "Devices");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.DeviceName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x =>
                    x.SerialNumber)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x =>
                    x.Imei)
            .HasMaxLength(50);

        builder.Property(
                x =>
                    x.Manufacturer)
            .HasMaxLength(100);

        builder.Property(
                x =>
                    x.Model)
            .HasMaxLength(150);

        builder.Property(
                x =>
                    x.OperatingSystem)
            .HasMaxLength(100);

        builder.Property(
                x =>
                    x.OperatingSystemVersion)
            .HasMaxLength(100);

        builder.Property(
                x =>
                    x.AgentVersion)
            .HasMaxLength(100);

        builder.Property(
                x =>
                    x.IpAddress)
            .HasMaxLength(64);

        builder.Property(
                x =>
                    x.MacAddress)
            .HasMaxLength(64);

        builder.Property(
                x =>
                    x.AssignedUser)
            .HasMaxLength(320);

        builder.Property(
                x =>
                    x.Department)
            .HasMaxLength(150);

        builder.Property(
                x =>
                    x.Platform)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(
                x =>
                    x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(
                x =>
                    x.ComplianceStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        // ========================================================
        // INDEXES
        // ========================================================

        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.SerialNumber
                    })
            .IsUnique();

        builder.HasIndex(
            x =>
                x.LastSeenAtUtc);

        builder.HasIndex(
            x =>
                x.Status);

        builder.HasIndex(
            x =>
                x.ComplianceStatus);

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
        // SITE
        //
        // IMPORTANTE:
        // SQL Server no debe ejecutar SET NULL/CASCADE aquí.
        // TitanMDM desasigna estos recursos explícitamente.
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