using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AndroidDeviceConfiguration
    : IEntityTypeConfiguration<AndroidDevice>
{
    public void Configure(
        EntityTypeBuilder<AndroidDevice> builder)
    {
        builder.ToTable("AndroidDevices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GoogleDeviceName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.GoogleDeviceId)
            .HasMaxLength(200);

        builder.Property(x => x.ManagementMode)
            .HasMaxLength(100);

        builder.Property(x => x.Ownership)
            .HasMaxLength(100);

        builder.Property(x => x.State)
            .HasMaxLength(100);

        builder.Property(x => x.AppliedPolicyName)
            .HasMaxLength(500);

        builder.Property(x => x.AppliedPolicyState)
            .HasMaxLength(100);

        builder.Property(x => x.EnrollmentTokenName)
            .HasMaxLength(500);

        builder.Property(x => x.UserName)
            .HasMaxLength(500);

        builder.Property(x => x.Brand)
            .HasMaxLength(100);

        builder.Property(x => x.Hardware)
            .HasMaxLength(200);

        builder.Property(x => x.DeviceBasebandVersion)
            .HasMaxLength(200);

        builder.Property(x => x.BootloaderVersion)
            .HasMaxLength(200);

        builder.Property(x => x.SecurityPatchLevel)
            .HasMaxLength(50);

        builder.Property(x => x.BuildNumber)
            .HasMaxLength(200);

        builder.Property(x => x.KernelVersion)
            .HasMaxLength(500);

        builder.Property(x => x.AndroidDevicePolicyVersion)
            .HasMaxLength(100);

        builder.Property(x => x.AndroidDevicePolicyVersionCode)
            .HasMaxLength(100);

        builder.Property(x => x.EncryptionStatus)
            .HasMaxLength(100);

        builder.Property(x => x.SecurityPosture)
            .HasMaxLength(100);

        /*
         * Un Device general solamente puede tener
         * un registro Android Enterprise.
         */
        builder.HasIndex(x => x.DeviceId)
            .IsUnique();

        /*
         * Identidad principal de AMAPI dentro
         * de una organización TitanMDM.
         */
        builder.HasIndex(
                x => new
                {
                    x.OrganizationId,
                    x.GoogleDeviceName
                })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.GoogleDeviceId
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.State
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.ManagementMode
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.IsDeletedInGoogle
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.LastSynchronizedAtUtc
            });

        builder.HasIndex(x =>
            x.LastStatusReportTimeUtc);

        /*
         * AndroidDevice -> Device
         * relación 1:1.
         */
        builder.HasOne<Device>()
            .WithOne()
            .HasForeignKey<AndroidDevice>(
                x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
         * Organización.
         */
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}