using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TitanMDM.Application.Automation;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Actions;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Automation.Enterprise.Events;
using TitanMDM.Infrastructure.Automation.Enterprise.Queue;
using TitanMDM.Infrastructure.Automation.Enterprise.Workers;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise;

/*
 * AUT-1 — Extensión de registro del motor empresarial.
 *
 * No modifica Program.cs, App.tsx, TitanMdmDbContext.cs,
 * InfrastructureServiceExtensions.cs ni TitanMdmSeeder.cs.
 * El registro se activa por la aplicación principal mediante:
 *
 *   builder.Services.AddTitanAutomationPlatform(configuration);
 *
 * y se documentan los puntos de integración en AUT1_ENTERPRISE_IMPLEMENTATION.md.
 */
public static class AutomationEnterpriseServiceCollectionExtensions
{
    public static IServiceCollection
        AddTitanAutomationPlatform(
            this IServiceCollection services,
            IConfiguration configuration)
    {
        services.Configure<AutomationEngineOptions>(
            configuration.GetSection(
                AutomationEngineOptions
                    .SectionName));

        services.AddScoped<
            IAutomationQueueService,
            AutomationQueueService>();

        services.AddScoped<
            IAutomationActionRegistry,
            AutomationActionRegistry>();

        services.AddScoped<
            IAutomationQueueProcessor,
            AutomationQueueProcessor>();

        services.AddScoped<
            IAutomationEventIngest,
            AutomationEventIngest>();

        services.AddScoped<
            IAutomationPlatformService,
            AutomationPlatformService>();

        services.AddHostedService<
            AutomationQueueWorker>();

        services.AddHostedService<
            AutomationPermissionSeeder>();

        return services;
    }
}
