using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AndroidManagedPlayApplicationConfiguration
    : IEntityTypeConfiguration<AndroidManagedPlayApplication>
{
    public void Configure(
        EntityTypeBuilder<AndroidManagedPlayApplication> builder)
    {
        builder.ToTable(
            "AndroidManagedPlayApplications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PackageName)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxPackageNameLength)
            .IsRequired();

        builder.Property(x => x.DisplayTitle)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxDisplayTitleLength);

        builder.Property(x => x.IconUrl)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxIconUrlLength);

        builder.Property(x => x.Description)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxDescriptionLength);

        builder.Property(x => x.LatestVersionName)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxLatestVersionNameLength);

        builder.Property(x => x.ProductId)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxProductIdLength);

        builder.Property(x => x.InstallationType)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxInstallationTypeLength);

        builder.Property(x => x.MetadataError)
            .HasMaxLength(
                AndroidManagedPlayApplication.MaxMetadataErrorLength);

        builder.Property(x => x.Source)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ApprovalState)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.PackageName
            })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.ApprovalState
            });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CreatedByUserId);
    }
}
