using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskZoneConfiguration
    : IEntityTypeConfiguration<HelpdeskZone>
{
    public void Configure(EntityTypeBuilder<HelpdeskZone> builder)
    {
        builder.ToTable("HelpdeskZones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(32).IsRequired();

        builder.HasIndex(x => new { x.OrganizationId, x.ParentZoneId, x.Name })
            .IsUnique();

        builder.HasIndex(x => new { x.OrganizationId, x.Type, x.IsActive });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskZone>()
            .WithMany()
            .HasForeignKey(x => x.ParentZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HelpdeskTeamConfiguration
    : IEntityTypeConfiguration<HelpdeskTeam>
{
    public void Configure(EntityTypeBuilder<HelpdeskTeam> builder)
    {
        builder.ToTable("HelpdeskTeams");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => new { x.OrganizationId, x.Name })
            .IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Categories)
            .HasMaxLength(800)
            .HasDefaultValue("")
            .IsRequired();
    }
}

public sealed class HelpdeskTeamZoneConfiguration
    : IEntityTypeConfiguration<HelpdeskTeamZone>
{
    public void Configure(EntityTypeBuilder<HelpdeskTeamZone> builder)
    {
        builder.ToTable("HelpdeskTeamZones");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.OrganizationId, x.TeamId, x.ZoneId })
            .IsUnique();

        builder.HasOne<HelpdeskTeam>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskZone>()
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HelpdeskTeamMemberConfiguration
    : IEntityTypeConfiguration<HelpdeskTeamMember>
{
    public void Configure(EntityTypeBuilder<HelpdeskTeamMember> builder)
    {
        builder.ToTable("HelpdeskTeamMembers");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.OrganizationId, x.TeamId, x.UserId })
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.AcceptsAutomaticAssignments,
            x.IsAvailable
        });

        builder.HasOne<HelpdeskTeam>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HelpdeskUserZoneConfiguration
    : IEntityTypeConfiguration<HelpdeskUserZone>
{
    public void Configure(EntityTypeBuilder<HelpdeskUserZone> builder)
    {
        builder.ToTable("HelpdeskUserZones");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.ZoneId })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskZone>()
            .WithMany()
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HelpdeskAssistantAccessConfiguration
    : IEntityTypeConfiguration<HelpdeskAssistantAccess>
{
    public void Configure(EntityTypeBuilder<HelpdeskAssistantAccess> builder)
    {
        builder.ToTable("HelpdeskAssistantAccess");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.OrganizationId, x.UserId })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

       
    }
}