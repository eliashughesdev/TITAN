using System.Reflection;

using TitanMDM.Api.Controllers;

namespace TitanMDM.IntegrationTests;

/*
 * ============================================================
 * TITANMDM HELPDESK — CONTRATO DE USUARIO CON SEDE
 * ============================================================
 *
 * Garantiza que las peticiones de creación y actualización de
 * usuario exponen SiteId y SiteLocationId, de modo que la sede
 * pueda persistirse a través de SiteAssignmentService.
 */

public sealed class UserRequestSiteContractTests
{
    [Theory]
    [InlineData(typeof(CreateUserRequest))]
    [InlineData(typeof(UpdateUserRequest))]
    public void RequestExposesSiteAndLocation(
        Type requestType)
    {
        Assert.NotNull(
            requestType.GetProperty(
                "SiteId",
                BindingFlags.Public
                    | BindingFlags.Instance));

        Assert.NotNull(
            requestType.GetProperty(
                "SiteLocationId",
                BindingFlags.Public
                    | BindingFlags.Instance));
    }
}
