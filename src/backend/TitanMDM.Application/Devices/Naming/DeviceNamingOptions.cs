namespace TitanMDM.Application.Devices.Naming;

public sealed class DeviceNamingOptions
{
    public const string SectionName =
        "DeviceNaming";

    public bool Enabled
    {
        get;
        init;
    } = true;

    public string OrganizationPrefix
    {
        get;
        init;
    } = "CI";

    public Dictionary<string, string>
        DeviceTypes
    {
        get;
        init;
    } =
        new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["L"] = "Laptop",
            ["D"] = "Desktop"
        };

    public Dictionary<string, string>
        Cities
    {
        get;
        init;
    } =
        new(
            StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DeviceAreaMapping>
        Areas
    {
        get;
        init;
    } =
        new(
            StringComparer.OrdinalIgnoreCase);
}

public sealed class DeviceAreaMapping
{
    /// <summary>
    /// Nombre o fragmento del Site.
    /// Ej: CEDI SPM.
    /// </summary>
    public string? SiteName
    {
        get;
        init;
    }

    /// <summary>
    /// Sublocalidad opcional.
    /// Ej: Facturación, Tráfico, Laboratorio.
    /// </summary>
    public string? SiteLocationName
    {
        get;
        init;
    }
}