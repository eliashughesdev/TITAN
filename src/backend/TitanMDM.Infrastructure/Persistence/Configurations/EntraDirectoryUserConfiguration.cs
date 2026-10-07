
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class EntraDirectoryUserConfiguration : IEntityTypeConfiguration<EntraDirectoryUser>
{
    public void Configure(EntityTypeBuilder<EntraDirectoryUser> builder)
    {
        builder.ToTable("EntraDirectoryUsers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntraObjectId).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UserPrincipalName).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Mail).HasMaxLength(320);
        builder.Property(x => x.JobTitle).HasMaxLength(150);
        builder.Property(x => x.Department).HasMaxLength(150);
        builder.HasIndex(x => new { x.OrganizationId, x.EntraObjectId }).IsUnique();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.LinkedTitanUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
