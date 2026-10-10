using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

using TitanMDM.Application.Android.Policies;
using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Applications;
using TitanMDM.Application.Audit;
using TitanMDM.Application.Automation;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Commands.Agent;
using TitanMDM.Application.Dashboard.Interfaces;
using TitanMDM.Application.Devices;
using TitanMDM.Application.Devices.Agent;
using TitanMDM.Application.Devices.Naming;
using TitanMDM.Application.Enrollment;
using TitanMDM.Application.Enrollment.DeviceRegistration;
using TitanMDM.Application.Groups;
using TitanMDM.Application.Helpdesk;
using TitanMDM.Application.Interfaces;
using TitanMDM.Application.Location;
using TitanMDM.Application.LostMode;
using TitanMDM.Application.Policies;
using TitanMDM.Application.Reports;
using TitanMDM.Application.Security;
using TitanMDM.Application.Sites;

using TitanMDM.Domain.Entities;

using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Android.Policies;
using TitanMDM.Infrastructure.Applications;
using TitanMDM.Infrastructure.Audit;
using TitanMDM.Infrastructure.Authentication;
using TitanMDM.Infrastructure.Automation;
using TitanMDM.Infrastructure.Commands;
using TitanMDM.Infrastructure.Dashboard;
using TitanMDM.Infrastructure.Devices;
using TitanMDM.Infrastructure.Devices.Agent;
using TitanMDM.Infrastructure.Enrollment;
using TitanMDM.Infrastructure.Groups;
using TitanMDM.Infrastructure.Helpdesk;
using TitanMDM.Infrastructure.Location;
using TitanMDM.Infrastructure.LostMode;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Infrastructure.Persistence.Bootstrap;
using TitanMDM.Infrastructure.Persistence.Seed;
using TitanMDM.Infrastructure.Policies;
using TitanMDM.Infrastructure.Reports;
using TitanMDM.Infrastructure.Security;
using TitanMDM.Infrastructure.Sites;

