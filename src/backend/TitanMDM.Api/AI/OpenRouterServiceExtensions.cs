using Microsoft.Extensions.Options;

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

        return services;
    }
}