using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class RemoteSessionParticipantConfiguration
    : IEntityTypeConfiguration<
        RemoteSessionParticipant>
{
    public void Configure(
        EntityTypeBuilder<
            RemoteSessionParticipant> builder)
    {
        builder.ToTable(
            "RemoteSessionParticipants");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.DisplayName)
            .HasMaxLength(
                200)
            .IsRequired();

        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.RemoteSessionId,
                        x.UserId
                    })
            .IsUnique();

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.RemoteSessionId,
                    x.IsConnected
                });
    }
}