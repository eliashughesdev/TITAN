using System.Text.Json.Nodes;
using TitanMDM.Infrastructure.Android.Policies;

namespace TitanMDM.UnitTests;

public sealed class AndroidPolicyCompilerFactoryAppsTests
{
    private readonly AndroidPolicyCompiler _compiler = new();

    [Fact]
    public void PlayStore_forzada_y_apps_obligatorias_no_quedan_en_pendiente()
    {
        var packages = new JsonArray
        {
            new JsonObject
            {
                ["packageName"] = "com.acme.wms",
                ["installType"] = "FORCE_INSTALLED"
            },
            new JsonObject
            {
                ["packageName"] = "com.android.vending",
                ["installType"] = "BLOCKED"
            }
        };

        var result = _compiler.Compile(new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["applications"] = new JsonObject { ["packages"] = packages }
            }
        }.ToJsonString());

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var apps = result.GooglePolicy!["applications"]!.AsArray()
            .Select(x => x!.AsObject())
            .ToDictionary(x => x["packageName"]!.GetValue<string>(), x => x);

        Assert.Equal("FORCE_INSTALLED", apps["com.android.vending"]["installType"]!.GetValue<string>());
        Assert.Equal("GRANT", apps["com.acme.wms"]["defaultPermissionPolicy"]!.GetValue<string>());
        Assert.Equal("AUTO_UPDATE_HIGH_PRIORITY", apps["com.acme.wms"]["autoUpdateMode"]!.GetValue<string>());
        Assert.Contains(result.Warnings, warning => warning.Contains("Play Store", StringComparison.Ordinal));
    }

    [Fact]
    public void El_exceso_de_objetos_se_recorta_sin_perder_Play_Store_ni_la_app_forzada()
    {
        var packages = new JsonArray();
        for (var i = 0; i < AndroidPolicyCompiler.MaxGoogleApplications + 5; i++)
        {
            packages.Add(new JsonObject
            {
                ["packageName"] = $"com.acme.app{i}",
                ["installType"] = i == 0 ? "FORCE_INSTALLED" : "AVAILABLE"
            });
        }

        var result = _compiler.Compile(new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["applications"] = new JsonObject { ["packages"] = packages }
            }
        }.ToJsonString());

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var apps = result.GooglePolicy!["applications"]!.AsArray();
        Assert.Equal(AndroidPolicyCompiler.MaxGoogleApplications, apps.Count);
        Assert.Contains(apps.OfType<JsonObject>(), app => app["packageName"]!.GetValue<string>() == "com.android.vending");
        Assert.Contains(apps.OfType<JsonObject>(), app => app["packageName"]!.GetValue<string>() == "com.acme.app0");
        Assert.Contains(result.Warnings, warning => warning.Contains("supera", StringComparison.Ordinal));
    }

    [Fact]
    public void Respeta_la_politica_de_permisos_y_actualizacion_fijada_por_TI()
    {
        var packages = new JsonArray
        {
            new JsonObject
            {
                ["packageName"] = "com.acme.camera",
                ["installType"] = "FORCE_INSTALLED",
                ["defaultPermissionPolicy"] = "PROMPT",
                ["autoUpdateMode"] = "AUTO_UPDATE_POSTPONED"
            }
        };

        var result = _compiler.Compile(new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["applications"] = new JsonObject { ["packages"] = packages }
            }
        }.ToJsonString());

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var app = result.GooglePolicy!["applications"]!.AsArray()
            .Select(x => x!.AsObject())
            .Single(x => x["packageName"]!.GetValue<string>() == "com.acme.camera");

        Assert.Equal("PROMPT", app["defaultPermissionPolicy"]?.GetValue<string>());
        Assert.Equal("AUTO_UPDATE_POSTPONED", app["autoUpdateMode"]?.GetValue<string>());
    }
}
