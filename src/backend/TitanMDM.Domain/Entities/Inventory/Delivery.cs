using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("deliveries")]
public class Delivery
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("asset_id", TypeName = "nvarchar(36)")]
    public string? AssetId { get; set; }

    [Column("asset_tag", TypeName = "nvarchar(60)")]
    public string? AssetTag { get; set; }

    [Column("asset_name", TypeName = "nvarchar(200)")]
    public string? AssetName { get; set; }

    [Column("serial_number", TypeName = "nvarchar(120)")]
    public string? SerialNumber { get; set; }

    [Column("assigned_to_name", TypeName = "nvarchar(160)")]
    public string? AssignedToName { get; set; }

    [Column("employee_code", TypeName = "nvarchar(60)")]
    public string? EmployeeCode { get; set; }

    [Column("department_id", TypeName = "nvarchar(36)")]
    public string? DepartmentId { get; set; }

    [Column("branch_id", TypeName = "nvarchar(36)")]
    public string? BranchId { get; set; }

    [Column("expected_return", TypeName = "nvarchar(40)")]
    public string? ExpectedReturn { get; set; }

    [Column("condition", TypeName = "nvarchar(40)")]
    public string? Condition { get; set; }

    [Column("notes", TypeName = "nvarchar(max)")]
    public string? Notes { get; set; }

    [Column("signature", TypeName = "nvarchar(max)")]
    public string? Signature { get; set; }

    [Column("accessories", TypeName = "nvarchar(max)")]
    public string? Accessories { get; set; }

    [Column("delivered_by", TypeName = "nvarchar(160)")]
    public string? DeliveredBy { get; set; }

    [Column("delivered_by_email", TypeName = "nvarchar(160)")]
    public string? DeliveredByEmail { get; set; }

    [Column("delivery_date", TypeName = "nvarchar(40)")]
    public string DeliveryDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("returned", TypeName = "bit")]
    public bool? Returned { get; set; }

    [Column("return_date", TypeName = "nvarchar(40)")]
    public string? ReturnDate { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    // Recuperadas de MongoDB (db/sync_inventory_from_mongo.py).
    [Column("employee_id", TypeName = "nvarchar(36)")]
    public string? EmployeeId { get; set; }

    [Column("mac_address", TypeName = "nvarchar(40)")]
    public string? MacAddress { get; set; }
}
