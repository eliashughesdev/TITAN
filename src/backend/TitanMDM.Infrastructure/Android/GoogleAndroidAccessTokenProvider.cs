using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TitanMDM.Infrastructure.Android;

public sealed class GoogleAndroidAccessTokenProvider
    : IGoogleAndroidAccessTokenProvider
{
    private readonly AndroidManagementOptions _options;
    private readonly ILogger<GoogleAndroidAccessTokenProvider> _logger;

    private readonly SemaphoreSlim _credentialLock =
        new(1, 1);

    private GoogleCredential? _credential;

    public GoogleAndroidAccessTokenProvider(
        IOptions<AndroidManagementOptions> options,
        ILogger<GoogleAndroidAccessTokenProvider> logger)
    {
        _options =
            options?.Value ??
            throw new ArgumentNullException(nameof(options));

        _logger =
            logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        var credential =
            await GetCredentialAsync(
                cancellationToken);

        var tokenAccess =
            (ITokenAccess)credential;

        var token =
            await tokenAccess.GetAccessTokenForRequestAsync(
                _options.BaseUrl,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Google ADC returned an empty access token.");
        }

        return token;
    }

    public async Task<bool> CanAuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ =
                await GetAccessTokenAsync(
                    cancellationToken);

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Android Management ADC authentication validation failed.");

            return false;
        }
    }

    private async Task<GoogleCredential> GetCredentialAsync(
        CancellationToken cancellationToken)
    {
        if (_credential is not null)
            return _credential;

        await _credentialLock.WaitAsync(
            cancellationToken);

        try
        {
            if (_credential is not null)
                return _credential;

            var credential =
                await GoogleCredential
                    .GetApplicationDefaultAsync(
                        cancellationToken);

            if (credential.IsCreateScopedRequired)
            {
                credential =
                    credential.CreateScoped(
                        _options.Scope);
            }

            _credential = credential;

            _logger.LogInformation(
                "Google Application Default Credentials loaded for Android Management.");

            return credential;
        }
        finally
        {
            _credentialLock.Release();
        }
    }
}