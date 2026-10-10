using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

using TitanMDM.Api.AI;
using TitanMDM.Api.Hubs;
using TitanMDM.Api.Ponches;
using TitanMDM.Api.RemoteSupport;
using TitanMDM.Api.Security;
using TitanMDM.Api.Services;
using TitanMDM.Application.Commands;

using TitanMDM.Infrastructure.DependencyInjection;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Infrastructure.Persistence.Bootstrap;

var builder =
    WebApplication.CreateBuilder(args);

// ============================================================================
// ENVIRONMENT / CONFIGURATION
// ============================================================================

var environment =
    builder.Environment;

var configuration =
    builder.Configuration;

var isDevelopment =
    environment.IsDevelopment();

var applicationName =
    configuration["Application:Name"]
    ?? "TitanMDM";

var applicationVersion =
    configuration["Application:Version"]
    ?? "1.0.0";

var frontendUrl =
    configuration["Application:FrontendUrl"];

// ============================================================================
// DATA PROTECTION
// ============================================================================

var keyRingPath =
    configuration[
        "DataProtection:KeyRingPath"];

if (string.IsNullOrWhiteSpace(
        keyRingPath))
{
    keyRingPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .CommonApplicationData),
            "TitanMDM",
            "DataProtectionKeys");
}

Directory.CreateDirectory(
    keyRingPath);

builder.Services
    .AddDataProtection()
    .SetApplicationName(
        "TitanMDM")
    .PersistKeysToFileSystem(
        new DirectoryInfo(
            keyRingPath));

// ============================================================================
// MVC / API
// ============================================================================

builder.Services.AddControllers(
    options =>
    {
        options.Filters.Add<
            HelpdeskAudienceFilter>();
    });

if (isDevelopment)
{
    builder.Services.AddOpenApi();
}

// ============================================================================
// CACHE
//
// IMPORTANTE:
// TODA la configuraciÃ³n DI debe existir ANTES de builder.Build().
// ============================================================================

builder.Services.AddMemoryCache();

// ============================================================================
// SIGNALR
// ============================================================================

builder.Services.AddSignalR(
    options =>
    {
        options.MaximumReceiveMessageSize =
            configuration.GetValue<long?>(
                "SignalR:MaximumReceiveMessageSizeBytes")
            ??
            8L * 1024L * 1024L;

        options.EnableDetailedErrors =
            isDevelopment &&
            configuration.GetValue<bool>(
                "SignalR:EnableDetailedErrors");

        options.KeepAliveInterval =
            TimeSpan.FromSeconds(
                configuration.GetValue<int?>(
                    "SignalR:KeepAliveSeconds")
                ??
                10);

        options.ClientTimeoutInterval =
            TimeSpan.FromSeconds(
                configuration.GetValue<int?>(
                    "SignalR:ClientTimeoutSeconds")
                ??
                30);
    })
    .AddMessagePackProtocol();

// ============================================================================
// OPENROUTER / AI
// ============================================================================

builder.Services.AddTitanOpenRouter(
    configuration);

// ============================================================================
// TITANMDM INFRASTRUCTURE
// ============================================================================

builder.Services.AddTitanMdmInfrastructure(
    configuration);

builder.Services.AddTitanAuthorization();

// ============================================================================
// REMOTE SUPPORT / WINDOWS SERVICES
// ============================================================================

builder.Services.AddSingleton<
    RemoteSupportNotifier>();

builder.Services.AddSingleton<
    IDeviceCommandNotifier,
    DeviceCommandSignalRNotifier>();

builder.Services.AddHostedService<
    DeviceCommandExpirationService>();

builder.Services.AddSingleton<
    RemoteHostTokenService>();

builder.Services.Configure<
    WindowsAgentDistributionOptions>(
        configuration.GetSection(
            WindowsAgentDistributionOptions
                .SectionName));

builder.Services.AddSingleton<
    IWindowsAgentDistributionService,
    WindowsAgentDistributionService>();

builder.Services.AddScoped<
    SessionSecurityService>();

builder.Services.AddScoped<
    RbacAuditFilter>();

builder.Services.AddSingleton<
    RemoteSupportConnectionRegistry>();

builder.Services.AddScoped<
    RemoteSupportParticipantService>();

