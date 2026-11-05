using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("receptions")]
public class Reception
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("asset_id", TypeName = "nvarchar(36)")]
    public string? AssetId { get; set; }

    [Column("description", TypeName = "nvarchar(200)")]
    public string? Description { get; set; }

    [Column("serial_number", TypeName = "nvarchar(120)")]
    public string? SerialNumber { get; set; }

    [Column("category_id", TypeName = "nvarchar(36)")]
    public string? CategoryId { get; set; }

    [Column("delivered_by", TypeName = "nvarchar(160)")]
    public string? DeliveredBy { get; set; }

    [Column("employee_code", TypeName = "nvarchar(60)")]
    public string? EmployeeCode { get; set; }

    [Column("condition", TypeName = "nvarchar(40)")]
    public string? Condition { get; set; }

    [Column("notes", TypeName = "nvarchar(max)")]
    public string? Notes { get; set; }

    [Column("signature", TypeName = "nvarchar(max)")]
    public string? Signature { get; set; }

    [Column("received_by", TypeName = "nvarchar(160)")]
    public string? ReceivedBy { get; set; }

    [Column("received_by_email", TypeName = "nvarchar(160)")]
    public string? ReceivedByEmail { get; set; }

    [Column("reception_date", TypeName = "nvarchar(40)")]
    public string ReceptionDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
}
