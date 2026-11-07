using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Devices;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Devices;

public sealed class DeviceAliasService : IDeviceAliasService
{
    private readonly TitanMdmDbContext _dbContext;

    public DeviceAliasService(TitanMdmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DeviceAliasResult?> SetAliasAsync(
        Guid organizationId,
        Guid deviceId,
        string? alias,
        CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.Id == deviceId && !x.IsDeleted,
                cancellationToken);

        if (device is null)
            return null;

        // ArgumentException si es demasiado largo o tiene caracteres de control.
        device.SetAlias(alias);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // El agente lo recibe en la respuesta de su siguiente heartbeat.
        return new DeviceAliasResult(device.Id, device.Alias);
    }
}
