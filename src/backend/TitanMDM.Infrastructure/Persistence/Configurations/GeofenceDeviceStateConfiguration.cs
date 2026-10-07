using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class GeofenceDeviceStateConfiguration
    : IEntityTypeConfiguration<GeofenceDeviceState>
{
    public void Configure(
        EntityTypeBuilder<GeofenceDeviceState> builder)
    {
        builder.ToTable("GeofenceDeviceStates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DistanceMeters)
            .HasPrecision(18, 4);

        builder.HasIndex(
                x => new
                {
                    x.GeofenceId,
                    x.DeviceId
                })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.DeviceId
            });

        builder.HasOne<Geofence>()
            .WithMany()
            .HasForeignKey(x => x.GeofenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}