using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class RemoteSessionControlLeaseConfiguration
    : IEntityTypeConfiguration<
        RemoteSessionControlLease>
{
    public void Configure(
        EntityTypeBuilder<
            RemoteSessionControlLease> builder)
    {
        builder.ToTable(
            "RemoteSessionControlLeases");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.DisplayName)
            .HasMaxLength(
                200)
            .IsRequired();

        /*
         * Solo puede existir un lease por sesión.
         * Esto evita que dos operadores tengan
         * teclado/mouse al mismo tiempo.
         */
        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.RemoteSessionId
                    })
            .IsUnique();

        builder.HasIndex(
            x =>
                x.ExpiresAtUtc);
    }
}