using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class GeofenceEventConfiguration
    : IEntityTypeConfiguration<GeofenceEvent>
{
    public void Configure(
        EntityTypeBuilder<GeofenceEvent> builder)
    {
        builder.ToTable("GeofenceEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Latitude)
            .HasPrecision(10, 7);

        builder.Property(x => x.Longitude)
            .HasPrecision(10, 7);

        builder.Property(x => x.DistanceMeters)
            .HasPrecision(18, 4);

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.OccurredAtUtc
            });

        builder.HasIndex(
            x => new
            {
                x.GeofenceId,
                x.DeviceId,
                x.OccurredAtUtc
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