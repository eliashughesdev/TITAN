using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AndroidEnrollmentConfiguration
    : IEntityTypeConfiguration<AndroidEnrollment>
{
    public void Configure(
        EntityTypeBuilder<AndroidEnrollment> builder)
    {
        builder.ToTable("AndroidEnrollments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Mode)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(
                x => x.GoogleEnrollmentTokenName)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(
                x => x.GoogleEnrollmentTokenName)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.CreatedAtUtc
        });

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.IsRevoked,
            x.ExpiresAtUtc
        });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Policy>()
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}