builder.Services.AddScoped<
    RemoteControlLeaseService>();

// ============================================================================
// HELPDESK INTELLIGENCE + BACKGROUND SERVICES
// ============================================================================

builder.Services.AddScoped<
    HelpdeskRequesterIntelligenceService>();

builder.Services.AddScoped<
    HelpdeskAiRoutingEnrichmentService>();

builder.Services.AddHostedService<
    HelpdeskMonitoringService>();

// Requester and AI enrichment are coordinated by HelpdeskRoutingWorker under
// the same persisted claim, before deterministic technician selection.

builder.Services.AddHostedService<
    HelpdeskRoutingWorker>();

builder.Services.AddHostedService<
    HelpdeskMailWorker>();

builder.Services.AddHostedService<
    HelpdeskOutboundEmailWorker>();



// ============================================================================
// CORS
// ============================================================================

var allowedOrigins =
    configuration
        .GetSection(
            "Cors:AllowedOrigins")
        .Get<string[]>()
    ??
    Array.Empty<string>();

builder.Services.AddCors(
    options =>
    {
        options.AddPolicy(
            "TitanMdmFrontend",
            policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    if (!isDevelopment)
                    {
                        throw new InvalidOperationException(
                            "Cors:AllowedOrigins debe contener al menos " +
                            "un origen en ambientes no Development.");
                    }

                    allowedOrigins =
                    [
                        "http://localhost:3020"
                    ];
                }

                policy
                    .WithOrigins(
                        allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
    });

// ============================================================================
// FORWARDED HEADERS
// IIS / REVERSE PROXY
// ============================================================================

builder.Services.Configure<
    ForwardedHeadersOptions>(
        options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders
                    .XForwardedFor
                |
                ForwardedHeaders
                    .XForwardedProto;
        });

// ============================================================================
// PONCHES / BIOMETRIC EDGE SERVICE
// ============================================================================

builder.Services
    .AddOptions<PonchesOptions>()
    .Bind(
        configuration.GetSection(
            PonchesOptions.SectionName));

// ----------------------------------------------------------------------------
// PONCHES CACHE
//
// El navegador nunca consulta directamente BioTime o Python.
//
// React
//   â†“
// TitanMDM API
//   â†“
// Memory Cache
//   â†“
// Ponches Gateway
//   â†“
// Python Edge
//   â†“
// BioTime / ZKTeco
//
// Operaciones de escritura NO deben reutilizar respuestas cacheadas.
// ----------------------------------------------------------------------------

builder.Services
    .AddOptions<PonchesCacheOptions>()
    .Bind(
        configuration.GetSection(
            PonchesCacheOptions.SectionName));

builder.Services.AddSingleton<
    IPonchesQueryCache,
    PonchesQueryCache>();

// ----------------------------------------------------------------------------
// PONCHES HTTP GATEWAY
// ----------------------------------------------------------------------------

builder.Services
    .AddHttpClient<
        IPonchesGateway,
        PonchesGateway>(
        client =>
        {
            /*
             * El timeout real es controlado
             * por PonchesGateway mediante
             * CancellationTokenSource.
             *
             * Esto permite tiempos diferentes
             * para:
             *
             * - consultas
             * - SQL
             * - sincronizaciÃ³n
             * - operaciones ZKTeco
             */
            client.Timeout =
                Timeout.InfiniteTimeSpan;

            client
                .DefaultRequestHeaders
                .UserAgent
                .ParseAdd(
                    "TitanMDM-PonchesGateway/1.0");
        });

// ============================================================================
// IMPORTANTE
//
// NO AGREGAR builder.Services DESPUÃ‰S DE ESTE PUNTO.
// ============================================================================

var app =
    builder.Build();

// ============================================================================
// DATABASE BOOTSTRAP
// ============================================================================

using (var scope =
       app.Services.CreateScope())
{
    var bootstrapper =
        scope
            .ServiceProvider
            .GetRequiredService<
                DatabaseBootstrapper>();

    await bootstrapper
        .BootstrapAsync();

}

// ============================================================================
// HTTP PIPELINE
// ============================================================================

app.UseForwardedHeaders();

if (!app.Environment
        .IsDevelopment())
{
    app.UseHsts();

    if (configuration.GetValue(
            "Security:RequireHttps",
            true))
    {
        app.UseHttpsRedirection();
    }
}

