using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TitanMDM.Domain.Entities;

namespace TitanMDM.Infrastructure.Persistence.Configurations;

public sealed class HelpdeskTicketConfiguration
    : IEntityTypeConfiguration<HelpdeskTicket>
{
    public void Configure(
        EntityTypeBuilder<HelpdeskTicket> builder)
    {
        builder.ToTable(
            "HelpdeskTickets");

        builder.HasKey(
            x =>
                x.Id);

        builder.Property(
                x =>
                    x.Number)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(
                x =>
                    x.Subject)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(
                x =>
                    x.Description)
            .HasMaxLength(4000);

        builder.Property(
                x =>
                    x.Type)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(
                x =>
                    x.Priority)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(
                x =>
                    x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(
                x =>
                    x.Category)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(
                x =>
                    x.Source)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(
                x =>
                    x.EntraObjectId)
            .HasMaxLength(80);

        builder.Property(
                x =>
                    x.EntraUserPrincipalName)
            .HasMaxLength(320);

        builder.Property(
                x =>
                    x.ExternalRequesterName)
            .HasMaxLength(200);

        builder.Property(
                x =>
                    x.ExternalRequesterEmail)
            .HasMaxLength(320);

        // ========================================================
        // INDEXES
        // ========================================================

        builder.HasIndex(
                x =>
                    new
                    {
                        x.OrganizationId,
                        x.Number
                    })
            .IsUnique();

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.Status
                });

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.SiteId
                });

        builder.HasIndex(
            x =>
                new
                {
                    x.OrganizationId,
                    x.SiteLocationId
                });

        // ========================================================
        // ORGANIZATION
        // ========================================================

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.OrganizationId)
            .OnDelete(
                DeleteBehavior.Restrict);

        // ========================================================
        // REQUESTER
        // ========================================================

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.RequesterUserId)
            .OnDelete(
                DeleteBehavior.Restrict);

        // ========================================================
        // DEVICE
        // ========================================================

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.DeviceId)
            .OnDelete(
                DeleteBehavior.SetNull);

        // ========================================================
        // MULTI-SITE
        //
        // NO ACTION evita múltiples cascade paths en SQL Server.
        // ========================================================

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.SiteId)
            .OnDelete(
                DeleteBehavior.NoAction);

        builder.HasOne<SiteLocation>()
            .WithMany()
            .HasForeignKey(
                x =>
                    x.SiteLocationId)
            .OnDelete(
                DeleteBehavior.NoAction);
    }
}