using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class GeofenceConfiguration
    : IEntityTypeConfiguration<Geofence>
{
    public void Configure(
        EntityTypeBuilder<Geofence> builder)
    {
        builder.ToTable("Geofences");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Name
            })
            .IsUnique();
    }
}