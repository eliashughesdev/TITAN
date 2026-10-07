using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceGroupConfiguration
    : IEntityTypeConfiguration<DeviceGroup>
{
    public void Configure(
        EntityTypeBuilder<DeviceGroup> builder)
    {
        builder.ToTable("DeviceGroups");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.RuleJson)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Name
            })
            .IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x => x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}