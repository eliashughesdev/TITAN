using System.Text.Json.Nodes;
using TitanMDM.Infrastructure.Android.Policies;

namespace TitanMDM.UnitTests;

public sealed class AndroidPolicyCompilerRestrictionsTests
{
    private readonly AndroidPolicyCompiler _compiler = new();

    [Fact]
    public void Restrictions_UseAmapiV1EnumNames()
    {
        const string configuration =
            """
            {
              "android": {
                "restrictions": {
                  "blockPrinting": true,
                  "blockMicrophone": true,
                  "blockCamera": true
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.True(result.IsValid);
        var policy = Assert.IsType<JsonObject>(result.GooglePolicy);

        Assert.Equal("PRINTING_DISALLOWED", policy["printingPolicy"]!.GetValue<string>());
        Assert.Equal("MICROPHONE_ACCESS_DISABLED", policy["microphoneAccess"]!.GetValue<string>());
        Assert.Equal("CAMERA_ACCESS_DISABLED", policy["cameraAccess"]!.GetValue<string>());
    }

    [Fact]
    public void Restrictions_NotConfigured_AreOmitted()
    {
        const string configuration =
            """
            {
              "android": {
                "restrictions": {
                  "blockPrinting": false,
                  "blockMicrophone": null
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.True(result.IsValid);
        var policy = Assert.IsType<JsonObject>(result.GooglePolicy);

        Assert.False(policy.ContainsKey("printingPolicy"));
        Assert.False(policy.ContainsKey("microphoneAccess"));
    }

    [Theory]
    [InlineData("", true, false, false)]
    [InlineData(",\"lockStatusBar\":true,\"lockSystemKeys\":true,\"persistentHome\":false", false, true, true)]
    public void Launcher_EmitsDeviceLockKeys(
        string extra,
        bool persistentHome,
        bool lockStatusBar,
        bool lockSystemKeys)
    {
        var configuration =
            "{\"android\":{\"launcher\":{\"enabled\":true,\"companyName\":\"CISA\"" + extra + "}}}";

        var result = _compiler.Compile(configuration);

        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
        var policy = Assert.IsType<JsonObject>(result.GooglePolicy);
        var agent = Assert.IsType<JsonArray>(policy["applications"])
            .OfType<JsonObject>()
            .Single(x => x["packageName"]!.GetValue<string>() == "com.titanmdm.agent");
        var managed = Assert.IsType<JsonObject>(agent["managedConfiguration"]);

        Assert.Equal(persistentHome, managed["titan_persistent_home"]!.GetValue<bool>());
        Assert.Equal(lockStatusBar, managed["titan_lock_status_bar"]!.GetValue<bool>());
        Assert.Equal(lockSystemKeys, managed["titan_lock_system_keys"]!.GetValue<bool>());
    }
}
