
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class EntraIdSettingsConfiguration : IEntityTypeConfiguration<EntraIdSettings>
{
    public void Configure(EntityTypeBuilder<EntraIdSettings> builder)
    {
        builder.ToTable("EntraIdSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).HasMaxLength(80);
        builder.Property(x => x.ClientId).HasMaxLength(80);
        builder.Property(x => x.ClientSecretProtected).HasMaxLength(2000);
        builder.Property(x => x.AllowedGroupIds).HasMaxLength(2000);
        builder.Property(x => x.LastSyncStatus).HasMaxLength(250);
        builder.HasIndex(x => x.OrganizationId).IsUnique();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
