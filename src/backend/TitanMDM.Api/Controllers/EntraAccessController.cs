using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/helpdesk/entra/access")]
public sealed class EntraAccessController : ControllerBase
{
    private readonly TitanMdmDbContext _db;

    public EntraAccessController(TitanMdmDbContext db)
    {
        _db = db;
    }

    [HttpPost("{directoryUserId:guid}")]
    public async Task<IActionResult> Grant(
        Guid directoryUserId,
        [FromBody] GrantEntraAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasPermission("users.manage"))
            return Forbid();

        var organizationId = GetOrganizationId();
        if (organizationId is null)
            return Unauthorized();

        if (request.RoleId == Guid.Empty)
            return BadRequest(new { message = "Selecciona un rol." });

        var directoryUser = await _db.EntraDirectoryUsers
            .SingleOrDefaultAsync(
                x => x.Id == directoryUserId &&
                     x.OrganizationId == organizationId.Value,
                cancellationToken);

        if (directoryUser is null || !directoryUser.IsActive)
            return NotFound(new
            {
                message = "La persona no está activa en el directorio sincronizado."
            });

        if (directoryUser.LinkedTitanUserId.HasValue)
            return Conflict(new
            {
                message = "Esta persona ya tiene una cuenta TitanMDM vinculada."
            });

        var role = await _db.Roles
            .SingleOrDefaultAsync(
                x => x.Id == request.RoleId &&
                     x.OrganizationId == organizationId.Value &&
                     x.IsActive &&
                     !x.IsSystemRole,
                cancellationToken);

        if (role is null)
            return BadRequest(new
            {
                message = "El rol no existe o no puede asignarse desde esta pantalla."
            });

        var email = (directoryUser.Mail ??
                     directoryUser.UserPrincipalName)
            .Trim()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || email.Length > 320)
            return BadRequest(new
            {
                message = "La persona no tiene un correo válido para TitanMDM."
            });

        // No se vincula automáticamente una cuenta existente por correo:
        // una coincidencia de texto no demuestra que sea la misma identidad.
        var emailInUse = await _db.Users.AnyAsync(
            x => x.OrganizationId == organizationId.Value &&
                 x.Email == email,
            cancellationToken);

        if (emailInUse)
            return Conflict(new
            {
                message =
                    "Ya existe una cuenta TitanMDM con ese correo. " +
                    "Revisa su identidad antes de vincularla."
            });

        var parts = directoryUser.DisplayName
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        var firstName = parts.FirstOrDefault();
        var lastName = string.Join(' ', parts.Skip(1));

        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName))
        {
            return BadRequest(new
            {
                message =
                    "El nombre sincronizado debe incluir nombre y apellido. " +
                    "Corrígelo en Entra ID y sincroniza nuevamente."
            });
        }

        var user = new User(
            organizationId.Value,
            firstName,
            lastName,
            email);

        user.SetJobTitle(directoryUser.JobTitle);

        // PasswordHash permanece null. Esta cuenta solo podrá entrar
        // cuando se habilite el inicio de sesión con Microsoft.
        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole(
            user.Id,
            role.Id,
            GetActorUserId()));

        directoryUser.Update(
            directoryUser.DisplayName,
            directoryUser.Mail,
            directoryUser.JobTitle,
            directoryUser.Department,
            user.Id,
            directoryUser.IsActive);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message =
                    "No se pudo guardar el acceso. Comprueba si " +
                    "otra cuenta ya utiliza ese correo y vuelve a cargar."
            });
        }

        return Ok(new
        {
            directoryUserId = directoryUser.Id,
            userId = user.Id,
            user.Email,
            roleId = role.Id,
            roleName = role.Name,
            microsoftLoginReady = false
        });
    }

    private bool HasPermission(string permission) =>
        User.Claims.Any(claim =>
            claim.Type == "permission" &&
            string.Equals(
                claim.Value,
                permission,
                StringComparison.OrdinalIgnoreCase));

    private Guid? GetOrganizationId()
    {
        var value = User.FindFirstValue("organization_id") ??
                    User.FindFirstValue("organizationId");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    private Guid? GetActorUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    User.FindFirstValue("sub");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    public sealed record GrantEntraAccessRequest(Guid RoleId);
}