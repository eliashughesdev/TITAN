using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class RemoteSessionConfiguration
    : IEntityTypeConfiguration<RemoteSession>
{
    public void Configure(
        EntityTypeBuilder<RemoteSession> builder)
    {
        builder.ToTable(
            "RemoteSessions");

        builder.HasKey(
            x => x.Id);

        builder.Property(
                x => x.TechnicianName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(
                x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(
                x => x.FailureReason)
            .HasMaxLength(2000);

        builder.Property(
                x => x.TerminationReason)
            .HasMaxLength(1000);

        builder.Property(
                x => x.TerminatedBy)
            .HasMaxLength(200);

        builder.Property(
                x => x.RequestedAtUtc)
            .IsRequired();

        builder.Property(
                x => x.ExpiresAtUtc)
            .IsRequired();

        builder.Property(
                x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Status
            });

        builder.HasIndex(
            x => new
            {
                x.DeviceId,
                x.Status
            });

        builder.HasIndex(
            x => x.RequestedByUserId);

        builder.HasIndex(
            x => x.RequestedAtUtc);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(
                x => x.DeviceId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x => x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(
                x => x.RequestedByUserId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}