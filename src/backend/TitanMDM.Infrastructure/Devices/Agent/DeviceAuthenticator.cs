using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Devices.Agent;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Devices.Agent;

public sealed class DeviceAuthenticator
    : IDeviceAuthenticator
{
    private readonly TitanMdmDbContext _dbContext;

    public DeviceAuthenticator(
        TitanMdmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AuthenticateAsync(
        Guid deviceId,
        string deviceSecret,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
        {
            throw new DeviceAuthenticationException(
                "INVALID_DEVICE_ID",
                "DeviceId no es válido.");
        }

        if (string.IsNullOrWhiteSpace(deviceSecret))
        {
            throw new DeviceAuthenticationException(
                "INVALID_CREDENTIAL",
                "DeviceSecret es obligatorio.");
        }

        var deviceExists =
            await _dbContext.Devices
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id == deviceId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!deviceExists)
        {
            throw new DeviceAuthenticationException(
                "DEVICE_NOT_FOUND",
                "El dispositivo no existe.");
        }

        var credential =
            await _dbContext.DeviceCredentials
                .SingleOrDefaultAsync(
                    x =>
                        x.DeviceId == deviceId &&
                        x.IsActive,
                    cancellationToken);

        if (credential is null)
        {
            throw new DeviceAuthenticationException(
                "CREDENTIAL_NOT_FOUND",
                "El dispositivo no posee una credencial activa.");
        }

        var suppliedSecretHash =
            ComputeSha256(
                deviceSecret.Trim());

        if (!FixedTimeEquals(
                credential.SecretHash,
                suppliedSecretHash))
        {
            throw new DeviceAuthenticationException(
                "INVALID_CREDENTIAL",
                "La credencial del dispositivo no es válida.");
        }

        credential.RegisterAuthentication();

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private static string ComputeSha256(
        string value)
    {
        var bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(
        string expectedHash,
        string suppliedHash)
    {
        try
        {
            var expected =
                Convert.FromHexString(
                    expectedHash);

            var supplied =
                Convert.FromHexString(
                    suppliedHash);

            return CryptographicOperations.FixedTimeEquals(
                expected,
                supplied);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}