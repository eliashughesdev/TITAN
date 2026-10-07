using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class AndroidPolicyPublicationConfiguration
    : IEntityTypeConfiguration<AndroidPolicyPublication>
{
    public void Configure(
        EntityTypeBuilder<AndroidPolicyPublication> builder)
    {
        builder.ToTable(
            "AndroidPolicyPublications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GooglePolicyId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.GooglePolicyName)
            .HasMaxLength(500);

        builder.Property(x => x.CompiledPolicyJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.GoogleResponseJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(200);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.PolicyId,
                x.PolicyVersion
            })
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.GooglePolicyId
            })
            .IsUnique();

        builder.HasIndex(
            x => x.PolicyVersionId)
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.OrganizationId,
                x.Status
            });

        builder.HasOne<Policy>()
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PolicyVersion>()
            .WithMany()
            .HasForeignKey(x => x.PolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}