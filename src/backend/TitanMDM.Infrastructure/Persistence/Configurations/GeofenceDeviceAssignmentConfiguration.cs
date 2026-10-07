using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class GeofenceDeviceAssignmentConfiguration
    : IEntityTypeConfiguration<GeofenceDeviceAssignment>
{
    public void Configure(
        EntityTypeBuilder<GeofenceDeviceAssignment> builder)
    {
        builder.ToTable(
            "GeofenceDeviceAssignments");

        builder.HasKey(x => x.Id);

        builder.HasIndex(
            x => new
            {
                x.GeofenceId,
                x.DeviceId
            })
            .IsUnique();

        builder.HasOne<Geofence>()
            .WithMany()
            .HasForeignKey(x => x.GeofenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}