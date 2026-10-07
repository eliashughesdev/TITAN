using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceCommandConfiguration
    : IEntityTypeConfiguration<DeviceCommand>
{
    public void Configure(
        EntityTypeBuilder<DeviceCommand> builder)
    {
        builder.ToTable("DeviceCommands");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CommandType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ResultJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.DeliveryAttempts)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.DeviceId,
                x.Status
            });

        builder.HasIndex(
            x => new
            {
                x.DeviceId,
                x.Status,
                x.CreatedAtUtc
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Status,
                x.CreatedAtUtc
            });

        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.HasIndex(x => x.CreatedByUserId);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}