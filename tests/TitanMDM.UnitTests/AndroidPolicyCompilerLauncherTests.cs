using System.Text.Json.Nodes;
using TitanMDM.Infrastructure.Android.Policies;

namespace TitanMDM.UnitTests;

public sealed class AndroidPolicyCompilerLauncherTests
{
    private readonly AndroidPolicyCompiler _compiler = new();

    [Fact]
    public void Launcher_EmitsManagedConfigurationAndHomeActivity()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": {
                  "enabled": true,
                  "companyName": "Acme Corp",
                  "welcomeMessage": "Bienvenido"
                },
                "applications": {
                  "packages": [
                    { "packageName": "com.acme.mail", "installType": "AVAILABLE" },
                    { "packageName": "com.acme.crm", "installType": "FORCE_INSTALLED" },
                    { "packageName": "com.acme.game", "installType": "BLOCKED" }
                  ]
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.True(result.IsValid);

        var policy = Assert.IsType<JsonObject>(result.GooglePolicy);

        var applications = Assert.IsType<JsonArray>(policy["applications"]);

        var agent = applications
            .OfType<JsonObject>()
            .Single(x => x["packageName"]!.GetValue<string>() == "com.titanmdm.agent");

        Assert.Equal("REQUIRED_FOR_SETUP", agent["installType"]!.GetValue<string>());

        var managedConfiguration =
            Assert.IsType<JsonObject>(agent["managedConfiguration"]);

        Assert.Equal(
            "Acme Corp",
            managedConfiguration["titan_company_name"]!.GetValue<string>());

        Assert.True(
            managedConfiguration["titan_launcher_enabled"]!.GetValue<bool>());

        var allowedAppsJson =
            managedConfiguration["titan_allowed_apps"]!.GetValue<string>();

        var allowedApps =
            JsonNode.Parse(allowedAppsJson) as JsonArray;

        Assert.NotNull(allowedApps);

        var allowedNames = allowedApps!
            .OfType<JsonObject>()
            .Select(x => x["packageName"]!.GetValue<string>())
            .ToArray();

        Assert.Contains("com.acme.mail", allowedNames);
        Assert.Contains("com.acme.crm", allowedNames);
        Assert.DoesNotContain("com.acme.game", allowedNames);
        Assert.DoesNotContain("com.titanmdm.agent", allowedNames);

        var preferredActivities =
            Assert.IsType<JsonArray>(policy["persistentPreferredActivities"]);

        var receiver =
            Assert.IsType<JsonObject>(preferredActivities[0]);

        Assert.Equal(
            "com.titanmdm.agent/com.titanmdm.agent.launcher.TitanLauncherActivity",
            receiver["receiverActivity"]!.GetValue<string>());

        Assert.False(
            policy["kioskCustomLauncherEnabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Launcher_AndKiosk_AreIncompatible()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": { "enabled": true },
                "kiosk": { "enabled": true, "applicationId": "com.acme.kiosk" }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("launcher", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Launcher_Disabled_DoesNotEmitPreferredActivities()
    {
        const string configuration =
            """
            {
              "android": {
                "applications": {
                  "packages": [
                    { "packageName": "com.acme.mail", "installType": "AVAILABLE" }
                  ]
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.True(result.IsValid);

        var policy = Assert.IsType<JsonObject>(result.GooglePolicy);

        Assert.Null(policy["persistentPreferredActivities"]);
    }

    [Fact]
    public void Launcher_InvalidSchemaVersion_Fails()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": { "enabled": true, "schemaVersion": 0 }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.False(result.IsValid);
    }
}