namespace TitanMDM.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection
        AddTitanMdmInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
    {
        // ============================================================
        // DATABASE
        // ============================================================

        var connectionString =
            configuration
                .GetConnectionString(
                    "TitanMdmDatabase");

        if (
            string.IsNullOrWhiteSpace(
                connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'TitanMdmDatabase' was not found.");
        }

        services
            .AddOptions<DatabaseOptions>()
            .Bind(
                configuration.GetSection(
                    DatabaseOptions
                        .SectionName));

        var databaseOptions =
            configuration
                .GetSection(
                    DatabaseOptions
                        .SectionName)
                .Get<DatabaseOptions>()
            ??
            new DatabaseOptions();

        services
            .AddDbContext<
                TitanMdmDbContext>(
                    options =>
                    {
                        options.UseSqlServer(
                            connectionString,
                            sqlOptions =>
                            {
                                sqlOptions
                                    .CommandTimeout(
                                        Math.Max(
                                            30,
                                            databaseOptions
                                                .CommandTimeoutSeconds));

                                sqlOptions
                                    .EnableRetryOnFailure(
                                        maxRetryCount:
                                            Math.Max(
                                                0,
                                                databaseOptions
                                                    .MaxRetryCount),

                                        maxRetryDelay:
                                            TimeSpan
                                                .FromSeconds(
                                                    Math.Max(
                                                        1,
                                                        databaseOptions
                                                            .MaxRetryDelaySeconds)),

                                        errorNumbersToAdd:
                                            null);
                            });
                    });

        // ============================================================
        // JWT
        // ============================================================

        services.Configure<
            JwtOptions>(
                configuration.GetSection(
                    JwtOptions
                        .SectionName));

        var jwtOptions =
            configuration
                .GetSection(
                    JwtOptions
                        .SectionName)
                .Get<JwtOptions>()
            ??
            throw new InvalidOperationException(
                "JWT configuration was not found.");

        if (
            string.IsNullOrWhiteSpace(
                jwtOptions.SigningKey)
            ||
            jwtOptions.SigningKey.Length <
                32)
        {
            throw new InvalidOperationException(
                "JWT signing key must contain at least 32 characters.");
        }

        services
            .AddAuthentication(
                JwtBearerDefaults
                    .AuthenticationScheme)
            .AddJwtBearer(
                options =>
                {
                    options
                        .TokenValidationParameters =
                            new TokenValidationParameters
                            {
                                ValidateIssuer =
                                    true,

                                ValidIssuer =
                                    jwtOptions.Issuer,

                                ValidateAudience =
                                    true,

                                ValidAudience =
                                    jwtOptions.Audience,

                                ValidateIssuerSigningKey =
                                    true,

                                IssuerSigningKey =
                                    new SymmetricSecurityKey(
                                        Encoding.UTF8
                                            .GetBytes(
                                                jwtOptions
                                                    .SigningKey)),

                                ValidateLifetime =
                                    true,

                                ClockSkew =
                                    TimeSpan
                                        .FromSeconds(
                                            30)
                            };

                    options.Events =
                        new JwtBearerEvents
                        {
                            OnMessageReceived =
                                context =>
                                {
                                    var accessToken =
                                        context
                                            .Request
                                            .Query[
                                                "access_token"]
                                            .FirstOrDefault();

                                    var path =
                                        context
                                            .HttpContext
                                            .Request
                                            .Path;

                                    if (
                                        !string.IsNullOrWhiteSpace(
                                            accessToken) &&
                                        (path.StartsWithSegments(
                                            "/hubs/remote-support") ||
                                         path.StartsWithSegments(
                                            "/hubs/device-commands")))
                                    {
                                        context.Token =
                                            accessToken;
                                    }

                                    return Task
                                        .CompletedTask;
                                }
                        };
                });

        services
            .AddAuthorization();

        // ============================================================
        // IDENTITY / AUTHENTICATION
        // ============================================================

        services.AddScoped<
                    IPasswordHasher<User>,
                    PasswordHasher<User>>();

        services.AddScoped<
            ITokenService,
            JwtTokenService>();

        services.AddScoped<
            IAuthenticationService,
            AuthenticationService>();

        // ============================================================
        // DASHBOARD
        // ============================================================

        services.AddScoped<
            IDashboardService,
            DashboardService>();

        // ============================================================
        // ENROLLMENT
        // ============================================================

        services.AddScoped<
            IEnrollmentService,
            EnrollmentService>();

        services.AddScoped<
            IDeviceRegistrationService,
            DeviceRegistrationService>();

        // ============================================================
        // DEVICES
        // ============================================================

        services.AddScoped<
            IDeviceAuthenticator,
            DeviceAuthenticator>();

        services.AddScoped<
            IDeviceAgentService,
            DeviceAgentService>();

        services.AddScoped<
            IDeviceQueryService,
            DeviceQueryService>();

        services.AddScoped<
            WindowsInventoryResultProcessor>();

        // ============================================================
        // CORPORATE DEVICE NAMING
        // ============================================================

        services
            .AddOptions<
                DeviceNamingOptions>()
            .Bind(
                configuration.GetSection(
                    DeviceNamingOptions
                        .SectionName));

        services.AddScoped<
            DeviceNamingResolver>();

        // ============================================================
        // SITES
        // ============================================================

        services.AddScoped<
            ISiteService,
            SiteService>();

        services.AddScoped<
            ISiteAssignmentService,
            SiteAssignmentService>();

        services.AddScoped<
            ISiteOperationsService,
            SiteOperationsService>();

        // ============================================================
        // COMMAND ENGINE
        // ============================================================

        services.AddSingleton<
            IDeviceCommandNotifier,
            NullDeviceCommandNotifier>();

        services.AddScoped<
            IDeviceCommandService,
            DeviceCommandService>();

        services.AddScoped<
            IDeviceCommandAgentService,
            DeviceCommandAgentService>();

        // ============================================================
        // APPLICATION INVENTORY
        // ============================================================

        services.AddScoped<
            IApplicationInventoryService,
            ApplicationInventoryService>();

        services.AddScoped<
            ISoftwareDeploymentService,
            SoftwareDeploymentService>();

        // ============================================================
        // POLICY ENGINE
        // ============================================================

        services.AddScoped<
            IPolicyService,
            PolicyService>();

        // ============================================================
        // ANDROID MANAGEMENT
        // ============================================================

        services
            .AddOptions<
                AndroidManagementOptions>()
            .Bind(
                configuration.GetSection(
                    AndroidManagementOptions
                        .SectionName));

        services.AddSingleton<
            IGoogleAndroidAccessTokenProvider,
            GoogleAndroidAccessTokenProvider>();

        services.AddHttpClient<
            AndroidManagementClient>();

        services.AddScoped<
            IAndroidEnterpriseService,
            AndroidEnterpriseService>();

        services.AddScoped<
            IAndroidDeviceSyncService,
            AndroidDeviceSyncService>();

        services.AddScoped<
            IAndroidPolicyCompiler,
            AndroidPolicyCompiler>();

        services.AddScoped<
            IAndroidPolicyPublisher,
            AndroidPolicyPublisher>();

        services.AddScoped<
            IAndroidPolicyAssignmentService,
            AndroidPolicyAssignmentService>();

        // ============================================================
        // SECURITY / COMPLIANCE
        // ============================================================

        services.AddScoped<
            ISecurityPostureService,
            SecurityPostureService>();

        // ============================================================
        // DEVICE GROUPS
        // ============================================================

        services.AddScoped<
            IDeviceGroupService,
            DeviceGroupService>();

        // ============================================================
        // AUTOMATION
        // ============================================================

        services.AddScoped<
            IAutomationService,
            AutomationService>();

        services.AddScoped<
            IAutomationEventDispatcher,
            AutomationEventDispatcher>();

        // ============================================================
        // LOCATION / LOST MODE
        // ============================================================

        services.AddScoped<
            IDeviceLocationService,
            DeviceLocationService>();

        services.AddScoped<
            ILostModeService,
            LostModeService>();

        services.AddHostedService<
            DeviceOfflineMonitor>();

        // ============================================================
        // AUDIT / REPORTS
        // ============================================================

        services.AddScoped<
            IAuditQueryService,
            AuditQueryService>();

        services.AddScoped<
            IAdministrativeAuditWriter,
            AdministrativeAuditWriter>();

        services.AddScoped<
            IReportsService,
            ReportsService>();

        // ============================================================
        // HELPDESK / ENTRA / MICROSOFT GRAPH MAIL
        // ============================================================

        services.AddHttpClient(
            "entra-id",
            client =>
            {
                client.Timeout =
                    TimeSpan.FromSeconds(
                        60);

                client.DefaultRequestHeaders
                    .UserAgent
                    .ParseAdd(
                        "TitanMDM-Helpdesk/1.0");
            });

        // ============================================================
        // HELPDESK CORE
        // ============================================================

        /*
         * Generador empresarial de nÃºmeros:
         *
         * HD-1
         * HD-2
         * HD-3
         * ...
         *
         * HelpdeskService depende directamente de este servicio.
         */
        services.AddScoped<
              HelpdeskTicketNumberGenerator>();

        services.AddScoped<
            IHelpdeskService,
            HelpdeskService>();

        services.AddScoped<
            HelpdeskMailIntakePolicy>();

        // ============================================================
        // MICROSOFT ENTRA DIRECTORY
        // ============================================================

        services.AddScoped<
            IEntraIdDirectoryService,
            EntraIdDirectoryService>();

        // ============================================================
        // HELPDESK MAIL IMPORT
        // ============================================================

        /*
         * Importador principal:
         *
         * email
         *   -> ticket nuevo
         *   -> comentario de thread existente
         */
        services.AddScoped<
            HelpdeskEmailImportService>();

        /*
         * Importador de adjuntos.
         *
         * Requerido por HelpdeskMailWorker.
         */
        services.AddScoped<
            HelpdeskEmailAttachmentImportService>();

        // ============================================================
        // AUTHORIZATION / SCOPES
        // ============================================================

        services.AddScoped<
            IAuthorizationScopeService,
            AuthorizationScopeService>();

        services.AddScoped<
            IScopeAccessService,
            ScopeAccessService>();
        // ============================================================
        // DATABASE BOOTSTRAP
        // ============================================================

        services.AddScoped<
            TitanMdmSeeder>();

        services.AddScoped<
            DatabaseBootstrapper>();

        return services;
    }
}


