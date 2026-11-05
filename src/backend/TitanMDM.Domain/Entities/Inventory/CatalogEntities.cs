using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TitanMDM.Domain.Entities.Inventory;

/// <summary>Campos comunes de los catalogos ITAM (alta, edicion y baja logica).</summary>
public interface IItamCatalogEntity
{
    string Id { get; set; }
    string Name { get; set; }
    bool? Deleted { get; set; }
    string? DeletedAt { get; set; }
    string CreatedAt { get; set; }
    string? UpdatedAt { get; set; }
}

[Table("categories")]
public class ItamCategory : IItamCatalogEntity
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("type", TypeName = "nvarchar(60)")]
    public string? Type { get; set; }

    [Column("depreciation_years", TypeName = "int")]
    public int? DepreciationYears { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}

[Table("branches")]
public class ItamBranch : IItamCatalogEntity
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("code", TypeName = "nvarchar(40)")]
    public string? Code { get; set; }

    [Column("city", TypeName = "nvarchar(120)")]
    public string? City { get; set; }

    [Column("address", TypeName = "nvarchar(300)")]
    public string? Address { get; set; }

    [Column("phone", TypeName = "nvarchar(40)")]
    public string? Phone { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}

[Table("departments")]
public class ItamDepartment : IItamCatalogEntity
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("code", TypeName = "nvarchar(40)")]
    public string? Code { get; set; }

    [Column("manager", TypeName = "nvarchar(160)")]
    public string? Manager { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}

[Table("manufacturers")]
public class ItamManufacturer : IItamCatalogEntity
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("website", TypeName = "nvarchar(200)")]
    public string? Website { get; set; }

    [Column("support_phone", TypeName = "nvarchar(40)")]
    public string? SupportPhone { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}

[Table("suppliers")]
public class ItamSupplier : IItamCatalogEntity
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name", TypeName = "nvarchar(200)")]
    public string Name { get; set; } = string.Empty;

    [Column("rnc", TypeName = "nvarchar(40)")]
    public string? Rnc { get; set; }

    [Column("contact_name", TypeName = "nvarchar(160)")]
    public string? ContactName { get; set; }

    [Column("phone", TypeName = "nvarchar(40)")]
    public string? Phone { get; set; }

    [Column("email", TypeName = "nvarchar(160)")]
    public string? Email { get; set; }

    [Column("address", TypeName = "nvarchar(300)")]
    public string? Address { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}

[Table("asset_models")]
public class ItamAssetModel : IItamCatalogEntity
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("model_number", TypeName = "nvarchar(120)")]
    public string? ModelNumber { get; set; }

    [Column("manufacturer_id", TypeName = "nvarchar(36)")]
    public string? ManufacturerId { get; set; }

    [Column("category_id", TypeName = "nvarchar(36)")]
    public string? CategoryId { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}

[Table("locations")]
public class ItamLocation : IItamCatalogEntity
{
    [Key]
    [Column("id", TypeName = "nvarchar(36)")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name", TypeName = "nvarchar(160)")]
    public string Name { get; set; } = string.Empty;

    [Column("branch_id", TypeName = "nvarchar(36)")]
    public string? BranchId { get; set; }

    [Column("floor", TypeName = "nvarchar(60)")]
    public string? Floor { get; set; }

    [Column("area", TypeName = "nvarchar(120)")]
    public string? Area { get; set; }

    [Column("deleted", TypeName = "bit")]
    public bool? Deleted { get; set; }

    [Column("deleted_at", TypeName = "nvarchar(40)")]
    public string? DeletedAt { get; set; }

    [Column("created_at", TypeName = "nvarchar(40)")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    [Column("updated_at", TypeName = "nvarchar(40)")]
    public string? UpdatedAt { get; set; }
}
