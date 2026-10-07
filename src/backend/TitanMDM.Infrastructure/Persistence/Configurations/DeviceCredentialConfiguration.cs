using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceCredentialConfiguration
    : IEntityTypeConfiguration<DeviceCredential>
{
    public void Configure(
        EntityTypeBuilder<DeviceCredential> builder)
    {
        builder.ToTable("DeviceCredentials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SecretHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.SecretHash)
            .IsUnique();

        builder.HasIndex(x => x.DeviceId);

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.IsActive
            });

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}