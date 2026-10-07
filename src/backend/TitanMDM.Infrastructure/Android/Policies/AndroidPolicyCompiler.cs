using System.Text.Json;
using System.Text.Json.Nodes;
using TitanMDM.Application.Android.Policies;

namespace TitanMDM.Infrastructure.Android.Policies;

public sealed class AndroidPolicyCompiler
    : IAndroidPolicyCompiler
{
    public AndroidPolicyCompilationResult Compile(
        string configurationJson)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return AndroidPolicyCompilationResult.Failure(
                new[]
                {
                    "La configuración Android está vacía."
                });
        }

        JsonObject root;

        try
        {
            root =
                JsonNode.Parse(configurationJson)
                    as JsonObject
                ?? throw new JsonException(
                    "La raíz debe ser un objeto JSON.");
        }
        catch (JsonException exception)
        {
            return AndroidPolicyCompilationResult.Failure(
                new[]
                {
                    $"JSON Android inválido: {exception.Message}"
                });
        }

        var android = root["android"] as JsonObject;

        if (android is null)
        {
            return AndroidPolicyCompilationResult.Failure(
                new[]
                {
                    "La configuración no contiene el objeto 'android'."
                });
        }

        var googlePolicy = new JsonObject
        {
            ["statusReportingSettings"] =
                new JsonObject
                {
                    ["applicationReportsEnabled"] = true,
                    ["deviceSettingsEnabled"] = true,
                    ["softwareInfoEnabled"] = true,
                    ["memoryInfoEnabled"] = true,
                    ["networkInfoEnabled"] = true,
                    ["displayInfoEnabled"] = true,
                    ["powerManagementEventsEnabled"] = true,
                    ["hardwareStatusEnabled"] = true,
                    ["systemPropertiesEnabled"] = true,
                    ["applicationReportingSettings"] =
                        new JsonObject
                        {
                            ["includeRemovedApps"] = true
                        }
                }
        };

        CompilePassword(
            android,
            googlePolicy,
            errors);

        CompileRestrictions(
            android,
            googlePolicy);

        CompileNetwork(
            android,
            googlePolicy);

        CompileLocation(
            android,
            googlePolicy,
            warnings);

        CompileSystemUpdate(
            android,
            googlePolicy,
            errors);

        CompileKiosk(
            android,
            googlePolicy,
            errors);

        CompileApplications(
            android,
            googlePolicy,
            errors);

        if (errors.Count > 0)
        {
            return AndroidPolicyCompilationResult.Failure(
                errors);
        }

        return AndroidPolicyCompilationResult.Success(
            googlePolicy,
            warnings);
    }

    private static void CompilePassword(
        JsonObject android,
        JsonObject policy,
        ICollection<string> errors)
    {
        var password =
            android["password"] as JsonObject;

        if (password is null ||
            !GetBool(password, "enabled"))
        {
            return;
        }

        var minimumLength =
            GetInt(password, "minimumLength", 6);

        if (minimumLength is < 4 or > 32)
        {
            errors.Add(
                "Android: minimumLength debe estar entre 4 y 32.");
            return;
        }

        var complexity =
            GetString(
                password,
                "complexity",
                GetBool(password, "requireComplex")
                    ? "HIGH"
                    : GetBool(password, "requireNumeric")
                        ? "MEDIUM"
                        : "LOW");

        complexity =
            complexity.ToUpperInvariant();

        if (complexity is not
            ("LOW" or "MEDIUM" or "HIGH"))
        {
            errors.Add(
                "Android: complexity debe ser LOW, MEDIUM o HIGH.");
            return;
        }

        var passwordRequirements =
            new JsonObject
            {
                ["passwordMinimumLength"] =
                    minimumLength,

                ["passwordQuality"] =
                    complexity switch
                    {
                        "HIGH" =>
                            "COMPLEX",

                        "MEDIUM" =>
                            "NUMERIC_COMPLEX",

                        _ =>
                            "SOMETHING"
                    },

                ["passwordScope"] =
                    "SCOPE_DEVICE"
            };

        var maxFailedAttempts =
            GetInt(
                password,
                "maxFailedAttempts",
                0);

        if (maxFailedAttempts > 0)
        {
            passwordRequirements[
                "maximumFailedPasswordsForWipe"] =
                maxFailedAttempts;
        }

        policy["passwordPolicies"] =
        new JsonArray
        {
            passwordRequirements
        };

        var timeoutMinutes =
            GetInt(
                password,
                "screenLockTimeoutMinutes",
                0);

        if (timeoutMinutes > 0)
        {
            policy["maximumTimeToLock"] =
                timeoutMinutes * 60_000L;
        }
    }

    private static void CompileRestrictions(
        JsonObject android,
        JsonObject policy)
    {
        var restrictions =
            android["restrictions"]
                as JsonObject;

        if (restrictions is null)
            return;

        if (GetBool(
        restrictions,
        "blockCamera"))
        {
    policy["cameraAccess"] =
        "CAMERA_ACCESS_DISABLED";
        }

        if (GetBool(
                restrictions,
                "blockScreenCapture"))
        {
            policy["screenCaptureDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockUsbFileTransfer"))
        {
            policy["usbFileTransferDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockBluetooth"))
        {
            policy["bluetoothDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockUnknownSources"))
        {
            var advancedSecurityOverrides =
    policy["advancedSecurityOverrides"] as JsonObject
    ?? new JsonObject();

        advancedSecurityOverrides["untrustedAppsPolicy"] =
            GetBool(
                restrictions,
                "blockUnknownSources")
                ? "DISALLOW_INSTALL"
                : "ALLOW_INSTALL_DEVICE_WIDE";

        policy["advancedSecurityOverrides"] =
            advancedSecurityOverrides;
        }

        if (GetBool(
                restrictions,
                "blockDebugging"))
        {
            policy[
                "debuggingFeaturesAllowed"] =
                false;
        }

        if (GetBool(
                restrictions,
                "blockFactoryReset"))
        {
            policy[
                "factoryResetDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockSafeBoot"))
        {
            policy[
                "safeBootDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockAddUser"))
        {
            policy[
                "addUserDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockRemoveUser"))
        {
            policy[
                "removeUserDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockModifyAccounts"))
        {
            policy[
                "modifyAccountsDisabled"] =
                true;
        }

        if (GetBool(
                restrictions,
                "blockPrinting"))
        {
            policy[
                "printingPolicy"] =
                "BLOCKED";
        }

        if (GetBool(
                restrictions,
                "blockMicrophone"))
        {
            policy[
                "microphoneAccess"] =
                "DISALLOW";
        }
    }

    private static void CompileNetwork(
        JsonObject android,
        JsonObject policy)
    {
        var network =
            android["network"]
                as JsonObject;

        if (network is null)
            return;

        if (GetBool(
                network,
                "wifiConfigDisabled"))
        {
            policy[
                "wifiConfigDisabled"] =
                true;
        }

        if (GetBool(
                network,
                "bluetoothConfigDisabled"))
        {
            policy[
                "bluetoothConfigDisabled"] =
                true;
        }

        if (GetBool(
                network,
                "tetheringDisabled"))
        {
            policy[
                "tetheringConfigDisabled"] =
                true;
        }

        if (GetBool(
                network,
                "vpnConfigDisabled"))
        {
            policy[
                "vpnConfigDisabled"] =
                true;
        }

        var privateDnsMode =
    GetString(
        network,
        "privateDnsMode",
        "UNSPECIFIED");

if (!string.Equals(
        privateDnsMode,
        "UNSPECIFIED",
        StringComparison.OrdinalIgnoreCase))
{
    var deviceConnectivityManagement =
        policy["deviceConnectivityManagement"] as JsonObject
        ?? new JsonObject();

    var privateDnsSettings =
        new JsonObject();

    switch (privateDnsMode.ToUpperInvariant())
    {
        case "OPPORTUNISTIC":
            privateDnsSettings["privateDnsMode"] =
                "PRIVATE_DNS_AUTOMATIC";
            break;

        case "OFF":
            // La API actual no ofrece un modo "OFF".
            // USER_CHOICE es la traducción segura del contrato
            // TitanMDM actual mientras ampliamos el editor.
            privateDnsSettings["privateDnsMode"] =
                "PRIVATE_DNS_USER_CHOICE";
            break;

        default:
            privateDnsSettings["privateDnsMode"] =
                "PRIVATE_DNS_USER_CHOICE";
            break;
    }

    deviceConnectivityManagement["privateDnsSettings"] =
        privateDnsSettings;

    policy["deviceConnectivityManagement"] =
        deviceConnectivityManagement;
}
    }

    private static void CompileLocation(
        JsonObject android,
        JsonObject policy,
        ICollection<string> warnings)
    {
        var location =
            android["location"]
                as JsonObject;

        if (location is null)
            return;

        var mode =
            GetString(
                location,
                "mode",
                string.Empty)
                .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(mode))
            return;

        switch (mode)
        {
            case "ENFORCED":
                policy[
                    "locationMode"] =
                    "HIGH_ACCURACY";
                break;

            case "DISABLED":
                policy[
                    "locationMode"] =
                    "LOCATION_USER_CHOICE";
                warnings.Add(
                    "La administración moderna de ubicación Android depende del modo de administración y versión del dispositivo; TitanMDM conservará el requisito para Compliance cuando Google no permita imponerlo directamente.");
                break;

            case "USER_CHOICE":
                policy[
                    "locationMode"] =
                    "LOCATION_USER_CHOICE";
                break;

            default:
                warnings.Add(
                    $"Modo de ubicación '{mode}' no reconocido. Se omitió del payload AMAPI.");
                break;
        }
    }

    private static void CompileSystemUpdate(
        JsonObject android,
        JsonObject policy,
        ICollection<string> errors)
    {
        var systemUpdate =
            android["systemUpdate"]
                as JsonObject;

        if (systemUpdate is null)
            return;

        var type =
            GetString(
                systemUpdate,
                "type",
                string.Empty)
                .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(type))
            return;

        if (type is not
            ("AUTOMATIC"
             or "WINDOWED"
             or "POSTPONE"))
        {
            errors.Add(
                "Android: systemUpdate.type debe ser AUTOMATIC, WINDOWED o POSTPONE.");
            return;
        }

        var payload =
            new JsonObject
            {
                ["type"] = type
            };

        if (type == "WINDOWED")
        {
            var startMinutes =
                GetInt(
                    systemUpdate,
                    "startMinutes",
                    -1);

            var endMinutes =
                GetInt(
                    systemUpdate,
                    "endMinutes",
                    -1);

            if (startMinutes is < 0 or > 1439 ||
                endMinutes is < 0 or > 1439)
            {
                errors.Add(
                    "Android: la ventana de actualización debe utilizar minutos entre 0 y 1439.");
                return;
            }

            payload["startMinutes"] =
                startMinutes;

            payload["endMinutes"] =
                endMinutes;
        }

        policy["systemUpdate"] =
            payload;
    }

    private static void CompileKiosk(
        JsonObject android,
        JsonObject policy,
        ICollection<string> errors)
    {
        var kiosk =
            android["kiosk"] as JsonObject;

        if (kiosk is null ||
            !GetBool(kiosk, "enabled"))
        {
            return;
        }

        var applicationId =
            GetString(
                kiosk,
                "applicationId",
                string.Empty);

        if (string.IsNullOrWhiteSpace(
                applicationId))
        {
            errors.Add(
                "Android: Kiosk requiere applicationId.");
            return;
        }

        policy["applications"] =
            new JsonArray
            {
                new JsonObject
                {
                    ["packageName"] =
                        applicationId,

                    ["installType"] =
                        "KIOSK"
                }
            };

        policy[
            "keyguardDisabled"] =
            GetBool(
                kiosk,
                "disableKeyguard",
                true);

        policy[
            "statusBarDisabled"] =
            GetBool(
                kiosk,
                "disableStatusBar",
                true);
    }

    private static void CompileApplications(
        JsonObject android,
        JsonObject policy,
        ICollection<string> errors)
    {
        var applications =
            android["applications"]
                as JsonObject;

        if (applications is null)
            return;

        var playStoreMode =
            GetString(
                applications,
                "playStoreMode",
                string.Empty)
                .ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(
                playStoreMode))
        {
            if (playStoreMode is not
                ("WHITELIST"
                 or "BLACKLIST"))
            {
                errors.Add(
                    "Android: playStoreMode debe ser WHITELIST o BLACKLIST.");
            }
            else
            {
                policy["playStoreMode"] =
                    playStoreMode;
            }
        }

        var packages =
            applications["packages"]
                as JsonArray;

        if (packages is null ||
            packages.Count == 0)
        {
            return;
        }

        var compiledApps =
            policy["applications"]
                as JsonArray
            ?? new JsonArray();

        foreach (var packageNode in packages)
        {
            if (packageNode is not
                JsonObject package)
            {
                continue;
            }

            var packageName =
                GetString(
                    package,
                    "packageName",
                    string.Empty);

            if (string.IsNullOrWhiteSpace(
                    packageName))
            {
                errors.Add(
                    "Android: una aplicación administrada no contiene packageName.");
                continue;
            }

            var installType =
                GetString(
                    package,
                    "installType",
                    "AVAILABLE")
                    .ToUpperInvariant();

            if (installType is not
                ("AVAILABLE"
                 or "FORCE_INSTALLED"
                 or "PREINSTALLED"
                 or "BLOCKED"
                 or "REQUIRED_FOR_SETUP"
                 or "KIOSK"))
            {
                errors.Add(
                    $"Android: installType '{installType}' no es válido para {packageName}.");
                continue;
            }

            var alreadyExists =
                compiledApps
                    .OfType<JsonObject>()
                    .Any(x =>
                        string.Equals(
                            GetString(
                                x,
                                "packageName",
                                string.Empty),
                            packageName,
                            StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
                continue;

            compiledApps.Add(
                new JsonObject
                {
                    ["packageName"] =
                        packageName,

                    ["installType"] =
                        installType
                });
        }

        if (compiledApps.Count > 0)
        {
            policy["applications"] =
                compiledApps;
        }
    }

    private static bool GetBool(
        JsonObject source,
        string property,
        bool defaultValue = false)
    {
        if (source[property] is not
            JsonValue value)
        {
            return defaultValue;
        }

        return value.TryGetValue<bool>(
            out var result)
            ? result
            : defaultValue;
    }

    private static int GetInt(
        JsonObject source,
        string property,
        int defaultValue)
    {
        if (source[property] is not
            JsonValue value)
        {
            return defaultValue;
        }

        return value.TryGetValue<int>(
            out var result)
            ? result
            : defaultValue;
    }

    private static string GetString(
        JsonObject source,
        string property,
        string defaultValue)
    {
        if (source[property] is not
            JsonValue value)
        {
            return defaultValue;
        }

        return value.TryGetValue<string>(
            out var result)
            ? result
            : defaultValue;
    }
}