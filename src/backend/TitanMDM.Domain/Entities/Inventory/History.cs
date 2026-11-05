using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("asset_history")]
public class AssetHistory
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("asset_id", TypeName = "nvarchar(36)")]
    public string? AssetId { get; set; }

    [Column("action", TypeName = "nvarchar(80)")]
    public string? Action { get; set; }

    [Column("detail", TypeName = "nvarchar(max)")]
    public string? Detail { get; set; }

    [Column("before_json", TypeName = "nvarchar(max)")]
    public string? BeforeJson { get; set; }

    [Column("after_json", TypeName = "nvarchar(max)")]
    public string? AfterJson { get; set; }

    [Column("user_email", TypeName = "nvarchar(160)")]
    public string? UserEmail { get; set; }

    [Column("user_name", TypeName = "nvarchar(160)")]
    public string? UserName { get; set; }

    [Column("timestamp", TypeName = "nvarchar(40)")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
}
