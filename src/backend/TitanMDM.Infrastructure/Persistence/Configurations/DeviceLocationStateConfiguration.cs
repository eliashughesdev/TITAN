using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceLocationStateConfiguration
    : IEntityTypeConfiguration<DeviceLocationState>
{
    public void Configure(
        EntityTypeBuilder<DeviceLocationState> builder)
    {
        builder.ToTable("DeviceLocationStates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source)
            .HasMaxLength(80);

        builder.HasIndex(
                x => new
                {
                    x.OrganizationId,
                    x.DeviceId
                })
            .IsUnique();

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
