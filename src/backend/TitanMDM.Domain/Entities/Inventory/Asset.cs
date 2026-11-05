using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("assets")]
public class Asset
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("asset_tag", TypeName = "nvarchar(60)")]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [Column("name", TypeName = "nvarchar(200)")]
    public string Name { get; set; } = string.Empty;

    [Column("serial_number", TypeName = "nvarchar(120)")]
    public string? SerialNumber { get; set; }

    [Column("category_id", TypeName = "nvarchar(36)")]
    public string? CategoryId { get; set; }

    [Column("manufacturer_id", TypeName = "nvarchar(36)")]
    public string? ManufacturerId { get; set; }

    [Column("model_id", TypeName = "nvarchar(36)")]
    public string? ModelId { get; set; }

    [Column("branch_id", TypeName = "nvarchar(36)")]
    public string? BranchId { get; set; }

    [Column("department_id", TypeName = "nvarchar(36)")]
    public string? DepartmentId { get; set; }

    [Column("location_id", TypeName = "nvarchar(36)")]
    public string? LocationId { get; set; }

    [Column("supplier_id", TypeName = "nvarchar(36)")]
    public string? SupplierId { get; set; }

    [Column("status", TypeName = "nvarchar(40)")]
    public string Status { get; set; } = "disponible";

    [Column("condition", TypeName = "nvarchar(40)")]
    public string Condition { get; set; } = "nuevo";

    [Column("purchase_date", TypeName = "nvarchar(40)")]
    public string? PurchaseDate { get; set; }

    [Column("purchase_cost", TypeName = "decimal(18,2)")]
    public decimal? PurchaseCost { get; set; }

    [Column("warranty_end", TypeName = "nvarchar(40)")]
    public string? WarrantyEnd { get; set; }

    [Column("depreciation_years", TypeName = "int")]
    public int? DepreciationYears { get; set; }

    [Column("mac_address", TypeName = "nvarchar(60)")]
    public string? MacAddress { get; set; }

    [Column("ip_address", TypeName = "nvarchar(60)")]
    public string? IpAddress { get; set; }

    [Column("hostname", TypeName = "nvarchar(120)")]
    public string? Hostname { get; set; }

    [Column("specs", TypeName = "nvarchar(max)")]
    public string? Specs { get; set; }

    [Column("notes", TypeName = "nvarchar(max)")]
    public string? Notes { get; set; }

    [Column("photos", TypeName = "nvarchar(max)")]
    public string? Photos { get; set; }

    [Column("documents", TypeName = "nvarchar(max)")]
    public string? Documents { get; set; }

    [Column("device_id", TypeName = "nvarchar(36)")]
    public string? DeviceId { get; set; }

    [Column("assigned_to", TypeName = "nvarchar(160)")]
    public string? AssignedTo { get; set; }

    [Column("assigned_to_name", TypeName = "nvarchar(160)")]
    public string? AssignedToName { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("created_by", TypeName = "nvarchar(160)")]
    public string? CreatedBy { get; set; }

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }

    [Column("updated_by", TypeName = "nvarchar(160)")]
    public string? UpdatedBy { get; set; }

    [Column("deleted_by", TypeName = "nvarchar(160)")]
    public string? DeletedBy { get; set; }

    // ------------------------------------------------------------
    // Inventario de hardware (agente). Columnas recuperadas por
    // db/sync_inventory_from_mongo.py; la migracion original las perdio.
    // ------------------------------------------------------------

    [Column("manufacturer", TypeName = "nvarchar(200)")]
    public string? HardwareManufacturer { get; set; }

    [Column("model", TypeName = "nvarchar(200)")]
    public string? HardwareModel { get; set; }

    [Column("os_name", TypeName = "nvarchar(200)")]
    public string? OsName { get; set; }

    [Column("processor", TypeName = "nvarchar(200)")]
    public string? Processor { get; set; }

    [Column("ram_gb")]
    public double? RamGb { get; set; }

    [Column("disk_total_gb")]
    public double? DiskTotalGb { get; set; }

    [Column("disk_free_gb")]
    public double? DiskFreeGb { get; set; }

    [Column("last_user", TypeName = "nvarchar(200)")]
    public string? LastUser { get; set; }

    [Column("last_seen", TypeName = "nvarchar(40)")]
    public string? LastSeen { get; set; }

    [Column("last_inventory_at", TypeName = "nvarchar(40)")]
    public string? LastInventoryAt { get; set; }

    [Column("source", TypeName = "nvarchar(60)")]
    public string? Source { get; set; }
}
