using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("returns")]
public class Return
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("delivery_id", TypeName = "nvarchar(36)")]
    public string? DeliveryId { get; set; }

    [Column("asset_id", TypeName = "nvarchar(36)")]
    public string? AssetId { get; set; }

    [Column("asset_tag", TypeName = "nvarchar(60)")]
    public string? AssetTag { get; set; }

    [Column("asset_name", TypeName = "nvarchar(200)")]
    public string? AssetName { get; set; }

    [Column("returned_by", TypeName = "nvarchar(160)")]
    public string? ReturnedBy { get; set; }

    [Column("employee_code", TypeName = "nvarchar(60)")]
    public string? EmployeeCode { get; set; }

    [Column("received_by", TypeName = "nvarchar(160)")]
    public string? ReceivedBy { get; set; }

    [Column("condition", TypeName = "nvarchar(40)")]
    public string? Condition { get; set; }

    [Column("notes", TypeName = "nvarchar(max)")]
    public string? Notes { get; set; }

    [Column("signature", TypeName = "nvarchar(max)")]
    public string? Signature { get; set; }

    [Column("return_date", TypeName = "nvarchar(40)")]
    public string ReturnDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    // Recuperadas de MongoDB (db/sync_inventory_from_mongo.py).
    [Column("employee_id", TypeName = "nvarchar(36)")]
    public string? EmployeeId { get; set; }

    [Column("serial_number", TypeName = "nvarchar(120)")]
    public string? SerialNumber { get; set; }

    [Column("branch_id", TypeName = "nvarchar(36)")]
    public string? BranchId { get; set; }

    [Column("department_id", TypeName = "nvarchar(36)")]
    public string? DepartmentId { get; set; }
}
