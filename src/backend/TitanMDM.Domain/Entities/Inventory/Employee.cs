using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

[Table("employees")]
public class Employee
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("code", TypeName = "nvarchar(60)")]
    public string? Code { get; set; }

    [Required]
    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("department_id", TypeName = "nvarchar(36)")]
    public string? DepartmentId { get; set; }

    [Column("supervisor", TypeName = "nvarchar(160)")]
    public string? Supervisor { get; set; }

    [Column("email", TypeName = "nvarchar(160)")]
    public string? Email { get; set; }

    [Column("phone", TypeName = "nvarchar(40)")]
    public string? Phone { get; set; }

    [Column("position", TypeName = "nvarchar(100)")]
    public string? Position { get; set; }

    [Column("branch_id", TypeName = "nvarchar(36)")]
    public string? BranchId { get; set; }

    [NotMapped]
    public string? Notes { get; set; }

    [Column("is_active", TypeName = "bit")]
    public bool? IsActive { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [NotMapped]
    public string? CreatedBy { get; set; }

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }

    [Column("updated_by", TypeName = "nvarchar(160)")]
    public string? UpdatedBy { get; set; }
}
