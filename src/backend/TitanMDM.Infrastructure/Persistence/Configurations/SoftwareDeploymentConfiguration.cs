using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class SoftwareDeploymentConfiguration
    : IEntityTypeConfiguration<SoftwareDeployment>
{
    public void Configure(
        EntityTypeBuilder<SoftwareDeployment> builder)
    {
        builder.ToTable(
            "SoftwareDeployments");

        builder.HasKey(
            x => x.Id);

        builder.Property(
                x => x.TargetType)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(
                x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.PackageId
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.TargetType,
                x.TargetId
            });
    }
}