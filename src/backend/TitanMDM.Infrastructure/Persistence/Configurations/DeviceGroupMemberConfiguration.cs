using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class DeviceGroupMemberConfiguration
    : IEntityTypeConfiguration<DeviceGroupMember>
{
    public void Configure(
        EntityTypeBuilder<DeviceGroupMember> builder)
    {
        builder.ToTable(
            "DeviceGroupMembers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.GroupId,
                x.DeviceId
            })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.DeviceId
            });

        builder.HasOne<DeviceGroup>()
            .WithMany()
            .HasForeignKey(
                x => x.GroupId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(
                x => x.DeviceId)
            .OnDelete(
                DeleteBehavior.Cascade);
    }
}