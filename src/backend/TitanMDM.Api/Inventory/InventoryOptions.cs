using Microsoft.Extensions.Options;

namespace TitanMDM.Api.Inventory;

public sealed class InventoryOptions
{
    const string SectionName = "InventoryInternal";

    public bool IsConfigured { get; set; }

    public string? BaseUri { get; set; }

    public string? IntegrationKey { get; set; }

    public int TimeoutSeconds { get; set; } = 300;

    public int DeviceOperationTimeoutSeconds { get; set; } = 600;

    public static InventoryOptions GetFromConfiguration(
        IConfiguration configuration)
    {
        var options = new InventoryOptions();

        var section = configuration.GetSection(SectionName);

        if (section.Exists())
        {
            options.IsConfigured =
                section.GetValue<bool>("IsConfigured");

            options.BaseUri =
                section.GetValue<string>("BaseUri");

            options.IntegrationKey =
                section.GetValue<string>("IntegrationKey");

            options.TimeoutSeconds =
                section.GetValue<int>("TimeoutSeconds");

            options.DeviceOperationTimeoutSeconds =
                section.GetValue<int>(
                    "DeviceOperationTimeoutSeconds");
        }

        return options;
    }
}