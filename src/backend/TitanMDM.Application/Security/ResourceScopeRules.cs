namespace TitanMDM.Application.Security;

public static class ResourceScopeRules
{
    public static bool CanAccessResource(
        bool hasOrganizationScope,
        IReadOnlyCollection<Guid> accessibleSiteIds,
        Guid? resourceSiteId)
    {
        if (hasOrganizationScope)
        {
            return true;
        }

        /*
         * Un recurso sin Site NO debe quedar visible
         * automáticamente para un operador limitado.
         *
         * Solamente un Organization scope puede ver
         * recursos todavía no clasificados.
         */
        if (!resourceSiteId.HasValue)
        {
            return false;
        }

        return accessibleSiteIds.Contains(
            resourceSiteId.Value);
    }
}