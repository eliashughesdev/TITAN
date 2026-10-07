using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

using TitanMDM.Api.Services;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public sealed class RolesController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly ILogger<
        RolesController> _logger;
    
    private readonly SessionSecurityService
    _sessionSecurity;

    public RolesController(
    TitanMdmDbContext dbContext,
    SessionSecurityService sessionSecurity,
    ILogger<RolesController> logger)
{
    _dbContext =
        dbContext;

    _sessionSecurity =
        sessionSecurity;

    _logger =
        logger;
}

    /*
     * ============================================================
     * GET ROLES
     * ============================================================
     */

    [HttpGet]
    public async Task<IActionResult> GetRoles(
        [FromQuery] bool? active,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.view"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (
            organizationId ==
            Guid.Empty)
        {
            return Unauthorized();
        }

        var query =
            _dbContext.Roles
                .AsNoTracking()
                .Where(
                    role =>
                        role.OrganizationId ==
                        organizationId);

        if (
            active.HasValue)
        {
            query =
                query.Where(
                    role =>
                        role.IsActive ==
                        active.Value);
        }

        var roles =
            await query
                .OrderBy(
                    role =>
                        role.Name)
                .Select(
                    role =>
                        new
                        {
                            id =
                                role.Id,

                            name =
                                role.Name,

                            description =
                                role.Description,

                            isSystemRole =
                                role.IsSystemRole,

                            isActive =
                                role.IsActive,

                            createdAtUtc =
                                role.CreatedAtUtc,

                            updatedAtUtc =
                                role.UpdatedAtUtc,

                            userCount =
                                _dbContext
                                    .UserRoles
                                    .Count(
                                        userRole =>
                                            userRole.RoleId ==
                                            role.Id),

                            permissionCount =
                                _dbContext
                                    .RolePermissions
                                    .Count(
                                        rolePermission =>
                                            rolePermission.RoleId ==
                                            role.Id)
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            roles);
    }

    /*
     * ============================================================
     * GET ROLE
     * ============================================================
     */

    [HttpGet("{roleId:guid}")]
    public async Task<IActionResult> GetRole(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.view"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var role =
            await _dbContext.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            roleId
                        &&
                        item.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            role is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Rol no encontrado."
                });
        }

        var permissions =
            await (
                from rolePermission
                    in _dbContext.RolePermissions

                join permission
                    in _dbContext.Permissions
                    on rolePermission.PermissionId
                    equals permission.Id

                where
                    rolePermission.RoleId ==
                    role.Id

                orderby
                    permission.Module,
                    permission.Name

                select new
                {
                    id =
                        permission.Id,

                    code =
                        permission.Code,

                    name =
                        permission.Name,

                    module =
                        permission.Module,

                    description =
                        permission.Description
                }
            )
            .AsNoTracking()
            .ToListAsync(
                cancellationToken);

        var users =
            await (
                from userRole
                    in _dbContext.UserRoles

                join user
                    in _dbContext.Users
                    on userRole.UserId
                    equals user.Id

                where
                    userRole.RoleId ==
                    role.Id
                    &&
                    user.OrganizationId ==
                    organizationId

                orderby
                    user.FirstName,
                    user.LastName

                select new
                {
                    id =
                        user.Id,

                    firstName =
                        user.FirstName,

                    lastName =
                        user.LastName,

                    email =
                        user.Email,

                    isActive =
                        user.IsActive
                }
            )
            .AsNoTracking()
            .ToListAsync(
                cancellationToken);

        return Ok(
            new
            {
                id =
                    role.Id,

                name =
                    role.Name,

                description =
                    role.Description,

                isSystemRole =
                    role.IsSystemRole,

                isActive =
                    role.IsActive,

                createdAtUtc =
                    role.CreatedAtUtc,

                updatedAtUtc =
                    role.UpdatedAtUtc,

                permissions,

                users
            });
    }

    /*
     * ============================================================
     * PERMISSIONS CATALOG
     * ============================================================
     */

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions(
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.view"))
        {
            return Forbid();
        }

        var permissions =
            await _dbContext.Permissions
                .AsNoTracking()
                .Where(
                    permission =>
                        permission.IsActive)
                .OrderBy(
                    permission =>
                        permission.Module)
                .ThenBy(
                    permission =>
                        permission.Name)
                .Select(
                    permission =>
                        new
                        {
                            id =
                                permission.Id,

                            code =
                                permission.Code,

                            name =
                                permission.Name,

                            module =
                                permission.Module,

                            description =
                                permission.Description
                        })
                .ToListAsync(
                    cancellationToken);

        /*
         * Agrupamos también por módulo para que el frontend pueda
         * construir una matriz visual de permisos.
         */
        var modules =
            permissions
                .GroupBy(
                    permission =>
                        permission.module)
                .Select(
                    group =>
                        new
                        {
                            module =
                                group.Key,

                            permissions =
                                group.ToArray()
                        })
                .ToArray();

        return Ok(
            new
            {
                total =
                    permissions.Count,

                modules
            });
    }

    /*
     * ============================================================
     * CREATE ROLE
     * ============================================================
     */

    [HttpPost]
    public async Task<IActionResult> CreateRole(
        [FromBody]
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        if (
            organizationId ==
            Guid.Empty)
        {
            return Unauthorized();
        }

        if (
            string.IsNullOrWhiteSpace(
                request.Name))
        {
            return BadRequest(
                new
                {
                    message =
                        "El nombre del rol es obligatorio."
                });
        }

        var normalizedName =
            request.Name.Trim();

        var exists =
            await _dbContext.Roles
                .AnyAsync(
                    role =>
                        role.OrganizationId ==
                            organizationId
                        &&
                        role.Name ==
                            normalizedName,
                    cancellationToken);

        if (
            exists)
        {
            return Conflict(
                new
                {
                    message =
                        "Ya existe un rol con ese nombre."
                });
        }

        var role =
            new Role(
                organizationId,
                normalizedName);

        role.SetDescription(
            request.Description);

        _dbContext.Roles.Add(
            role);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        if (
            request.PermissionIds is not null
            &&
            request.PermissionIds.Count >
                0)
        {
            var result =
                await ReplacePermissionsInternalAsync(
                    role,
                    request.PermissionIds,
                    cancellationToken);

            if (
                result is not null)
            {
                _dbContext.Roles.Remove(
                    role);

                await _dbContext.SaveChangesAsync(
                    cancellationToken);
                
                /*
 * Todos los usuarios que poseen este rol deben
 * obtener un nuevo conjunto de permisos.
 */

            await _sessionSecurity
                .RevokeRoleSessionsAsync(
                    role.Id,
                    HttpContext
                        .Connection
                        .RemoteIpAddress
                        ?.ToString(),
                    cancellationToken);

                return result;
            }
        }

        _logger.LogInformation(
            "Rol creado. RoleId={RoleId}, Name={RoleName}, Actor={ActorUserId}.",
            role.Id,
            role.Name,
            GetUserId());

        return CreatedAtAction(
            nameof(GetRole),
            new
            {
                roleId =
                    role.Id
            },
            new
            {
                id =
                    role.Id,

                name =
                    role.Name,

                description =
                    role.Description,

                isSystemRole =
                    role.IsSystemRole,

                isActive =
                    role.IsActive
            });
    }

    /*
     * ============================================================
     * UPDATE ROLE
     * ============================================================
     */

    [HttpPut("{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(
        Guid roleId,
        [FromBody]
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var role =
            await _dbContext.Roles
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            roleId
                        &&
                        item.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            role is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Rol no encontrado."
                });
        }

        if (
            string.IsNullOrWhiteSpace(
                request.Name))
        {
            return BadRequest(
                new
                {
                    message =
                        "El nombre del rol es obligatorio."
                });
        }

        if (
            !role.IsSystemRole)
        {
            var normalizedName =
                request.Name.Trim();

            var duplicate =
                await _dbContext.Roles
                    .AnyAsync(
                        item =>
                            item.Id !=
                                role.Id
                            &&
                            item.OrganizationId ==
                                organizationId
                            &&
                            item.Name ==
                                normalizedName,
                        cancellationToken);

            if (
                duplicate)
            {
                return Conflict(
                    new
                    {
                        message =
                            "Ya existe otro rol con ese nombre."
                    });
            }
        }

        role.Update(
            request.Name,
            request.Description);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Ok(
            new
            {
                id =
                    role.Id,

                name =
                    role.Name,

                description =
                    role.Description,

                isSystemRole =
                    role.IsSystemRole,

                isActive =
                    role.IsActive,

                updatedAtUtc =
                    role.UpdatedAtUtc
            });
    }

    /*
     * ============================================================
     * REPLACE PERMISSIONS
     * ============================================================
     */

    [HttpPut("{roleId:guid}/permissions")]
    public async Task<IActionResult> ReplacePermissions(
        Guid roleId,
        [FromBody]
        AssignRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var role =
            await _dbContext.Roles
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            roleId
                        &&
                        item.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            role is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Rol no encontrado."
                });
        }

        /*
         * SuperAdmin se mantiene con todos los permisos disponibles.
         * No permitimos degradarlo desde la interfaz.
         */
        if (
            role.IsSystemRole
            &&
            string.Equals(
                role.Name,
                "SuperAdmin",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                new
                {
                    message =
                        "Los permisos del rol SuperAdmin son administrados por TitanMDM y no pueden reducirse."
                });
        }

        var result =
            await ReplacePermissionsInternalAsync(
                role,
                request.PermissionIds,
                cancellationToken);

        if (
            result is not null)
        {
            return result;
        }

        return NoContent();
    }

    /*
     * ============================================================
     * ACTIVATE
     * ============================================================
     */

    [HttpPost("{roleId:guid}/activate")]
    public async Task<IActionResult> ActivateRole(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var role =
            await _dbContext.Roles
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            roleId
                        &&
                        item.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            role is null)
        {
            return NotFound();
        }

        role.Activate();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }

    /*
     * ============================================================
     * DEACTIVATE
     * ============================================================
     */

    [HttpPost("{roleId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateRole(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "roles.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var role =
            await _dbContext.Roles
                .FirstOrDefaultAsync(
                    item =>
                        item.Id ==
                            roleId
                        &&
                        item.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            role is null)
        {
            return NotFound();
        }

        if (
            role.IsSystemRole)
        {
            return BadRequest(
                new
                {
                    message =
                        "Los roles del sistema no pueden ser desactivados."
                });
        }

        /*
         * No dejamos desactivar un rol que todavía esté
         * asignado a usuarios.
         */
        var assignedUsers =
            await _dbContext.UserRoles
                .CountAsync(
                    userRole =>
                        userRole.RoleId ==
                        role.Id,
                    cancellationToken);

        if (
            assignedUsers >
            0)
        {
            return Conflict(
                new
                {
                    message =
                        $"El rol está asignado a {assignedUsers} usuario(s). Retira esas asignaciones antes de desactivarlo."
                });
        }

        role.Deactivate();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }

    /*
     * ============================================================
     * INTERNAL PERMISSION ASSIGNMENT
     * ============================================================
     */

    private async Task<IActionResult?>
        ReplacePermissionsInternalAsync(
            Role role,
            IReadOnlyCollection<Guid> permissionIds,
            CancellationToken cancellationToken)
    {
        var distinctIds =
            permissionIds
                .Where(
                    permissionId =>
                        permissionId !=
                        Guid.Empty)
                .Distinct()
                .ToArray();

        var permissions =
            await _dbContext.Permissions
                .Where(
                    permission =>
                        distinctIds.Contains(
                            permission.Id)
                        &&
                        permission.IsActive)
                .ToListAsync(
                    cancellationToken);

        if (
            permissions.Count !=
            distinctIds.Length)
        {
            return BadRequest(
                new
                {
                    message =
                        "Uno o más permisos no existen o están inactivos."
                });
        }

        var current =
            await _dbContext
                .RolePermissions
                .Where(
                    rolePermission =>
                        rolePermission.RoleId ==
                        role.Id)
                .ToListAsync(
                    cancellationToken);

        _dbContext.RolePermissions
            .RemoveRange(
                current);

        foreach (
            var permission
            in permissions)
        {
            _dbContext.RolePermissions.Add(
                new RolePermission(
                    role.Id,
                    permission.Id));
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return null;
    }

    /*
     * ============================================================
     * CLAIM HELPERS
     * ============================================================
     */

    private Guid GetOrganizationId()
    {
        var value =
            User.FindFirstValue(
                "organization_id");

        return Guid.TryParse(
            value,
            out var organizationId)
                ? organizationId
                : Guid.Empty;
    }

    private Guid GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return Guid.TryParse(
            value,
            out var userId)
                ? userId
                : Guid.Empty;
    }

    private bool HasPermission(
        string permission)
    {
        return User.Claims
            .Any(
                claim =>
                    claim.Type ==
                        "permission"
                    &&
                    string.Equals(
                        claim.Value,
                        permission,
                        StringComparison
                            .OrdinalIgnoreCase));
    }
}

/*
 * ================================================================
 * REQUEST CONTRACTS
 * ================================================================
 */

public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    IReadOnlyCollection<Guid>? PermissionIds);

public sealed record UpdateRoleRequest(
    string Name,
    string? Description);

public sealed record AssignRolePermissionsRequest(
    IReadOnlyCollection<Guid> PermissionIds);

