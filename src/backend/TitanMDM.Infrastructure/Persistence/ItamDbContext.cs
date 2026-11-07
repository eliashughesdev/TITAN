using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities.Inventory;

namespace TitanMDM.Infrastructure.Persistence;

public sealed class ItamDbContext : DbContext
{
    public ItamDbContext(
        DbContextOptions<ItamDbContext> options)
        : base(options)
    {
    }

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();

    public DbSet<Return> Returns => Set<Return>();

    public DbSet<Reception> Receptions => Set<Reception>();

    public DbSet<InventorySession> InventorySessions => Set<InventorySession>();

    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();

    public DbSet<AssetHistory> AssetHistory => Set<AssetHistory>();

    public DbSet<ItamCategory> Categories => Set<ItamCategory>();

    public DbSet<ItamBranch> Branches => Set<ItamBranch>();

    public DbSet<ItamDepartment> Departments => Set<ItamDepartment>();

    public DbSet<ItamManufacturer> Manufacturers => Set<ItamManufacturer>();

    public DbSet<ItamSupplier> Suppliers => Set<ItamSupplier>();

    public DbSet<ItamAssetModel> AssetModels => Set<ItamAssetModel>();

    public DbSet<ItamLocation> Locations => Set<ItamLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Asset>()
            .HasIndex(x => x.AssetTag)
            .IsUnique();

        modelBuilder.Entity<Employee>()
            .HasIndex(x => x.Code);

        modelBuilder.Entity<Delivery>()
            .HasIndex(x => x.AssetId);

        modelBuilder.Entity<Delivery>()
            .HasIndex(x => x.Rp4dId);

        modelBuilder.Entity<Return>()
            .HasIndex(x => x.AssetId);

        modelBuilder.Entity<InventoryCount>()
            .HasIndex(x => x.SessionId);

        modelBuilder.Entity<AssetHistory>()
            .HasIndex(x => x.AssetId);

        /*
         * FK reales de cisa_ti_itam (FK_hist_asset, FK_deliv_asset,
         * FK_deliv_printer). Sin declararlas, EF no ordena los INSERT y puede
         * grabar el historial antes que el activo nuevo: SQL Server rechaza la
         * fila y el alta de activo respondía 500.
         */
        modelBuilder.Entity<AssetHistory>()
            .HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .HasConstraintName("FK_hist_asset")
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Delivery>()
            .HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .HasConstraintName("FK_deliv_asset")
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Delivery>()
            .HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.Rp4dId)
            .HasConstraintName("FK_deliv_rp4d")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
