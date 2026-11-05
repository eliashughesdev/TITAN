/*
 * Aplica en caliente los permisos de catalogos ITAM sin reiniciar el API.
 * Espejo idempotente de TitanMdmSeeder.EnsurePermissionsAsync +
 * EnsureSuperAdminPermissionsAsync: al reiniciar, el seeder no agrega nada.
 *
 *   sqlcmd -S "DESKTOP-JGB8EAN\SQLEXPRESS" -E -d TitanMDM -i db\seed_inventory_catalog_permissions.sql
 */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetime2 = SYSUTCDATETIME();

DECLARE @defs TABLE (Code nvarchar(150), Name nvarchar(200), Description nvarchar(500));
INSERT INTO @defs VALUES
 (N'inventory.catalogs.view', N'Ver catálogos de inventario',
  N'Permite consultar sucursales, departamentos, ubicaciones, fabricantes, modelos, proveedores y categorías.'),
 (N'inventory.catalogs.manage', N'Gestionar catálogos de inventario',
  N'Permite crear, editar y eliminar sucursales, departamentos, ubicaciones, fabricantes, modelos, proveedores y categorías.');

INSERT INTO Permissions (Id, Code, Name, Module, Description, IsActive, CreatedAtUtc, UpdatedAtUtc)
SELECT NEWID(), d.Code, d.Name, N'Inventory', d.Description, 1, @now, @now
FROM @defs d
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Code = d.Code);

INSERT INTO RolePermissions (RoleId, PermissionId, GrantedAtUtc)
SELECT r.Id, p.Id, @now
FROM Roles r
CROSS JOIN Permissions p
WHERE r.IsSystemRole = 1 AND r.IsActive = 1 AND r.Name = N'SuperAdmin'
  AND p.IsActive = 1 AND p.Code IN (SELECT Code FROM @defs)
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

COMMIT TRANSACTION;

SELECT p.Code, COUNT(rp.RoleId) AS Roles
FROM Permissions p LEFT JOIN RolePermissions rp ON rp.PermissionId = p.Id
WHERE p.Code LIKE N'inventory.catalogs.%'
GROUP BY p.Code;
