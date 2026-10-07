using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AdministrativeAuditEventConfiguration
    : IEntityTypeConfiguration<
        AdministrativeAuditEvent>
{
    public void Configure(
        EntityTypeBuilder<
            AdministrativeAuditEvent> builder)
    {
        builder.ToTable(
            "AdministrativeAuditEvents");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.Action)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x =>
                    x.TargetType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                x =>
                    x.TargetId)
            .HasMaxLength(200);

        builder.Property(
                x =>
                    x.Result)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(
                x =>
                    x.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                x =>
                    x.IpAddress)
            .HasMaxLength(64);

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.CreatedAtUtc
                });

        builder.HasIndex(
            x =>
                x.ActorUserId);

        builder.HasIndex(
            x =>
                x.CorrelationId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.ActorUserId)
            .OnDelete(
                DeleteBehavior.NoAction);
    }
}