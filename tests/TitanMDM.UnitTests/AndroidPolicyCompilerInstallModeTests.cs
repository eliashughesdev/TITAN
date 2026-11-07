using System.Text.Json.Nodes;
using TitanMDM.Infrastructure.Android.Policies;

namespace TitanMDM.UnitTests;

/*
 * Apps obligatorias: el tipo de cada app manda; el "Modo de instalación"
 * global FORCE_INSTALLED solo eleva las marcadas como Disponible.
 */
public sealed class AndroidPolicyCompilerInstallModeTests
{
    private readonly AndroidPolicyCompiler _compiler = new();

    private Dictionary<string, string> Compile(string installMode, params (string Package, string InstallType)[] apps)
    {
        var packages = new JsonArray(apps
            .Select(app => (JsonNode)new JsonObject
            {
                ["packageName"] = app.Package,
                ["label"] = app.Package,
                ["installType"] = app.InstallType,
            })
            .ToArray());

        var configuration = new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["applications"] = new JsonObject
                {
                    ["installMode"] = installMode,
                    ["packages"] = packages,
                },
            },
        };

        var result = _compiler.Compile(configuration.ToJsonString());
        Assert.True(result.IsValid, string.Join("; ", result.Errors));

        return result.GooglePolicy!["applications"]!.AsArray()
            .Select(x => x!.AsObject())
            .ToDictionary(x => x["packageName"]!.GetValue<string>(), x => x["installType"]!.GetValue<string>());
    }

    [Fact]
    public void App_forzada_se_mantiene_aunque_el_modo_global_sea_disponible()
    {
        var apps = Compile("AVAILABLE",
            ("com.Microsoft.WarehouseManagement", "FORCE_INSTALLED"),
            ("com.android.chrome", "AVAILABLE"),
            ("com.honeywell.setup", "REQUIRED_FOR_SETUP"));

        Assert.Equal("FORCE_INSTALLED", apps["com.Microsoft.WarehouseManagement"]);
        Assert.Equal("AVAILABLE", apps["com.android.chrome"]);
        Assert.Equal("REQUIRED_FOR_SETUP", apps["com.honeywell.setup"]);
    }

    [Fact]
    public void Modo_global_forzado_eleva_las_apps_disponibles_y_respeta_las_bloqueadas()
    {
        var apps = Compile("FORCE_INSTALLED",
            ("com.Microsoft.WarehouseManagement", "AVAILABLE"),
            ("com.whatsapp", "BLOCKED"),
            ("com.honeywell.setup", "REQUIRED_FOR_SETUP"));

        Assert.Equal("FORCE_INSTALLED", apps["com.Microsoft.WarehouseManagement"]);
        Assert.Equal("BLOCKED", apps["com.whatsapp"]);
        Assert.Equal("REQUIRED_FOR_SETUP", apps["com.honeywell.setup"]);
    }
}
