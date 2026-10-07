namespace TitanMDM.Infrastructure.Android;

public interface IGoogleAndroidAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default);

    Task<bool> CanAuthenticateAsync(
        CancellationToken cancellationToken = default);
}