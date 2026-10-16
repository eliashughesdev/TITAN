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
                    { "packageName": "com.acme.mail", "label": "Acme Mail", "installType": "AVAILABLE" },
                    { "packageName": "com.acme.crm", "displayName": "Acme CRM", "installType": "FORCE_INSTALLED" },
                    { "packageName": "com.acme.setup", "label": "Acme Setup", "installType": "REQUIRED_FOR_SETUP" },
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

        Assert.Equal(
            1,
            managedConfiguration["titan_launcher_schema_version"]!.GetValue<int>());

        var allowedAppsJson =
            managedConfiguration["titan_allowed_apps"]!.GetValue<string>();

        var allowedApps =
            JsonNode.Parse(allowedAppsJson) as JsonArray;

        Assert.NotNull(allowedApps);

        Assert.Collection(
            allowedApps!.OfType<JsonObject>(),
            app =>
            {
                Assert.Equal(
                    "com.acme.mail",
                    app["packageName"]!.GetValue<string>());
                Assert.Equal(
                    "Acme Mail",
                    app["label"]!.GetValue<string>());
                Assert.False(
                    app["required"]!.GetValue<bool>());
            },
            app =>
            {
                Assert.Equal(
                    "com.acme.crm",
                    app["packageName"]!.GetValue<string>());
                Assert.Equal(
                    "Acme CRM",
                    app["label"]!.GetValue<string>());
                Assert.True(
                    app["required"]!.GetValue<bool>());
            },
            app =>
            {
                Assert.Equal(
                    "com.acme.setup",
                    app["packageName"]!.GetValue<string>());
                Assert.Equal(
                    "Acme Setup",
                    app["label"]!.GetValue<string>());
                Assert.True(
                    app["required"]!.GetValue<bool>());
            });

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
    public void Launcher_AndKioskInstallType_AreIncompatible()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": { "enabled": true },
                "applications": {
                  "packages": [
                    { "packageName": "com.acme.kiosk", "installType": "KIOSK" }
                  ]
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.False(result.IsValid);
        Assert.Null(result.GooglePolicy);
        Assert.Contains(
            result.Errors,
            error => error.Contains("KIOSK", StringComparison.OrdinalIgnoreCase));
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

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("3")]
    [InlineData("\"1\"")]
    [InlineData("1.5")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Launcher_UnsupportedOrMalformedExplicitSchemaVersion_Fails(
        string schemaVersion)
    {
        var configuration =
            $$"""
            {
              "android": {
                "launcher": {
                  "enabled": true,
                  "schemaVersion": {{schemaVersion}}
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.False(result.IsValid);
        Assert.Null(result.GooglePolicy);
        Assert.Contains(
            result.Errors,
            error => error.Contains("schemaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void Launcher_CaseInsensitiveDuplicatePackages_Fail()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": { "enabled": true },
                "applications": {
                  "packages": [
                    { "packageName": "com.acme.Mail", "installType": "AVAILABLE" },
                    { "packageName": "COM.ACME.MAIL", "installType": "FORCE_INSTALLED" }
                  ]
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("duplicado", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("applications")]
    [InlineData("kiosk")]
    [InlineData("launcher")]
    public void AndroidPackageNames_AreValidatedConsistently(
        string source)
    {
        var android = new JsonObject();

        if (source == "applications")
        {
            android["applications"] =
                new JsonObject
                {
                    ["packages"] =
                        new JsonArray
                        {
                            new JsonObject
                            {
                                ["packageName"] = "invalid-package",
                                ["installType"] = "AVAILABLE"
                            }
                        }
                };
        }
        else if (source == "kiosk")
        {
            android["kiosk"] =
                new JsonObject
                {
                    ["enabled"] = true,
                    ["applicationId"] = "invalid-package"
                };
        }
        else
        {
            android["launcher"] =
                new JsonObject
                {
                    ["enabled"] = true,
                    ["packageName"] = "invalid-package"
                };
        }

        var configuration =
            new JsonObject
            {
                ["android"] = android
            };

        var result =
            _compiler.Compile(
                configuration.ToJsonString());

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("válido", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Launcher_EnforcesMaximumEffectiveApplications(
        int applicationCount,
        bool expectedValid)
    {
        var packages = new JsonArray();

        for (var index = 0;
             index < applicationCount;
             index++)
        {
            packages.Add(
                new JsonObject
                {
                    ["packageName"] =
                        $"com.acme.app{index}",
                    ["installType"] = "AVAILABLE"
                });
        }

        packages.Add(
            new JsonObject
            {
                ["packageName"] = "com.acme.blocked",
                ["installType"] = "BLOCKED"
            });

        var configuration =
            new JsonObject
            {
                ["android"] =
                    new JsonObject
                    {
                        ["launcher"] =
                            new JsonObject
                            {
                                ["enabled"] = true
                            },
                        ["applications"] =
                            new JsonObject
                            {
                                ["packages"] = packages
                            }
                    }
            };

        var result =
            _compiler.Compile(
                configuration.ToJsonString());

        Assert.Equal(expectedValid, result.IsValid);

        if (!expectedValid)
        {
            Assert.Contains(
                result.Errors,
                error => error.Contains("100", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Launcher_RejectsOversizedTextFields()
    {
        var configurations = new[]
        {
            CreateLauncherConfiguration(
                "companyName",
                new string('c', 81)),
            CreateLauncherConfiguration(
                "welcomeMessage",
                new string('w', 181)),
            CreateLauncherConfiguration(
                "label",
                new string('l', 61))
        };

        foreach (var configuration in configurations)
        {
            var result =
                _compiler.Compile(
                    configuration.ToJsonString());

            Assert.False(result.IsValid);
        }
    }

    [Fact]
    public void Launcher_CompilesPreviousProfileWithoutNewFields()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": {
                  "enabled": true,
                  "schemaVersion": 1,
                  "companyName": "Legado"
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.True(result.IsValid);
        var managed = GetLauncherManagedConfiguration(result);
        Assert.Equal(1, managed["titan_launcher_schema_version"]!.GetValue<int>());
        Assert.Equal(4, managed["titan_grid_columns"]!.GetValue<int>());
        Assert.False(managed.ContainsKey("titan_wallpaper_url"));
    }

    [Fact]
    public void Launcher_EmitsBrandingResourcesAndPublicationStamp()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": {
                  "enabled": true,
                  "schemaVersion": 2,
                  "wallpaperUrl": "https://cdn.empresa.com/launcher/fondo.webp",
                  "logoUrl": "https://cdn.empresa.com/launcher/logo.png",
                  "gridColumns": 5
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);
        Assert.True(result.IsValid);

        var managed = GetLauncherManagedConfiguration(result);
        Assert.Equal(2, managed["titan_launcher_schema_version"]!.GetValue<int>());
        Assert.Equal(5, managed["titan_grid_columns"]!.GetValue<int>());
        Assert.Equal(
            "https://cdn.empresa.com/launcher/fondo.webp",
            managed["titan_wallpaper_url"]!.GetValue<string>());

        var profileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var publicationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        AndroidPolicyCompiler.StampLauncherPublication(
            result.GooglePolicy!,
            profileId,
            publicationId,
            7,
            new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(profileId.ToString("D"), managed["titan_profile_id"]!.GetValue<string>());
        Assert.Equal(publicationId.ToString("D"), managed["titan_publication_id"]!.GetValue<string>());
        Assert.Equal(7, managed["titan_config_version"]!.GetValue<int>());
        Assert.DoesNotContain("base64", result.GooglePolicy!.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://cdn.empresa.com/fondo.png")]
    [InlineData("file:///tmp/fondo.png")]
    [InlineData("https://10.0.0.8/fondo.png")]
    [InlineData("https://127.0.0.1/logo.png")]
    [InlineData("https://user:pass@cdn.empresa.com/logo.png")]
    [InlineData("data:image/png;base64,AAAA")]
    public void Launcher_RejectsUnsafeResourceUrls(string url)
    {
        var configuration = new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["launcher"] = new JsonObject
                {
                    ["enabled"] = true,
                    ["wallpaperUrl"] = url
                }
            }
        };

        var result = _compiler.Compile(configuration.ToJsonString());
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Launcher_RejectsGridColumnsOutOfRange(int columns)
    {
        var configuration = new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["launcher"] = new JsonObject
                {
                    ["enabled"] = true,
                    ["gridColumns"] = columns
                }
            }
        };

        var result = _compiler.Compile(configuration.ToJsonString());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("gridColumns", StringComparison.Ordinal));
    }

    [Fact]
    public void Launcher_AcceptsPrivateAssetTokenAndRejectsThemeOutOfRange()
    {
        var valid = new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["launcher"] = new JsonObject
                {
                    ["enabled"] = true,
                    ["schemaVersion"] = 2,
                    ["wallpaperUrl"] = "titan-asset:11111111-1111-1111-1111-111111111111",
                    ["iconSize"] = 64
                }
            }
        };
        var compiled = _compiler.Compile(valid.ToJsonString());
        Assert.True(compiled.IsValid);
        Assert.Equal(
            "titan-asset:11111111-1111-1111-1111-111111111111",
            GetLauncherManagedConfiguration(compiled)["titan_wallpaper_url"]!.GetValue<string>());

        var invalid = new JsonObject
        {
            ["android"] = new JsonObject
            {
                ["launcher"] = new JsonObject
                {
                    ["enabled"] = true,
                    ["iconSize"] = 10
                }
            }
        };
        Assert.False(_compiler.Compile(invalid.ToJsonString()).IsValid);
    }

    [Fact]
    public void Launcher_CompilesValidatedBrandingColors()
    {
        const string configuration =
            """
            {
              "android": {
                "launcher": {
                  "enabled": true,
                  "schemaVersion": 1,
                  "primaryColor": "#12aBcF",
                  "backgroundColor": "#102030"
                }
              }
            }
            """;

        var result = _compiler.Compile(configuration);

        Assert.True(result.IsValid);

        var managedConfiguration =
            GetLauncherManagedConfiguration(result);

        Assert.Equal(
            "#12aBcF",
            managedConfiguration["titan_primary_color"]!.GetValue<string>());
        Assert.Equal(
            "#102030",
            managedConfiguration["titan_background_color"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("123456")]
    [InlineData("#12345G")]
    public void Launcher_RejectsInvalidBrandingColors(
        string color)
    {
        var configuration =
            new JsonObject
            {
                ["android"] =
                    new JsonObject
                    {
                        ["launcher"] =
                            new JsonObject
                            {
                                ["enabled"] = true,
                                ["primaryColor"] = color
                            }
                    }
            };

        var result =
            _compiler.Compile(
                configuration.ToJsonString());

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("#RRGGBB", StringComparison.Ordinal));
    }

    [Fact]
    public void Kiosk_AcceptsCurrentFrontendAndLegacyFieldNames()
    {
        const string currentConfiguration =
            """
            {
              "android": {
                "kiosk": {
                  "enabled": true,
                  "applicationId": "com.acme.kiosk",
                  "statusBar": false,
                  "keyguard": true
                }
              }
            }
            """;

        const string compatibleConfiguration =
            """
            {
              "android": {
                "kiosk": {
                  "enabled": true,
                  "applicationId": "com.acme.kiosk",
                  "statusBar": true,
                  "disableStatusBar": false,
                  "keyguard": false,
                  "disableKeyguard": true
                }
              }
            }
            """;

        var current =
            Assert.IsType<JsonObject>(
                _compiler.Compile(currentConfiguration).GooglePolicy);

        Assert.False(
            current["statusBarDisabled"]!.GetValue<bool>());
        Assert.True(
            current["keyguardDisabled"]!.GetValue<bool>());

        var compatible =
            Assert.IsType<JsonObject>(
                _compiler.Compile(compatibleConfiguration).GooglePolicy);

        Assert.False(
            compatible["statusBarDisabled"]!.GetValue<bool>());
        Assert.True(
            compatible["keyguardDisabled"]!.GetValue<bool>());
    }

    private static JsonObject CreateLauncherConfiguration(
        string property,
        string value)
    {
        var launcher =
            new JsonObject
            {
                ["enabled"] = true
            };

        var application =
            new JsonObject
            {
                ["packageName"] = "com.acme.app",
                ["installType"] = "AVAILABLE"
            };

        if (property == "label")
        {
            application[property] = value;
        }
        else
        {
            launcher[property] = value;
        }

        return new JsonObject
        {
            ["android"] =
                new JsonObject
                {
                    ["launcher"] = launcher,
                    ["applications"] =
                        new JsonObject
                        {
                            ["packages"] =
                                new JsonArray
                                {
                                    application
                                }
                        }
                }
        };
    }

    private static JsonObject GetLauncherManagedConfiguration(
        TitanMDM.Application.Android.Policies.AndroidPolicyCompilationResult result)
    {
        var policy =
            Assert.IsType<JsonObject>(result.GooglePolicy);

        var applications =
            Assert.IsType<JsonArray>(policy["applications"]);

        var agent = applications
            .OfType<JsonObject>()
            .Single(application =>
                application["packageName"]!.GetValue<string>() ==
                "com.titanmdm.agent");

        return Assert.IsType<JsonObject>(
            agent["managedConfiguration"]);
    }
}
