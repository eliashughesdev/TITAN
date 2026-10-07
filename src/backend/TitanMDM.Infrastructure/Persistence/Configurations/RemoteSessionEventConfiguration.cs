using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class RemoteSessionEventConfiguration
    : IEntityTypeConfiguration<RemoteSessionEvent>
{
    public void Configure(
        EntityTypeBuilder<RemoteSessionEvent> builder)
    {
        builder.ToTable(
            "RemoteSessionEvents");

        builder.HasKey(
            x => x.Id);

        builder.Property(
                x => x.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                x => x.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(
                x => x.MetadataJson)
            .HasColumnType(
                "nvarchar(max)");

        builder.Property(
                x => x.OccurredAtUtc)
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.OccurredAtUtc
            });

        builder.HasIndex(
            x => new
            {
                x.RemoteSessionId,
                x.OccurredAtUtc
            });

        builder.HasOne<RemoteSession>()
            .WithMany()
            .HasForeignKey(
                x => x.RemoteSessionId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(
                x => x.UserId)
            .OnDelete(
                DeleteBehavior.NoAction);
    }
}