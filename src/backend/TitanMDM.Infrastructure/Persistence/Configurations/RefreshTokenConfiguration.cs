using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration
    : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(
        EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.CreatedByIp)
            .HasMaxLength(64);

        builder.Property(x => x.RevokedByIp)
            .HasMaxLength(64);

        builder.Property(x => x.ReplacedByTokenHash)
            .HasMaxLength(128);

        builder.Ignore(x => x.IsRevoked);
        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsActive);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => x.UserId);

        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}