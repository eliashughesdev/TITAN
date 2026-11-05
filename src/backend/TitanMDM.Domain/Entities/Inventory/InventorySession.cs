using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("inventory_sessions")]
public class InventorySession
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("branch_id", TypeName = "nvarchar(36)")]
    public string? BranchId { get; set; }

    [Column("department_id", TypeName = "nvarchar(36)")]
    public string? DepartmentId { get; set; }

    [Column("notes", TypeName = "nvarchar(max)")]
    public string? Notes { get; set; }

    [Column("status", TypeName = "nvarchar(40)")]
    public string Status { get; set; } = "open";

    [Column("counted", TypeName = "int")]
    public int? Counted { get; set; }

    [Column("report", TypeName = "nvarchar(max)")]
    public string? Report { get; set; }

    [Column("created_by", TypeName = "nvarchar(160)")]
    public string? CreatedBy { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
}
