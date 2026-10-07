using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DevicePolicyAssignmentConfiguration
    : IEntityTypeConfiguration<DevicePolicyAssignment>
{
    public void Configure(
        EntityTypeBuilder<DevicePolicyAssignment> builder)
    {
        builder.ToTable(
            "DevicePolicyAssignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(
                x => new
                {
                    x.PolicyId,
                    x.DeviceId
                })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Status
            });

        builder.HasOne<Policy>()
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DeviceCommand>()
            .WithMany()
            .HasForeignKey(x => x.CommandId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}