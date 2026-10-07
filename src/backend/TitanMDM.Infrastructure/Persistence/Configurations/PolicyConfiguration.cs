using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class PolicyConfiguration
    : IEntityTypeConfiguration<Policy>
{
    public void Configure(
        EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Platform)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CurrentVersion)
            .IsRequired();

        builder.HasIndex(
                x => new
                {
                    x.OrganizationId,
                    x.Name
                })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Platform,
                x.Status
            });
    }
}