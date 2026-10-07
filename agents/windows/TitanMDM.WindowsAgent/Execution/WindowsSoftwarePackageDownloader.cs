using Microsoft.Extensions.Options;

using TitanMDM.WindowsAgent.Configuration;
using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Execution;

public sealed class WindowsSoftwarePackageDownloader
{
    private readonly IHttpClientFactory
        _httpClientFactory;

    private readonly DeviceIdentityStore
        _identityStore;

    private readonly IOptions<AgentOptions>
        _options;

    public WindowsSoftwarePackageDownloader(
        IHttpClientFactory httpClientFactory,
        DeviceIdentityStore identityStore,
        IOptions<AgentOptions> options)
    {
        _httpClientFactory =
            httpClientFactory;

        _identityStore =
            identityStore;

        _options =
            options;
    }

    public async Task<string>
        DownloadAsync(
            string relativeUrl,
            string fileName,
            CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(
                relativeUrl))
        {
            throw new InvalidOperationException(
                "DownloadUrl no puede estar vacío.");
        }

        if (
            string.IsNullOrWhiteSpace(
                fileName))
        {
            throw new InvalidOperationException(
                "FileName no puede estar vacío.");
        }

        var identity =
            await _identityStore
                .LoadAsync(
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "El dispositivo no posee identidad TitanMDM.");

        var serverUrl =
            _options.Value.ServerUrl;

        if (
            !Uri.TryCreate(
                serverUrl,
                UriKind.Absolute,
                out var baseUri))
        {
            throw new InvalidOperationException(
                "ServerUrl no es válido.");
        }

        var destinationRoot =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "TitanMDM",
                "packages");

        Directory.CreateDirectory(
            destinationRoot);

        var safeFileName =
            Path.GetFileName(
                fileName);

        var destinationPath =
            Path.Combine(
                destinationRoot,
                $"{Guid.NewGuid():N}-{safeFileName}");

        var client =
            _httpClientFactory
                .CreateClient();

        client.BaseAddress =
            baseUri;

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                relativeUrl);

        request.Headers
            .TryAddWithoutValidation(
                "X-Titan-Device-Id",
                identity.DeviceId
                    .ToString());

        request.Headers
            .TryAddWithoutValidation(
                "X-Titan-Device-Secret",
                identity.DeviceSecret);

        using var response =
            await client.SendAsync(
                request,
                HttpCompletionOption
                    .ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var input =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken);

        await using var output =
            File.Create(
                destinationPath);

        await input.CopyToAsync(
            output,
            cancellationToken);

        return destinationPath;
    }
}