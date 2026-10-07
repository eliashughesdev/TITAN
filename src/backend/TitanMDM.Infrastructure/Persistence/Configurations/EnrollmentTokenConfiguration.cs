using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentTokenConfiguration
    : IEntityTypeConfiguration<EnrollmentToken>
{
    public void Configure(
        EntityTypeBuilder<EnrollmentToken> builder)
    {
        builder.ToTable("EnrollmentTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Platform)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.MaxUses)
            .IsRequired();

        builder.Property(x => x.UsedCount)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Status
            });

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Platform
            });

        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.HasIndex(x => x.CreatedByUserId);

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