if (app.Environment
        .IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(
    "TitanMdmFrontend");

app.UseAuthentication();

app.UseAuthorization();

// ============================================================================
// API CONTROLLERS
// ============================================================================

app.MapControllers();

// ============================================================================
// SIGNALR
// ============================================================================

app.MapHub<
    RemoteSupportHub>(
        RemoteSupportHub.Route);

app.MapHub<
    DeviceCommandHub>(
        DeviceCommandHub.Route,
        options =>
        {
            options.CloseOnAuthenticationExpiration = true;
        });

// ============================================================================
// ROOT INFORMATION
// ============================================================================

app.MapGet(
    "/",
    () =>
    {
        return Results.Ok(
            new
            {
                application =
                    applicationName,

                service =
                    "TitanMDM.Api",

                version =
                    applicationVersion,

                environment =
                    environment
                        .EnvironmentName,

                status =
                    "Running",

                frontend =
                    frontendUrl,

                health =
                    "/api/health",

                readiness =
                    "/api/health/ready",

                liveness =
                    "/api/health/live",

                ponchesHealth =
                    "/api/ponches/health",

                windowsAgentPackage =
                    "/api/enrollment/windows/package",

                windowsInstaller =
                    "/api/enrollment/windows/installer",

                remoteSupportHub =
                    RemoteSupportHub.Route,

                deviceCommandHub =
                    DeviceCommandHub.Route,

                utc =
                    DateTime.UtcNow
            });
    });

// ============================================================================
// LIVENESS
// ============================================================================

app.MapGet(
    "/api/health/live",
    () =>
    {
        return Results.Ok(
            new
            {
                service =
                    "TitanMDM.Api",

                status =
                    "Alive",

                utc =
                    DateTime.UtcNow
            });
    });

// ============================================================================
// READINESS
// ============================================================================

app.MapGet(
    "/api/health/ready",
    async (
        TitanMdmDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var databaseAvailable =
                await dbContext
                    .Database
                    .CanConnectAsync(
                        cancellationToken);

            if (!databaseAvailable)
            {
                return Results.Json(
                    new
                    {
                        service =
                            "TitanMDM.Api",

                        status =
                            "Unhealthy",

                        database =
                            "Unavailable",

                        utc =
                            DateTime.UtcNow
                    },
                    statusCode:
                        StatusCodes
                            .Status503ServiceUnavailable);
            }

            return Results.Ok(
                new
                {
                    service =
                        "TitanMDM.Api",

                    status =
                        "Ready",

                    database =
                        "Available",

                    utc =
                        DateTime.UtcNow
                });
        }
        catch
        {
            return Results.Json(
                new
                {
                    service =
                        "TitanMDM.Api",

                    status =
                        "Unhealthy",

                    database =
                        "Unavailable",

                    utc =
                        DateTime.UtcNow
                },
                statusCode:
                    StatusCodes
                        .Status503ServiceUnavailable);
        }
    });

// ============================================================================
// GENERAL HEALTH
// ============================================================================

app.MapGet(
    "/api/health",
    async (
        TitanMdmDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        bool databaseAvailable;

        try
        {
            databaseAvailable =
                await dbContext
                    .Database
                    .CanConnectAsync(
                        cancellationToken);
        }
        catch
        {
            databaseAvailable =
                false;
        }

        var status =
            databaseAvailable
                ? "Healthy"
                : "Unhealthy";

        var response =
            new
            {
                service =
                    "TitanMDM.Api",

                status,

                database =
                    databaseAvailable
                        ? "Available"
                        : "Unavailable",

                signalR =
                    "Enabled",

                remoteSupport =
                    "Enabled",

                windowsAgentDistribution =
                    "Enabled",

                ponchesGateway =
                    "Enabled",

                memoryCache =
                    "Enabled",

                utc =
                    DateTime.UtcNow
            };

        return databaseAvailable
            ? Results.Ok(
                response)
            : Results.Json(
                response,
                statusCode:
                    StatusCodes
                        .Status503ServiceUnavailable);
    });

// ============================================================================
// RUN
// ============================================================================

app.Run();

// ============================================================================
// TEST HOST SUPPORT
// ============================================================================

public partial class Program
{
}




