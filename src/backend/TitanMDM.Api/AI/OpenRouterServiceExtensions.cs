using Microsoft.Extensions.Options;

using TitanMDM.Api.AI.Assistant;

namespace TitanMDM.Api.AI;

public static class OpenRouterServiceExtensions
{
    public static IServiceCollection AddTitanOpenRouter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<OpenRouterOptions>()
            .Bind(
                configuration.GetSection(
                    OpenRouterOptions.SectionName));

        services.AddHttpClient<
            IOpenRouterAiService,
            OpenRouterAiService>(
            (serviceProvider, client) =>
            {
                var options =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<OpenRouterOptions>>()
                        .Value;

                var baseUrl =
                    string.IsNullOrWhiteSpace(
                        options.BaseUrl)
                        ? "https://openrouter.ai/api/v1/"
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
                            180));
            });

        // Núcleo del asistente empresarial (herramientas de
        // lectura, sesiones, orquestador y proveedores).
        // Se registra aquí para no modificar el registro global
        // de Program.cs ni el contenedor de Android.
        services.AddTitanAssistant(
            configuration);

        return services;
    }
}