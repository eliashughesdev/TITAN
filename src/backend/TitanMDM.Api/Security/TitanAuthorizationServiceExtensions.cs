using Microsoft.AspNetCore.Authorization;

namespace TitanMDM.Api.Security;

public static class TitanAuthorizationServiceExtensions
{
    public static IServiceCollection
        AddTitanAuthorization(
            this IServiceCollection services)
    {
        services.AddSingleton<
            IAuthorizationPolicyProvider,
            PermissionAuthorizationPolicyProvider>();

        services.AddSingleton<
            IAuthorizationHandler,
            PermissionAuthorizationHandler>();

        return services;
    }
}