using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class PolicyVersionConfiguration
    : IEntityTypeConfiguration<PolicyVersion>
{
    public void Configure(
        EntityTypeBuilder<PolicyVersion> builder)
    {
        builder.ToTable("PolicyVersions");

        builder.HasKey(x => x.Id);

        builder.Property(
                x => x.ConfigurationJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.HasIndex(
                x => new
                {
                    x.PolicyId,
                    x.VersionNumber
                })
            .IsUnique();

        builder.HasIndex(
            x => x.OrganizationId);

        builder.HasOne<Policy>()
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}