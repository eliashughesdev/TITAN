using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class UserScopeGrantConfiguration
    : IEntityTypeConfiguration<UserScopeGrant>
{
    public void Configure(
        EntityTypeBuilder<UserScopeGrant> builder)
    {
        builder.ToTable(
            "UserScopeGrants");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.ScopeType)
            .IsRequired();

        builder.Property(
                x =>
                    x.ScopeId)
            .IsRequired();

        builder.Property(
                x =>
                    x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(
                x =>
                    new
                    {
                        x.UserId,
                        x.ScopeType,
                        x.ScopeId
                    })
            .IsUnique();

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.ScopeType,
                    x.ScopeId
                });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.UserId)
            .OnDelete(
                DeleteBehavior.Cascade);

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
                    x.GrantedByUserId)
            .OnDelete(
                DeleteBehavior.NoAction);
    }
}