using System.Security.Claims;

using TitanMDM.Api.Security;

namespace TitanMDM.IntegrationTests;

/*
 * ============================================================
 * TITANMDM HELPDESK — AUTORIZACIÓN DE ROUTING
 * ============================================================
 *
 * Verifica que la centralización de permisos de routing:
 *  - autoriza a quien ya tenía acceso (no debilita),
 *  - rechaza a quien no lo tiene,
 *  - es insensible a mayúsculas/minúsculas,
 *  - y comparte el mismo conjunto entre operaciones, cola
 *    y diagnósticos.
 */

public sealed class HelpdeskRoutingAuthorizationTests
{
    [Theory]
    [InlineData("helpdesk.routing.view")]
    [InlineData("helpdesk.admin.access")]
    [InlineData("helpdesk.ticket.details.view")]
    [InlineData("settings.manage")]
    public void View_IsGrantedForKnownReadPermissions(
        string permission)
    {
        Assert.True(
            HelpdeskRoutingAuthorization.CanView(
                Principal(
                    permission)));
    }

    [Theory]
    [InlineData("helpdesk.ticket.assign")]
    [InlineData("tickets.assign")]
    [InlineData("helpdesk.routing.manage")]
    public void Manage_IsGrantedForKnownManagePermissions(
        string permission)
    {
        Assert.True(
            HelpdeskRoutingAuthorization.CanManage(
                Principal(
                    permission)));
    }

    [Fact]
    public void View_IsDeniedWithoutRoutingPermission()
    {
        Assert.False(
            HelpdeskRoutingAuthorization.CanView(
                Principal(
                    "devices.view")));
    }

    [Fact]
    public void Manage_IsDeniedForReadOnlyPermission()
    {
        Assert.False(
            HelpdeskRoutingAuthorization.CanManage(
                Principal(
                    "helpdesk.groups.view")));
    }

    [Fact]
    public void NoPermissions_IsDeniedForViewAndManage()
    {
        var principal =
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            Guid.NewGuid()
                                .ToString())
                    },
                    "Test"));

        Assert.False(
            HelpdeskRoutingAuthorization.CanView(
                principal));

        Assert.False(
            HelpdeskRoutingAuthorization.CanManage(
                principal));
    }

    [Fact]
    public void PermissionMatch_IsCaseInsensitive()
    {
        Assert.True(
            HelpdeskRoutingAuthorization.CanView(
                Principal(
                    "HELPDESK.ROUTING.VIEW")));
    }

    private static ClaimsPrincipal Principal(
        string permission)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        Guid.NewGuid()
                            .ToString()),

                    new Claim(
                        "permission",
                        permission)
                },
                "Test"));
    }
}
