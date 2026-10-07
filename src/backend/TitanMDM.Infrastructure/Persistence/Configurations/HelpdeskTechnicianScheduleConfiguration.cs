using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskTechnicianScheduleConfiguration
    : IEntityTypeConfiguration<HelpdeskTechnicianSchedule>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskTechnicianSchedule> builder)
    {
        builder.ToTable("HelpdeskTechnicianSchedules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TimeZoneId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SlotsJson)
            .HasMaxLength(4000)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.TeamId,
            x.UserId
        }).IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HelpdeskTeam>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}