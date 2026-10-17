using Microsoft.Extensions.Options;

using TitanMDM.Api.AI.Assistant;
using TitanMDM.Application.Assistant;
using TitanMDM.Infrastructure.Assistant;
using TitanMDM.Infrastructure.Assistant.Tools;

namespace TitanMDM.Api.AI.Assistant;

/// <summary>
/// Registra el núcleo del asistente empresarial (herramientas
/// de lectura, sesiones, orquestador y proveedores). Se invoca
/// desde la extensión de IA existente para no modificar el
/// registro global de Program.cs ni el contenedor de Android.
/// </summary>
public static class AssistantServiceExtensions
{
    public static IServiceCollection AddTitanAssistant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AssistantOptions>()
            .Bind(
                configuration.GetSection(
                    AssistantOptions.SectionName));

        services
            .AddOptions<OllamaAssistantOptions>()
            .Bind(
                configuration.GetSection(
                    OllamaAssistantOptions.SectionName));

        services.AddSingleton<
            IAssistantSessionStore,
            InMemoryAssistantSessionStore>();

        services.AddSingleton<
            IAssistantQuota,
            InMemoryAssistantQuota>();

        services.AddScoped<
            IAssistantTool,
            HelpdeskMyTicketsTool>();

        services.AddScoped<
            IAssistantTool,
            HelpdeskSlaStatusTool>();

        services.AddScoped<
            IAssistantTool,
            HelpdeskTicketTool>();

        services.AddScoped<
            IAssistantTool,
            DeviceListTool>();

        services.AddScoped<
            IAssistantTool,
            DeviceStatusTool>();

        services.AddScoped<
            IAssistantTool,
            PolicyListTool>();

        services.AddScoped<
            IAssistantTool,
            ApplicationInventoryTool>();

        services.AddScoped<
            IAssistantTool,
            ReportOverviewTool>();

        services.AddScoped<
            IAssistantToolRegistry,
            AssistantToolRegistry>();

        services.AddScoped<
            OpenRouterAssistantProvider>();

        services.AddScoped<
            IAssistantProvider>(
            provider =>
                provider.GetRequiredService<
                    OpenRouterAssistantProvider>());

        services.AddHttpClient<
            OllamaAssistantProvider>(
            OllamaAssistantProvider.HttpClientName,
            (provider, client) =>
            {
                var options =
                    provider
                        .GetRequiredService<
                            IOptions<OllamaAssistantOptions>>()
                        .Value;

                var baseUrl =
                    string.IsNullOrWhiteSpace(
                        options.BaseUrl)
                        ? "http://localhost:11434/"
                        : options.BaseUrl.Trim();

                if (!baseUrl.EndsWith(
                        "/",
                        StringComparison.Ordinal))
                {
                    baseUrl += "/";
                }

                client.BaseAddress =
                    new Uri(
                        baseUrl,
                        UriKind.Absolute);

                client.Timeout =
                    TimeSpan.FromSeconds(
                        Math.Clamp(
                            options.TimeoutSeconds,
                            10,
                            300));
            });

        services.AddTransient<
            IAssistantProvider>(
            provider =>
                provider.GetRequiredService<
                    OllamaAssistantProvider>());

        services.AddScoped<
            IAssistantProviderRegistry,
            AssistantProviderRegistry>();

        services.AddScoped<
            IAssistantOrchestrator,
            AssistantOrchestrator>();

        services.AddScoped<
            AssistantPrincipalResolver>();

        return services;
    }
}
