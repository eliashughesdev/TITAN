using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceApplicationConfiguration
    : IEntityTypeConfiguration<DeviceApplication>
{
    public void Configure(
        EntityTypeBuilder<DeviceApplication> builder)
    {
        builder.ToTable(
            "DeviceApplications");

        builder.HasKey(
            x => x.Id);

        builder.Property(
                x => x.PackageName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(
                x => x.ApplicationName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(
                x => x.VersionName)
            .HasMaxLength(200);

        builder.Property(
                x => x.InstallerPackageName)
            .HasMaxLength(500);

        builder.Property(
                x => x.VersionCode)
            .IsRequired();

        builder.Property(
                x => x.IsSystemApp)
            .IsRequired();

        builder.Property(
                x => x.IsEnabled)
            .IsRequired();

        builder.Property(
                x => x.IsPresent)
            .IsRequired();

        builder.HasIndex(
                x => new
                {
                    x.DeviceId,
                    x.PackageName
                })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.PackageName
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.IsPresent
            });

        builder.HasIndex(
            x => x.ApplicationName);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(
                x => x.DeviceId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x => x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}