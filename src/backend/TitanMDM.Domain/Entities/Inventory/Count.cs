using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("inventory_counts")]
public class InventoryCount
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("session_id", TypeName = "nvarchar(36)")]
    public string? SessionId { get; set; }

    [Column("asset_tag", TypeName = "nvarchar(60)")]
    public string? AssetTag { get; set; }

    [Column("asset_id", TypeName = "nvarchar(36)")]
    public string? AssetId { get; set; }

    [Column("asset_name", TypeName = "nvarchar(200)")]
    public string? AssetName { get; set; }

    [Column("found", TypeName = "bit")]
    public bool? Found { get; set; }

    [Column("system_status", TypeName = "nvarchar(40)")]
    public string? SystemStatus { get; set; }

    [Column("condition", TypeName = "nvarchar(40)")]
    public string? Condition { get; set; }

    [Column("location_note", TypeName = "nvarchar(300)")]
    public string? LocationNote { get; set; }

    [Column("counted_by", TypeName = "nvarchar(160)")]
    public string? CountedBy { get; set; }

    [Column("counted_at", TypeName = "nvarchar(40)")]
    public string? CountedAt { get; set; }
}
