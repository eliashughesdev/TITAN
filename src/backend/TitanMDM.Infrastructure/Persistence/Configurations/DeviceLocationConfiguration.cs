using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceLocationConfiguration
    : IEntityTypeConfiguration<DeviceLocation>
{
    public void Configure(
        EntityTypeBuilder<DeviceLocation> builder)
    {
        builder.ToTable("DeviceLocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source)
            .HasMaxLength(80)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.DeviceId,
                x.CapturedAtUtc
            });

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}