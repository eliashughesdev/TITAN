using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AndroidEnterpriseSignupSessionConfiguration
    : IEntityTypeConfiguration<AndroidEnterpriseSignupSession>
{
    public void Configure(
        EntityTypeBuilder<AndroidEnterpriseSignupSession> builder)
    {
        builder.ToTable(
            "AndroidEnterpriseSignupSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.State)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SignupUrlName)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.State)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.CreatedAtUtc
        });

        builder.HasIndex(x => new
        {
            x.ExpiresAtUtc,
            x.CompletedAtUtc
        });

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