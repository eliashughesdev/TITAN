using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Catalog;

[Table("manufacturers")]
public class Manufacturer
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("deleted", TypeName = "bit")]
    public bool Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}