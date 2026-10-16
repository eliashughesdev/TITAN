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

        CompileLauncher(
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
                string.Empty)
                .Trim();

        if (!IsValidAndroidPackageName(
                applicationId))
        {
            errors.Add(
                "Android: Kiosk requiere un applicationId válido.");
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
            GetCompatibleKioskBool(
                kiosk,
                "disableKeyguard",
                "keyguard",
                true);

        policy[
            "statusBarDisabled"] =
            GetCompatibleKioskBool(
                kiosk,
                "disableStatusBar",
                "statusBar",
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

        var packageNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

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
                    string.Empty)
                    .Trim();

            if (!IsValidAndroidPackageName(
                    packageName))
            {
                errors.Add(
                    $"Android: packageName '{packageName}' no es un nombre de paquete Android válido.");
                continue;
            }

            if (!packageNames.Add(packageName))
            {
                errors.Add(
                    $"Android: packageName duplicado '{packageName}'.");
                continue;
            }

            var label =
                GetApplicationLabel(
                    package,
                    packageName);

            if (label.Length > MaxLauncherLabelLength)
            {
                errors.Add(
                    $"Android: el label/displayName de {packageName} no puede exceder {MaxLauncherLabelLength} caracteres.");
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

    private const string DefaultLauncherPackage =
        "com.titanmdm.agent";

    private const string DefaultLauncherActivity =
        "com.titanmdm.agent.launcher.TitanLauncherActivity";

    private const int MinLauncherSchemaVersion = 1;

    private const int SupportedLauncherSchemaVersion = 2;

    private const int MaxLauncherApps = 100;

    private const int MaxLauncherLabelLength = 60;

    private const int MaxLauncherCompanyLength = 80;

    private const int MaxLauncherWelcomeLength = 180;

    /*
     * Titan Launcher multiaplicación.
     *
     * A diferencia del kiosk de una sola aplicación (installType=KIOSK),
     * el launcher empresarial:
     *   - se instala en el paquete del agente;
     *   - publica su managedConfiguration (empresa, mensaje y apps
     *     autorizadas) mediante claves APP_RESTRICTIONS;
     *   - se declara como HOME preferido con
     *     persistentPreferredActivities.
     *
     * Nunca se combina con kiosk de aplicación única.
     */
    private static void CompileLauncher(
        JsonObject android,
        JsonObject policy,
        ICollection<string> errors)
    {
        var launcher =
            android["launcher"] as JsonObject;

        if (launcher is null ||
            !GetBool(launcher, "enabled"))
        {
            return;
        }

        var kiosk =
            android["kiosk"] as JsonObject;

        if (kiosk is not null &&
            GetBool(kiosk, "enabled"))
        {
            errors.Add(
                "Android: no se puede combinar el kiosk de una aplicación con el launcher empresarial multiaplicación.");
            return;
        }

        if (HasKioskApplication(android))
        {
            errors.Add(
                "Android: una aplicación con installType KIOSK no se puede combinar con el launcher empresarial multiaplicación.");
            return;
        }

        var packageName =
            GetString(
                launcher,
                "packageName",
                DefaultLauncherPackage)
                .Trim();

        if (!IsValidAndroidPackageName(packageName))
        {
            errors.Add(
                "Android: el launcher requiere un packageName válido.");
            return;
        }

        var activityName =
            GetString(
                launcher,
                "activityName",
                DefaultLauncherActivity)
                .Trim();

        if (string.IsNullOrWhiteSpace(activityName))
        {
            errors.Add(
                "Android: el launcher requiere activityName.");
            return;
        }

        var schemaVersion =
            MinLauncherSchemaVersion;

        if (launcher.TryGetPropertyValue(
                "schemaVersion",
                out var schemaNode) &&
            (schemaNode is not JsonValue schemaValue ||
             !schemaValue.TryGetValue<int>(out schemaVersion) ||
             schemaVersion < MinLauncherSchemaVersion ||
             schemaVersion > SupportedLauncherSchemaVersion))
        {
            errors.Add(
                $"Android: schemaVersion del launcher debe estar entre {MinLauncherSchemaVersion} y {SupportedLauncherSchemaVersion}.");
            return;
        }

        var companyName =
            GetString(
                launcher,
                "companyName",
                "TitanMDM Enterprise")
                .Trim();

        if (companyName.Length >
            MaxLauncherCompanyLength)
        {
            errors.Add(
                $"Android: companyName del launcher no puede exceder {MaxLauncherCompanyLength} caracteres.");
            return;
        }

        if (companyName.Length == 0)
        {
            companyName =
                "TitanMDM Enterprise";
        }

        var welcomeMessage =
            GetString(
                launcher,
                "welcomeMessage",
                "Dispositivo corporativo")
                .Trim();

        if (welcomeMessage.Length >
            MaxLauncherWelcomeLength)
        {
            errors.Add(
                $"Android: welcomeMessage del launcher no puede exceder {MaxLauncherWelcomeLength} caracteres.");
            return;
        }

        if (welcomeMessage.Length == 0)
        {
            welcomeMessage =
                "Dispositivo corporativo";
        }

        if (!TryGetBrandingColor(
                launcher,
                "primaryColor",
                out var primaryColor) ||
            !TryGetBrandingColor(
                launcher,
                "backgroundColor",
                out var backgroundColor))
        {
            errors.Add(
                "Android: los colores del launcher deben utilizar el formato #RRGGBB.");
            return;
        }

        if (!TryGetLauncherResourceUrl(
                launcher,
                "wallpaperUrl",
                out var wallpaperUrl,
                out var wallpaperError))
        {
            errors.Add(
                wallpaperError ??
                "Android: la URL del recurso del launcher no es válida.");
            return;
        }

        if (!TryGetLauncherResourceUrl(
                launcher,
                "logoUrl",
                out var logoUrl,
                out var logoError))
        {
            errors.Add(
                logoError ??
                "Android: la URL del recurso del launcher no es válida.");
            return;
        }

        if (!TryGetGridColumns(
                launcher,
                out var gridColumns))
        {
            errors.Add(
                "Android: gridColumns del launcher debe ser 3, 4 o 5.");
            return;
        }

        if (!TryGetOptionalGuid(
                launcher,
                "profileId",
                out var profileId) ||
            !TryGetOptionalGuid(
                launcher,
                "publicationId",
                out var publicationId))
        {
            errors.Add(
                "Android: profileId y publicationId del launcher deben ser identificadores GUID.");
            return;
        }

        if (!TryGetOptionalPositiveInt(
                launcher,
                "configVersion",
                out var configVersion))
        {
            errors.Add(
                "Android: configVersion del launcher debe ser un entero positivo.");
            return;
        }

        if (!TryGetOptionalUtcTimestamp(
                launcher,
                "updatedAt",
                out var updatedAt))
        {
            errors.Add(
                "Android: updatedAt del launcher debe ser una fecha UTC ISO-8601.");
            return;
        }

        if (!TryGetTheme(launcher, out var theme, out var themeError))
        {
            errors.Add(themeError ?? "Android: el tema del launcher no es válido.");
            return;
        }

        var requiredForSetup =
            GetBool(launcher, "requiredForSetup", true);

        var installType =
            requiredForSetup
                ? "REQUIRED_FOR_SETUP"
                : "FORCE_INSTALLED";

        var compiledApps =
            policy["applications"] as JsonArray
            ?? new JsonArray();

        var allowedApps = new JsonArray();

        foreach (var node in compiledApps)
        {
            if (node is not JsonObject app)
                continue;

            var appPackage =
                GetString(
                    app,
                    "packageName",
                    string.Empty);

            if (string.IsNullOrWhiteSpace(appPackage) ||
                string.Equals(
                    appPackage,
                    packageName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var appInstallType =
                GetString(
                    app,
                    "installType",
                    "AVAILABLE")
                    .ToUpperInvariant();

            if (appInstallType == "BLOCKED")
                continue;

            if (appInstallType == "KIOSK")
            {
                errors.Add(
                    "Android: una aplicación con installType KIOSK no se puede combinar con el launcher empresarial multiaplicación.");
                continue;
            }

            var required =
                appInstallType is
                    "FORCE_INSTALLED" or
                    "REQUIRED_FOR_SETUP";

            allowedApps.Add(
                new JsonObject
                {
                    ["packageName"] = appPackage,
                    ["label"] =
                        GetManagedApplicationLabel(
                            android,
                            appPackage),
                    ["required"] = required
                });
        }

        if (allowedApps.Count > MaxLauncherApps)
        {
            errors.Add(
                $"Android: el launcher no puede contener más de {MaxLauncherApps} aplicaciones efectivas.");
        }

        if (errors.Count > 0)
            return;

        var agentEntry =
            compiledApps
                .OfType<JsonObject>()
                .FirstOrDefault(
                    x => string.Equals(
                        GetString(
                            x,
                            "packageName",
                            string.Empty),
                        packageName,
                        StringComparison.OrdinalIgnoreCase));

        if (agentEntry is null)
        {
            agentEntry =
                new JsonObject
                {
                    ["packageName"] = packageName
                };

            compiledApps.Add(agentEntry);
        }

        agentEntry["installType"] = installType;

        var managedConfiguration =
            new JsonObject
            {
                ["titan_company_name"] = companyName,
                ["titan_welcome_message"] = welcomeMessage,
                ["titan_launcher_enabled"] = true,
                ["titan_launcher_schema_version"] = schemaVersion,
                ["titan_allowed_apps"] = allowedApps.ToJsonString()
            };

        if (primaryColor is not null)
        {
            managedConfiguration["titan_primary_color"] =
                primaryColor;
        }

        if (backgroundColor is not null)
        {
            managedConfiguration["titan_background_color"] =
                backgroundColor;
        }

        managedConfiguration["titan_grid_columns"] =
            gridColumns;

        if (wallpaperUrl is not null)
        {
            managedConfiguration["titan_wallpaper_url"] =
                wallpaperUrl;
        }

        if (logoUrl is not null)
        {
            managedConfiguration["titan_logo_url"] =
                logoUrl;
        }

        if (profileId is not null)
        {
            managedConfiguration["titan_profile_id"] =
                profileId;
        }

        if (publicationId is not null)
        {
            managedConfiguration["titan_publication_id"] =
                publicationId;
        }

        if (configVersion is not null)
        {
            managedConfiguration["titan_config_version"] =
                configVersion;
        }

        if (updatedAt is not null)
        {
            managedConfiguration["titan_updated_at"] =
                updatedAt;
        }

        foreach (var entry in theme)
        {
            managedConfiguration[entry.Key] = entry.Value;
        }

        agentEntry["managedConfiguration"] =
            managedConfiguration;

        policy["applications"] = compiledApps;

        policy["persistentPreferredActivities"] =
            new JsonArray
            {
                new JsonObject
                {
                    ["receiverActivity"] =
                        $"{packageName}/{activityName}",

                    ["actions"] =
                        new JsonArray
                        {
                            "android.intent.action.MAIN"
                        },

                    ["categories"] =
                        new JsonArray
                        {
                            "android.intent.category.HOME"
                        }
                }
            };

        /*
         * El launcher multiaplicación no utiliza el kiosk launcher de
         * Google; se deja explícito para evitar ambigüedad.
         */
        policy["kioskCustomLauncherEnabled"] = false;
    }

    private static bool HasKioskApplication(
        JsonObject android)
    {
        var packages =
            android["applications"]?["packages"]
                as JsonArray;

        return packages?
            .OfType<JsonObject>()
            .Any(package =>
                string.Equals(
                    GetString(
                        package,
                        "installType",
                        string.Empty)
                        .Trim(),
                    "KIOSK",
                    StringComparison.OrdinalIgnoreCase))
            ?? false;
    }

    private static string GetManagedApplicationLabel(
        JsonObject android,
        string packageName)
    {
        var packages =
            android["applications"]?["packages"]
                as JsonArray;

        var package = packages?
            .OfType<JsonObject>()
            .FirstOrDefault(candidate =>
                string.Equals(
                    GetString(
                        candidate,
                        "packageName",
                        string.Empty)
                        .Trim(),
                    packageName,
                    StringComparison.OrdinalIgnoreCase));

        return package is null
            ? packageName
            : GetApplicationLabel(
                package,
                packageName);
    }

    private static string GetApplicationLabel(
        JsonObject package,
        string packageName)
    {
        var label =
            GetString(
                package,
                "label",
                string.Empty)
                .Trim();

        if (label.Length == 0)
        {
            label =
                GetString(
                    package,
                    "displayName",
                    string.Empty)
                    .Trim();
        }

        return label.Length == 0
            ? packageName
            : label;
    }

    public static void StampLauncherPublication(
        JsonNode googlePolicy,
        Guid profileId,
        Guid publicationId,
        int configVersion,
        DateTime updatedAtUtc)
    {
        if (profileId == Guid.Empty ||
            publicationId == Guid.Empty ||
            configVersion < 1 ||
            googlePolicy is not JsonObject policy ||
            policy["applications"] is not JsonArray applications)
        {
            return;
        }

        var managedConfiguration =
            applications
                .OfType<JsonObject>()
                .Select(application =>
                    application["managedConfiguration"] as JsonObject)
                .FirstOrDefault(configuration =>
                    configuration?.ContainsKey("titan_launcher_enabled") == true);

        if (managedConfiguration is null)
        {
            return;
        }

        managedConfiguration["titan_profile_id"] =
            profileId.ToString("D");
        managedConfiguration["titan_publication_id"] =
            publicationId.ToString("D");
        managedConfiguration["titan_config_version"] =
            configVersion;
        managedConfiguration["titan_updated_at"] =
            updatedAtUtc.ToUniversalTime().ToString("O");
    }

    public static IReadOnlyCollection<string> ReadLauncherResourceUrls(
        JsonNode googlePolicy)
    {
        if (googlePolicy is not JsonObject policy ||
            policy["applications"] is not JsonArray applications)
        {
            return [];
        }

        return applications
            .OfType<JsonObject>()
            .Select(application =>
                application["managedConfiguration"] as JsonObject)
            .Where(configuration => configuration is not null)
            .SelectMany(configuration => new[]
            {
                configuration!["titan_wallpaper_url"]?.GetValue<string>(),
                configuration["titan_logo_url"]?.GetValue<string>()
            })
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .ToArray();
    }

    private static bool TryGetTheme(
        JsonObject launcher,
        out Dictionary<string, JsonNode> theme,
        out string? error)
    {
        theme = [];
        error = null;

        if (!TryCopyColor(launcher, theme, "secondaryColor", "titan_secondary_color", out error) ||
            !TryCopyColor(launcher, theme, "textColor", "titan_text_color", out error) ||
            !TryCopyColor(launcher, theme, "cardColor", "titan_card_color", out error) ||
            !TryCopyColor(launcher, theme, "cardTextColor", "titan_card_text_color", out error) ||
            !TryCopyColor(launcher, theme, "buttonColor", "titan_button_color", out error))
        {
            return false;
        }

        if (!TryCopyRange(launcher, theme, "borderRadius", "titan_border_radius", 0, 32, out error) ||
            !TryCopyRange(launcher, theme, "cardOpacity", "titan_card_opacity", 0, 100, out error) ||
            !TryCopyRange(launcher, theme, "shadowIntensity", "titan_shadow", 0, 100, out error) ||
            !TryCopyRange(launcher, theme, "iconSize", "titan_icon_size", 40, 96, out error) ||
            !TryCopyRange(launcher, theme, "gridGap", "titan_grid_gap", 4, 32, out error))
        {
            return false;
        }

        if (!TryCopyChoice(
                launcher,
                theme,
                "imageFit",
                "titan_image_fit",
                ["cover", "contain", "center"],
                out error) ||
            !TryCopyChoice(
                launcher,
                theme,
                "themePreset",
                "titan_theme_preset",
                ["corporate", "dark", "custom"],
                out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryCopyColor(
        JsonObject launcher,
        Dictionary<string, JsonNode> theme,
        string property,
        string key,
        out string? error)
    {
        error = null;
        if (!TryGetBrandingColor(launcher, property, out var color))
        {
            error = $"Android: {property} del launcher debe utilizar el formato #RRGGBB.";
            return false;
        }

        if (color is not null)
        {
            theme[key] = color.ToUpperInvariant();
        }

        return true;
    }

    private static bool TryCopyRange(
        JsonObject launcher,
        Dictionary<string, JsonNode> theme,
        string property,
        string key,
        int minimum,
        int maximum,
        out string? error)
    {
        error = null;
        if (!launcher.TryGetPropertyValue(property, out var node) || node is null)
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<int>(out var number) ||
            number < minimum ||
            number > maximum)
        {
            error = $"Android: {property} del launcher está fuera de rango.";
            return false;
        }

        theme[key] = number;
        return true;
    }

    private static bool TryCopyChoice(
        JsonObject launcher,
        Dictionary<string, JsonNode> theme,
        string property,
        string key,
        string[] allowed,
        out string? error)
    {
        error = null;
        if (!launcher.TryGetPropertyValue(property, out var node) || node is null)
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var raw) ||
            !allowed.Contains(raw.Trim().ToLowerInvariant()))
        {
            error = $"Android: {property} del launcher no es un valor permitido.";
            return false;
        }

        theme[key] = raw.Trim().ToLowerInvariant();
        return true;
    }

    private static bool TryGetLauncherResourceUrl(
        JsonObject launcher,
        string property,
        out string? normalized,
        out string? error)
    {
        normalized = null;
        error = null;

        if (!launcher.TryGetPropertyValue(property, out var node) ||
            node is null)
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var raw))
        {
            error =
                $"Android: {property} del launcher debe ser una URL HTTPS.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (LauncherAssetStore.AssetToken(raw) is not null)
        {
            normalized = "titan-asset:" + LauncherAssetStore.AssetToken(raw);
            return true;
        }

        if (raw.Contains("base64", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            error =
                "Android: los recursos del launcher no se almacenan en Base64 dentro de la política.";
            return false;
        }

        if (!LauncherResourceUrlRules.TryValidate(
                raw,
                out normalized,
                out var validationError))
        {
            error =
                $"Android: {property} no es un recurso HTTPS autorizado. {validationError}";
            return false;
        }

        return true;
    }

    private static bool TryGetGridColumns(
        JsonObject launcher,
        out int gridColumns)
    {
        gridColumns = 4;

        if (!launcher.TryGetPropertyValue(
                "gridColumns",
                out var node) ||
            node is null)
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<int>(out gridColumns) ||
            gridColumns is not (3 or 4 or 5))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetOptionalGuid(
        JsonObject launcher,
        string property,
        out string? normalized)
    {
        normalized = null;

        if (!launcher.TryGetPropertyValue(property, out var node) ||
            node is null)
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var raw))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!Guid.TryParse(raw.Trim(), out var parsed) ||
            parsed == Guid.Empty)
        {
            return false;
        }

        normalized = parsed.ToString("D");
        return true;
    }

    private static bool TryGetOptionalPositiveInt(
        JsonObject launcher,
        string property,
        out int? parsed)
    {
        parsed = null;

        if (!launcher.TryGetPropertyValue(property, out var node) ||
            node is null)
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<int>(out var number) ||
            number < 1)
        {
            return false;
        }

        parsed = number;
        return true;
    }

    private static bool TryGetOptionalUtcTimestamp(
        JsonObject launcher,
        string property,
        out string? normalized)
    {
        normalized = null;

        if (!launcher.TryGetPropertyValue(property, out var node) ||
            node is null)
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var raw))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!DateTime.TryParse(
                raw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return false;
        }

        normalized = parsed.ToUniversalTime().ToString("O");
        return true;
    }

    private static bool TryGetBrandingColor(
        JsonObject launcher,
        string property,
        out string? color)
    {
        color = null;

        if (!launcher.TryGetPropertyValue(
                property,
                out var node))
        {
            return true;
        }

        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var rawColor))
        {
            return false;
        }

        color = rawColor.Trim();

        if (color.Length == 0)
        {
            color = null;
            return true;
        }

        if (color.Length != 7 ||
            color[0] != '#')
        {
            return false;
        }

        for (var index = 1;
             index < color.Length;
             index++)
        {
            var character = color[index];

            if (!((character >= '0' && character <= '9') ||
                  (character >= 'A' && character <= 'F') ||
                  (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidAndroidPackageName(
        string packageName)
    {
        var segments =
            packageName.Split('.');

        if (segments.Length < 2)
            return false;

        foreach (var segment in segments)
        {
            if (segment.Length == 0 ||
                !IsAsciiLetter(segment[0]))
            {
                return false;
            }

            for (var index = 1;
                 index < segment.Length;
                 index++)
            {
                var character = segment[index];

                if (!IsAsciiLetter(character) &&
                    !(character >= '0' && character <= '9') &&
                    character != '_')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(
        char character)
    {
        return (character >= 'A' && character <= 'Z') ||
               (character >= 'a' && character <= 'z');
    }

    private static bool GetCompatibleKioskBool(
        JsonObject kiosk,
        string legacyProperty,
        string currentProperty,
        bool defaultValue)
    {
        if (kiosk.ContainsKey(legacyProperty))
        {
            return GetBool(
                kiosk,
                legacyProperty,
                defaultValue);
        }

        return GetBool(
            kiosk,
            currentProperty,
            defaultValue);
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
