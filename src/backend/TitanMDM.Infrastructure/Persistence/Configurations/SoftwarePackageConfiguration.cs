using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class SoftwarePackageConfiguration
    : IEntityTypeConfiguration<SoftwarePackage>
{
    public void Configure(
        EntityTypeBuilder<SoftwarePackage> builder)
    {
        builder.ToTable(
            "SoftwarePackages");

        builder.HasKey(
            x => x.Id);

        builder.Property(
                x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x => x.Version)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(
                x => x.PackageType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(
                x => x.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(
                x => x.StoredFileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(
                x => x.RelativePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(
                x => x.Sha256)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(
                x => x.InstallArguments)
            .HasMaxLength(1000);

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Name,
                x.Version
            });
    }
}