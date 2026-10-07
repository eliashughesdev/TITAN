using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class LostModeSessionConfiguration
    : IEntityTypeConfiguration<LostModeSession>
{
    public void Configure(
        EntityTypeBuilder<LostModeSession> builder)
    {
        builder.ToTable("LostModeSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.DeviceId,
                x.Status
            });

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}