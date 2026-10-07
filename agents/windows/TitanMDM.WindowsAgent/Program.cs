using Microsoft.Extensions.Options;
using TitanMDM.WindowsAgent;
using TitanMDM.WindowsAgent.Configuration;
using TitanMDM.WindowsAgent.Execution;
using TitanMDM.WindowsAgent.Interop;
using TitanMDM.WindowsAgent.Services;
using TitanMDM.WindowsAgent.Storage;

var builder =
    Host.CreateApplicationBuilder(
        args);

/*
 * ==============================================================
 * WINDOWS SERVICE
 * ==============================================================
 */

builder.Services
    .AddWindowsService(
        options =>
        {
            options.ServiceName =
                "TitanMDM Windows Agent";
        });

builder.Logging
    .AddEventLog(
        settings =>
        {
            settings.SourceName =
                "TitanMDM Windows Agent";
        });

/*
 * ==============================================================
 * CONFIGURATION
 * ==============================================================
 */

builder.Services
    .Configure<AgentOptions>(
        builder.Configuration
            .GetSection(
                AgentOptions.SectionName));

builder.Services
    .AddSingleton<
        AgentRuntimeSettingsStore>();

builder.Services
    .AddSingleton<
        AgentConfigurationBootstrapper>();

/*
 * ==============================================================
 * IDENTITY / LIFECYCLE
 * ==============================================================
 */

builder.Services
    .AddSingleton<
        DeviceIdentityStore>();

/*
 * Si estos servicios ya existen por el trabajo de R1-R8
 * se registran aquí una sola vez.
 */

builder.Services
    .AddSingleton<
        AgentLifecycleCoordinator>();

/*
 * ==============================================================
 * DIAGNOSTICS / SELF HEALING / RESILIENCE
 * ==============================================================
 */

builder.Services
    .AddSingleton<
        AgentDiagnosticsService>();

builder.Services
    .AddSingleton<
        AgentSelfHealingService>();

builder.Services
    .AddSingleton<
        AgentRetryPolicy>();

/*
 * ==============================================================
 * WINDOWS PROVIDERS
 * ==============================================================
 */

builder.Services
    .AddSingleton<
        WindowsDeviceInfoProvider>();

builder.Services
    .AddSingleton<
        WindowsInventoryProvider>();

builder.Services
    .AddSingleton<
        WindowsSecurityProvider>();

builder.Services
    .AddSingleton<
        WindowsComplianceProvider>();

builder.Services
    .AddSingleton<
        WindowsUpdateProvider>();

/*
 * ==============================================================
 * EXECUTION
 * ==============================================================
 */

builder.Services
    .AddSingleton<
        WindowsActionExecutor>();

builder.Services
    .AddSingleton<
        WindowsServiceManager>();

builder.Services
    .AddSingleton<
        WindowsScriptExecutor>();

builder.Services
    .AddHttpClient();

builder.Services
    .AddSingleton<
        WindowsSoftwarePackageDownloader>();

builder.Services
    .AddSingleton<
        WindowsSoftwareManager>();

builder.Services
    .AddSingleton<
        ICommandExecutor,
        CommandExecutor>();

builder.Services
    .AddSingleton<
        WindowsPolicyExecutor>();

builder.Services
    .AddSingleton<
        WindowsKioskExecutor>();

/*
 * ==============================================================
 * REMOTE SUPPORT
 * ==============================================================
 */

builder.Services
    .AddSingleton<
        RemoteSupportSessionManager>();

builder.Services
    .AddSingleton<
        RemoteDesktopHostLauncher>();

builder.Services
    .AddSingleton<
        ActiveSessionProcessLauncher>();

/*
 * ==============================================================
 * HTTP CLIENTS
 * ==============================================================
 */

builder.Services
    .AddHttpClient<
        EnrollmentService>(
            (
                serviceProvider,
                client) =>
            {
                var options =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<
                                AgentOptions>>()
                        .Value;

                ConfigureHttpClient(
                    client,
                    options);
            });

builder.Services
    .AddHttpClient<
        TitanMdmApiClient>(
            (
                serviceProvider,
                client) =>
            {
                var options =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<
                                AgentOptions>>()
                        .Value;

                ConfigureHttpClient(
                    client,
                    options);
            });

builder.Services
    .AddHttpClient<
        RemoteSupportApiClient>(
            (
                serviceProvider,
                client) =>
            {
                var options =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<
                                AgentOptions>>()
                        .Value;

                ConfigureHttpClient(
                    client,
                    options);
            });

/*
 * ==============================================================
 * BACKGROUND SERVICES
 * ==============================================================
 */

builder.Services
    .AddHostedService<
        Worker>();

builder.Services
    .AddHostedService<
        HeartbeatBackgroundService>();

builder.Services
    .AddHostedService<
        RemoteSupportBackgroundService>();

builder.Services
    .AddHostedService<
        AgentDiagnosticsBackgroundService>();

builder.Services
    .AddHostedService<
        AgentSelfHealingBackgroundService>();

/*
 * ==============================================================
 * BUILD
 * ==============================================================
 */

var host =
    builder.Build();

/*
 * ==============================================================
 * PRE-FLIGHT / RUNTIME BOOTSTRAP
 * ==============================================================
 */

var bootstrapper =
    host.Services
        .GetRequiredService<
            AgentConfigurationBootstrapper>();

await bootstrapper
    .InitializeAsync();

/*
 * Self-Healing inicial antes de comenzar servicios.
 */

var selfHealing =
    host.Services
        .GetRequiredService<
            AgentSelfHealingService>();

await selfHealing
    .RepairAsync();

/*
 * Snapshot inicial.
 */

var diagnostics =
    host.Services
        .GetRequiredService<
            AgentDiagnosticsService>();

await diagnostics
    .CaptureAsync();

await host.RunAsync();

/*
 * ==============================================================
 * HTTP
 * ==============================================================
 */

static void ConfigureHttpClient(
    HttpClient client,
    AgentOptions options)
{
    if (
        string.IsNullOrWhiteSpace(
            options.ServerUrl))
    {
        throw new InvalidOperationException(
            "TitanMDM ServerUrl no puede estar vacío.");
    }

    if (
        !Uri.TryCreate(
            options.ServerUrl,
            UriKind.Absolute,
            out var serverUri))
    {
        throw new InvalidOperationException(
            $"TitanMDM ServerUrl inválido: {options.ServerUrl}");
    }

    client.BaseAddress =
        serverUri;

    client.Timeout =
        TimeSpan.FromSeconds(
            Math.Clamp(
                options
                    .RequestTimeoutSeconds,
                10,
                300));
}