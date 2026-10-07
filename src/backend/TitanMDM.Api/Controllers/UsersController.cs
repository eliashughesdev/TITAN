using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

using TitanMDM.Api.Services;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController
    : ControllerBase
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IPasswordHasher<User>
        _passwordHasher;

    private readonly ILogger<
        UsersController> _logger;

    public UsersController(
    TitanMdmDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    SessionSecurityService sessionSecurity,
    ILogger<UsersController> logger)
{
    _dbContext =
        dbContext;

    _passwordHasher =
        passwordHasher;

    _sessionSecurity =
        sessionSecurity;

    _logger =
        logger;
}

    /*
     * ============================================================
     * GET USERS
     * ============================================================
     */

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] bool? active,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.view"))
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
            _dbContext.Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId);

        if (
            active.HasValue)
        {
            query =
                query.Where(
                    x =>
                        x.IsActive ==
                        active.Value);
        }

        if (
            !string.IsNullOrWhiteSpace(
                search))
        {
            var term =
                search
                    .Trim()
                    .ToLower();

            query =
                query.Where(
                    x =>
                        x.FirstName
                            .ToLower()
                            .Contains(term)
                        ||
                        x.LastName
                            .ToLower()
                            .Contains(term)
                        ||
                        x.Email
                            .ToLower()
                            .Contains(term)
                        ||
                        (
                            x.JobTitle !=
                                null
                            &&
                            x.JobTitle
                                .ToLower()
                                .Contains(term)
                        ));
        }

        var users =
            await query
                .OrderBy(
                    x =>
                        x.FirstName)
                .ThenBy(
                    x =>
                        x.LastName)
                .Select(
                    x =>
                        new
                        {
                            id =
                                x.Id,

                            firstName =
                                x.FirstName,

                            lastName =
                                x.LastName,

                            fullName =
                                x.FirstName
                                +
                                " "
                                +
                                x.LastName,

                            email =
                                x.Email,

                            jobTitle =
                                x.JobTitle,

                            departmentId =
                                x.DepartmentId,

                            isActive =
                                x.IsActive,

                            mfaEnabled =
                                x.MfaEnabled,

                            lastLoginAtUtc =
                                x.LastLoginAtUtc,

                            createdAtUtc =
                                x.CreatedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        return Ok(
            users);
    }

    /*
     * ============================================================
     * GET USER
     * ============================================================
     */

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.view"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var user =
            await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            user is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Usuario no encontrado."
                });
        }

        var roles =
            await (
                from userRole
                    in _dbContext.UserRoles

                join role
                    in _dbContext.Roles
                    on userRole.RoleId
                    equals role.Id

                where
                    userRole.UserId ==
                        user.Id

                orderby
                    role.Name

                select new
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
                }
            )
            .AsNoTracking()
            .ToListAsync(
                cancellationToken);

        return Ok(
            new
            {
                id =
                    user.Id,

                firstName =
                    user.FirstName,

                lastName =
                    user.LastName,

                fullName =
                    user.FullName,

                email =
                    user.Email,

                jobTitle =
                    user.JobTitle,

                departmentId =
                    user.DepartmentId,

                isActive =
                    user.IsActive,

                mfaEnabled =
                    user.MfaEnabled,

                lastLoginAtUtc =
                    user.LastLoginAtUtc,

                createdAtUtc =
                    user.CreatedAtUtc,

                updatedAtUtc =
                    user.UpdatedAtUtc,

                roles
            });
    }

    /*
     * ============================================================
     * CREATE USER
     * ============================================================
     */

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody]
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var actorUserId =
            GetUserId();

        if (
            organizationId ==
            Guid.Empty
            ||
            actorUserId ==
            Guid.Empty)
        {
            return Unauthorized();
        }

        var validationError =
            ValidateCreateRequest(
                request);

        if (
            validationError is not null)
        {
            return BadRequest(
                new
                {
                    message =
                        validationError
                });
        }

        var email =
            request.Email
                .Trim()
                .ToLowerInvariant();

        var exists =
            await _dbContext.Users
                .AnyAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Email ==
                            email,
                    cancellationToken);

        if (
            exists)
        {
            return Conflict(
                new
                {
                    message =
                        "Ya existe un usuario con ese correo."
                });
        }

        if (
            request.DepartmentId
                .HasValue)
        {
            var departmentExists =
                await _dbContext.Departments
                    .AnyAsync(
                        x =>
                            x.Id ==
                                request
                                    .DepartmentId
                                    .Value
                            &&
                            x.OrganizationId ==
                                organizationId,
                        cancellationToken);

            if (
                !departmentExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "El departamento indicado no existe."
                    });
            }
        }

        var user =
            new User(
                organizationId,
                request.FirstName,
                request.LastName,
                email);

        user.SetJobTitle(
            request.JobTitle);

        user.SetDepartment(
            request.DepartmentId);

        var passwordHash =
            _passwordHasher
                .HashPassword(
                    user,
                    request.Password);

        user.SetPasswordHash(
            passwordHash);

        if (
            request.MfaEnabled)
        {
            user.EnableMfa();
        }

        _dbContext.Users.Add(
            user);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
        
        /*
 * Los permisos efectivos del usuario cambiaron.
 * Invalidamos sus sesiones renovables.
 */

        await _sessionSecurity
            .RevokeUserSessionsAsync(
                user.Id,
                HttpContext
                    .Connection
                    .RemoteIpAddress
                    ?.ToString(),
                cancellationToken);

        if (
            request.RoleIds is not null
            &&
            request.RoleIds.Count >
                0)
        {
            var roleAssignmentResult =
                await AssignRolesInternalAsync(
                    user,
                    request.RoleIds,
                    actorUserId,
                    cancellationToken);

            if (
                roleAssignmentResult is not null)
            {
                _dbContext.Users.Remove(
                    user);

                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                return roleAssignmentResult;
            }
        }

        _logger.LogInformation(
            "Usuario creado. UserId={UserId}, Email={Email}, Actor={ActorUserId}.",
            user.Id,
            user.Email,
            actorUserId);

        return CreatedAtAction(
            nameof(GetUser),
            new
            {
                userId =
                    user.Id
            },
            new
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
            });
    }

    /*
     * ============================================================
     * UPDATE USER
     * ============================================================
     */

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateUser(
        Guid userId,
        [FromBody]
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var user =
            await _dbContext.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            user is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Usuario no encontrado."
                });
        }

        if (
            string.IsNullOrWhiteSpace(
                request.FirstName)
            ||
            string.IsNullOrWhiteSpace(
                request.LastName))
        {
            return BadRequest(
                new
                {
                    message =
                        "Nombre y apellido son obligatorios."
                });
        }

        if (
            request.DepartmentId
                .HasValue)
        {
            var departmentExists =
                await _dbContext.Departments
                    .AnyAsync(
                        x =>
                            x.Id ==
                                request
                                    .DepartmentId
                                    .Value
                            &&
                            x.OrganizationId ==
                                organizationId,
                        cancellationToken);

            if (
                !departmentExists)
            {
                return BadRequest(
                    new
                    {
                        message =
                            "El departamento indicado no existe."
                    });
            }
        }

       user.UpdateProfile(
            request.FirstName,
            request.LastName);
            
        user.SetJobTitle(
            request.JobTitle);

        user.SetDepartment(
            request.DepartmentId);

        if (
            request.MfaEnabled)
        {
            user.EnableMfa();
        }
        else
        {
            user.DisableMfa();
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Ok(
            new
            {
                id =
                    user.Id,

                firstName =
                    user.FirstName,

                lastName =
                    user.LastName,

                email =
                    user.Email,

                jobTitle =
                    user.JobTitle,

                departmentId =
                    user.DepartmentId,

                mfaEnabled =
                    user.MfaEnabled,

                isActive =
                    user.IsActive
            });
    }

    /*
     * ============================================================
     * ACTIVATE
     * ============================================================
     */

    [HttpPost("{userId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var user =
            await _dbContext.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            user is null)
        {
            return NotFound();
        }

        user.Activate();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }

    /*
     * ============================================================
     * DEACTIVATE
     * ============================================================
     */

    [HttpPost("{userId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var actorUserId =
            GetUserId();

        if (
            userId ==
            actorUserId)
        {
            return BadRequest(
                new
                {
                    message =
                        "No puedes desactivar tu propia cuenta."
                });
        }

        var user =
            await _dbContext.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            user is null)
        {
            return NotFound();
        }

        user.Deactivate();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }

    /*
     * ============================================================
     * CHANGE PASSWORD
     * ============================================================
     */

    [HttpPost("{userId:guid}/password")]
    public async Task<IActionResult> ChangePassword(
        Guid userId,
        [FromBody]
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.manage"))
        {
            return Forbid();
        }

        if (
            string.IsNullOrWhiteSpace(
                request.NewPassword)
            ||
            request.NewPassword.Length <
                12)
        {
            return BadRequest(
                new
                {
                    message =
                        "La contraseña debe tener al menos 12 caracteres."
                });
        }

        var organizationId =
            GetOrganizationId();

        var user =
            await _dbContext.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            user is null)
        {
            return NotFound();
        }

        var passwordHash =
            _passwordHasher
                .HashPassword(
                    user,
                    request.NewPassword);

        user.SetPasswordHash(
            passwordHash);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }

    /*
     * ============================================================
     * ASSIGN ROLES
     * ============================================================
     */

    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> AssignRoles(
        Guid userId,
        [FromBody]
        AssignUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (
            !HasPermission(
                "users.manage")
            ||
            !HasPermission(
                "roles.manage"))
        {
            return Forbid();
        }

        var organizationId =
            GetOrganizationId();

        var actorUserId =
            GetUserId();

        var user =
            await _dbContext.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            userId
                        &&
                        x.OrganizationId ==
                            organizationId,
                    cancellationToken);

        if (
            user is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Usuario no encontrado."
                });
        }

        var result =
            await AssignRolesInternalAsync(
                user,
                request.RoleIds,
                actorUserId,
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
     * INTERNAL ROLE ASSIGNMENT
     * ============================================================
     */

    private async Task<IActionResult?>
        AssignRolesInternalAsync(
            User user,
            IReadOnlyCollection<Guid> roleIds,
            Guid actorUserId,
            CancellationToken cancellationToken)
    {
        var organizationId =
            user.OrganizationId;

        var distinctRoleIds =
            roleIds
                .Where(
                    x =>
                        x !=
                        Guid.Empty)
                .Distinct()
                .ToArray();

        var roles =
            await _dbContext.Roles
                .Where(
                    x =>
                        distinctRoleIds
                            .Contains(
                                x.Id)
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive)
                .ToListAsync(
                    cancellationToken);

        if (
            roles.Count !=
            distinctRoleIds.Length)
        {
            return BadRequest(
                new
                {
                    message =
                        "Uno o más roles no existen o no están activos."
                });
        }

        var currentAssignments =
            await _dbContext.UserRoles
                .Where(
                    x =>
                        x.UserId ==
                        user.Id)
                .ToListAsync(
                    cancellationToken);

        _dbContext.UserRoles
            .RemoveRange(
                currentAssignments);

        foreach (
            var role in roles)
        {
            _dbContext.UserRoles.Add(
                new UserRole(
                    user.Id,
                    role.Id,
                    actorUserId));
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return null;
    }

    /*
     * ============================================================
     * VALIDATION
     * ============================================================
     */

    private static string?
        ValidateCreateRequest(
            CreateUserRequest request)
    {
        if (
            string.IsNullOrWhiteSpace(
                request.FirstName))
        {
            return
                "El nombre es obligatorio.";
        }

        if (
            string.IsNullOrWhiteSpace(
                request.LastName))
        {
            return
                "El apellido es obligatorio.";
        }

        if (
            string.IsNullOrWhiteSpace(
                request.Email))
        {
            return
                "El correo es obligatorio.";
        }

        if (
            string.IsNullOrWhiteSpace(
                request.Password)
            ||
            request.Password.Length <
                12)
        {
            return
                "La contraseña debe tener al menos 12 caracteres.";
        }

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

    private readonly SessionSecurityService
    _sessionSecurity;
}

/*
 * ================================================================
 * REQUEST CONTRACTS
 * ================================================================
 */

public sealed record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? JobTitle,
    Guid? DepartmentId,
    bool MfaEnabled,
    IReadOnlyCollection<Guid>? RoleIds);

public sealed record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? JobTitle,
    Guid? DepartmentId,
    bool MfaEnabled);

public sealed record ChangePasswordRequest(
    string NewPassword);

public sealed record AssignUserRolesRequest(
    IReadOnlyCollection<Guid> RoleIds);