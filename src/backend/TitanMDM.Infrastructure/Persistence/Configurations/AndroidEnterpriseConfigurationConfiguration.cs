using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AndroidEnterpriseConfigurationConfiguration
    : IEntityTypeConfiguration<
        AndroidEnterpriseConfiguration>
{
    public void Configure(
        EntityTypeBuilder<
            AndroidEnterpriseConfiguration> builder)
    {
        builder.ToTable(
            "AndroidEnterpriseConfigurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GoogleProjectId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.EnterpriseName)
            .HasMaxLength(300);

        builder.Property(
                x => x.EnterpriseDisplayName)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.OrganizationId)
            .IsUnique();

        builder.HasIndex(x => x.EnterpriseName)
            .IsUnique()
            .HasFilter(
                "[EnterpriseName] IS NOT NULL");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}