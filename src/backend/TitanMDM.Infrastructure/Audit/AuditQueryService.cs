using System.Text;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Audit;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Audit;

public sealed class AuditQueryService
    : IAuditQueryService
{
    private readonly TitanMdmDbContext
        _dbContext;

    public AuditQueryService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<AuditListResultDto>
        GetEventsAsync(
            Guid organizationId,
            AuditQueryDto query,
            CancellationToken cancellationToken = default)
    {
        var page =
            Math.Max(
                query.Page,
                1);

        var pageSize =
            Math.Clamp(
                query.PageSize,
                1,
                100);

        var source =
            CreateQuery(
                organizationId,
                query);

        var totalCount =
            await source.CountAsync(
                cancellationToken);

        var totalPages =
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize);

        var rows =
            await source
                .OrderByDescending(
                    x =>
                        x.CreatedAtUtc)
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(
                    pageSize)
                .ToListAsync(
                    cancellationToken);

        return new AuditListResultDto(
            rows
                .Select(Map)
                .ToArray(),

            totalCount,
            page,
            pageSize,
            totalPages);
    }

    public async Task<AuditSummaryDto>
        GetSummaryAsync(
            Guid organizationId,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            CancellationToken cancellationToken = default)
    {
        var commands =
            _dbContext
                .DeviceCommands
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId);

        if (fromUtc.HasValue)
        {
            commands =
                commands.Where(
                    x =>
                        x.CreatedAtUtc >=
                        fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            commands =
                commands.Where(
                    x =>
                        x.CreatedAtUtc <=
                        toUtc.Value);
        }

        var now =
            DateTime.UtcNow;

        var last24Hours =
            now.AddHours(-24);

        var total =
            await commands.CountAsync(
                cancellationToken);

        var successful =
            await commands.CountAsync(
                x =>
                    x.Status ==
                    DeviceCommandStatus.Success,
                cancellationToken);

        var failed =
            await commands.CountAsync(
                x =>
                    x.Status ==
                        DeviceCommandStatus.Failed
                    ||
                    x.Status ==
                        DeviceCommandStatus.Timeout
                    ||
                    x.Status ==
                        DeviceCommandStatus.Cancelled,
                cancellationToken);

        var active =
            await commands.CountAsync(
                x =>
                    x.Status !=
                        DeviceCommandStatus.Success
                    &&
                    x.Status !=
                        DeviceCommandStatus.Failed
                    &&
                    x.Status !=
                        DeviceCommandStatus.Timeout
                    &&
                    x.Status !=
                        DeviceCommandStatus.Cancelled,
                cancellationToken);

        var eventsLast24Hours =
            await _dbContext
                .DeviceCommands
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.CreatedAtUtc >=
                            last24Hours,
                    cancellationToken);

        var uniqueActors =
            await commands
                .Select(
                    x =>
                        x.CreatedByUserId)
                .Distinct()
                .CountAsync(
                    cancellationToken);

        var uniqueDevices =
            await commands
                .Select(
                    x =>
                        x.DeviceId)
                .Distinct()
                .CountAsync(
                    cancellationToken);

        return new AuditSummaryDto(
            total,
            successful,
            failed,
            active,
            eventsLast24Hours,
            uniqueActors,
            uniqueDevices);
    }

    public async Task<AuditFilterOptionsDto>
        GetFilterOptionsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        var actions =
            await _dbContext
                .DeviceCommands
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId)
                .Select(
                    x =>
                        x.CommandType)
                .Distinct()
                .OrderBy(
                    x => x)
                .ToArrayAsync(
                    cancellationToken);

        var modules =
            actions
                .Select(
                    GetModule)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .OrderBy(
                    x => x)
                .ToArray();

        var statuses =
            Enum
                .GetNames<
                    DeviceCommandStatus>()
                .OrderBy(
                    x => x)
                .ToArray();

        return new AuditFilterOptionsDto(
            modules,
            actions,
            statuses);
    }

    public async Task<byte[]>
        ExportCsvAsync(
            Guid organizationId,
            AuditQueryDto query,
            CancellationToken cancellationToken = default)
    {
        var source =
            CreateQuery(
                organizationId,
                query);

        var rows =
            await source
                .OrderByDescending(
                    x =>
                        x.CreatedAtUtc)
                .Take(10000)
                .ToListAsync(
                    cancellationToken);

        var builder =
            new StringBuilder();

        builder.AppendLine(
            "Fecha,Modulo,Accion,Estado,Usuario,Correo,Dispositivo,Plataforma,DuracionMs,Intentos,Error");

        foreach (
            var row
            in rows)
        {
            var mapped =
                Map(row);

            builder.AppendLine(
                string.Join(
                    ",",
                    Csv(
                        mapped
                            .CreatedAtUtc
                            .ToString("O")),
                    Csv(mapped.Module),
                    Csv(mapped.Action),
                    Csv(mapped.Status),
                    Csv(mapped.ActorName),
                    Csv(mapped.ActorEmail),
                    Csv(mapped.DeviceName),
                    Csv(mapped.Platform),
                    Csv(
                        mapped
                            .DurationMilliseconds
                            ?.ToString()
                        ??
                        string.Empty),
                    Csv(
                        mapped
                            .DeliveryAttempts
                            .ToString()),
                    Csv(
                        mapped
                            .ErrorMessage
                        ??
                        string.Empty)));
        }

        return Encoding.UTF8
            .GetBytes(
                builder.ToString());
    }

    private IQueryable<AuditRow>
        CreateQuery(
            Guid organizationId,
            AuditQueryDto query)
    {
        var commands =
            _dbContext
                .DeviceCommands
                .AsNoTracking()
                .Where(
                    command =>
                        command.OrganizationId ==
                        organizationId);

        if (
            query.FromUtc
                .HasValue)
        {
            commands =
                commands.Where(
                    x =>
                        x.CreatedAtUtc >=
                        query.FromUtc.Value);
        }

        if (
            query.ToUtc
                .HasValue)
        {
            commands =
                commands.Where(
                    x =>
                        x.CreatedAtUtc <=
                        query.ToUtc.Value);
        }

        if (
            query.DeviceId
                .HasValue)
        {
            commands =
                commands.Where(
                    x =>
                        x.DeviceId ==
                        query.DeviceId.Value);
        }

        if (
            query.UserId
                .HasValue)
        {
            commands =
                commands.Where(
                    x =>
                        x.CreatedByUserId ==
                        query.UserId.Value);
        }

        if (
            !string.IsNullOrWhiteSpace(
                query.Action))
        {
            var action =
                query.Action.Trim();

            commands =
                commands.Where(
                    x =>
                        x.CommandType ==
                        action);
        }

        if (
            !string.IsNullOrWhiteSpace(
                query.Status)
            &&
            Enum.TryParse<
                DeviceCommandStatus>(
                    query.Status,
                    true,
                    out var status))
        {
            commands =
                commands.Where(
                    x =>
                        x.Status ==
                        status);
        }

        if (
            !string.IsNullOrWhiteSpace(
                query.Module))
        {
            commands =
                ApplyModuleFilter(
                    commands,
                    query.Module);
        }

        var joined =
            from command in commands

            join device
                in _dbContext
                    .Devices
                    .AsNoTracking()
                on command.DeviceId
                equals device.Id
                into deviceJoin

            from device
                in deviceJoin
                    .DefaultIfEmpty()

            join user
                in _dbContext
                    .Users
                    .AsNoTracking()
                on command.CreatedByUserId
                equals user.Id
                into userJoin

            from user
                in userJoin
                    .DefaultIfEmpty()

            select new AuditRow
            {
                Id =
                    command.Id,

                CommandType =
                    command.CommandType,

                Status =
                    command.Status,

                UserId =
                    command
                        .CreatedByUserId,

                UserFirstName =
                    user == null
                        ? string.Empty
                        : user.FirstName,

                UserLastName =
                    user == null
                        ? string.Empty
                        : user.LastName,

                UserEmail =
                    user == null
                        ? string.Empty
                        : user.Email,

                DeviceId =
                    command.DeviceId,

                DeviceName =
                    device == null
                        ? string.Empty
                        : device.DeviceName,

                Platform =
                    device == null
                        ? DevicePlatform.Unknown
                        : device.Platform,

                CreatedAtUtc =
                    command.CreatedAtUtc,

                StartedAtUtc =
                    command.StartedAtUtc,

                CompletedAtUtc =
                    command.CompletedAtUtc,

                UpdatedAtUtc =
                    command.UpdatedAtUtc,

                DeliveryAttempts =
                    command.DeliveryAttempts,

                ErrorCode =
                    command.ErrorCode,

                ErrorMessage =
                    command.ErrorMessage,

                PayloadJson =
                    command.PayloadJson,

                ResultJson =
                    command.ResultJson
            };

        if (
            !string.IsNullOrWhiteSpace(
                query.Search))
        {
            var search =
                query.Search
                    .Trim();

            joined =
                joined.Where(
                    x =>
                        x.CommandType
                            .Contains(search)
                        ||
                        x.DeviceName
                            .Contains(search)
                        ||
                        x.UserFirstName
                            .Contains(search)
                        ||
                        x.UserLastName
                            .Contains(search)
                        ||
                        x.UserEmail
                            .Contains(search));
        }

        return joined;
    }

    private static IQueryable<
        TitanMDM.Domain.Entities.DeviceCommand>
        ApplyModuleFilter(
            IQueryable<
                TitanMDM.Domain.Entities.DeviceCommand>
                query,
            string module)
    {
        var normalized =
            module
                .Trim()
                .ToLowerInvariant();

        return normalized switch
        {
            "inventario" =>
                query.Where(
                    x =>
                        x.CommandType
                            .Contains(
                                "INVENTORY")
                        ||
                        x.CommandType ==
                            "DEVICE_INFO"
                        ||
                        x.CommandType ==
                            "NETWORK_INFO"),

            "software" =>
                query.Where(
                    x =>
                        x.CommandType
                            .StartsWith(
                                "SOFTWARE_")
                        ||
                        x.CommandType ==
                            "APP_INVENTORY"),

            "seguridad" =>
                query.Where(
                    x =>
                        x.CommandType ==
                            "SECURITY_STATUS"
                        ||
                        x.CommandType ==
                            "COMPLIANCE_CHECK"),

            "windows update" =>
                query.Where(
                    x =>
                        x.CommandType
                            .StartsWith(
                                "WINDOWS_UPDATE")),

            "sistema" =>
                query.Where(
                    x =>
                        x.CommandType ==
                            "LOCK_DEVICE"
                        ||
                        x.CommandType ==
                            "RESTART_DEVICE"
                        ||
                        x.CommandType ==
                            "SHUTDOWN_DEVICE"
                        ||
                        x.CommandType
                            .StartsWith(
                                "PROCESS_")
                        ||
                        x.CommandType
                            .StartsWith(
                                "SERVICE_")
                        ||
                        x.CommandType ==
                            "SCRIPT_EXECUTE"),

            _ =>
                query
        };
    }

    private static AuditEventDto Map(
        AuditRow row)
    {
        var actorName =
            string.Join(
                    " ",
                    new[]
                    {
                        row.UserFirstName,
                        row.UserLastName
                    }
                    .Where(
                        x =>
                            !string
                                .IsNullOrWhiteSpace(
                                    x)))
                .Trim();

        if (
            string.IsNullOrWhiteSpace(
                actorName))
        {
            actorName =
                "Usuario desconocido";
        }

        long? duration =
            null;

        var start =
            row.StartedAtUtc
            ??
            row.CreatedAtUtc;

        var end =
            row.CompletedAtUtc
            ??
            (
                IsTerminal(
                    row.Status)
                    ? row.UpdatedAtUtc
                    : null
            );

        if (end.HasValue)
        {
            duration =
                Math.Max(
                    0,
                    (long)(
                        end.Value -
                        start)
                    .TotalMilliseconds);
        }

        return new AuditEventDto(
            row.Id,
            "DeviceCommand",
            GetModule(
                row.CommandType),
            row.CommandType,
            row.Status.ToString(),
            row.UserId,
            actorName,
            row.UserEmail,
            row.DeviceId,
            string.IsNullOrWhiteSpace(
                row.DeviceName)
                ? "Dispositivo eliminado"
                : row.DeviceName,
            row.Platform.ToString(),
            row.CreatedAtUtc,
            row.CompletedAtUtc,
            duration,
            row.DeliveryAttempts,
            row.ErrorCode,
            row.ErrorMessage,
            row.PayloadJson,
            row.ResultJson);
    }

    private static string GetModule(
        string commandType)
    {
        if (
            commandType.Contains(
                "INVENTORY",
                StringComparison
                    .OrdinalIgnoreCase)
            ||
            commandType.Equals(
                "DEVICE_INFO",
                StringComparison
                    .OrdinalIgnoreCase)
            ||
            commandType.Equals(
                "NETWORK_INFO",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return "Inventario";
        }

        if (
            commandType.StartsWith(
                "SOFTWARE_",
                StringComparison
                    .OrdinalIgnoreCase)
            ||
            commandType.Equals(
                "APP_INVENTORY",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return "Software";
        }

        if (
            commandType.Equals(
                "SECURITY_STATUS",
                StringComparison
                    .OrdinalIgnoreCase)
            ||
            commandType.Equals(
                "COMPLIANCE_CHECK",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return "Seguridad";
        }

        if (
            commandType.StartsWith(
                "WINDOWS_UPDATE",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            return "Windows Update";
        }

        return "Sistema";
    }

    private static bool IsTerminal(
        DeviceCommandStatus status)
    {
        return status is
            DeviceCommandStatus.Success or
            DeviceCommandStatus.Failed or
            DeviceCommandStatus.Timeout or
            DeviceCommandStatus.Cancelled;
    }

    private static string Csv(
        string value)
    {
        return
            $"\"{value.Replace(
                "\"",
                "\"\"",
                StringComparison.Ordinal)}\"";
    }

    private sealed class AuditRow
    {
        public Guid Id { get; init; }

        public string CommandType { get; init; } =
            string.Empty;

        public DeviceCommandStatus Status
        {
            get;
            init;
        }

        public Guid UserId { get; init; }

        public string UserFirstName
        {
            get;
            init;
        } = string.Empty;

        public string UserLastName
        {
            get;
            init;
        } = string.Empty;

        public string UserEmail
        {
            get;
            init;
        } = string.Empty;

        public Guid DeviceId { get; init; }

        public string DeviceName
        {
            get;
            init;
        } = string.Empty;

        public DevicePlatform Platform
        {
            get;
            init;
        }

        public DateTime CreatedAtUtc
        {
            get;
            init;
        }

        public DateTime? StartedAtUtc
        {
            get;
            init;
        }

        public DateTime? CompletedAtUtc
        {
            get;
            init;
        }

        public DateTime UpdatedAtUtc
        {
            get;
            init;
        }

        public int DeliveryAttempts
        {
            get;
            init;
        }

        public string? ErrorCode
        {
            get;
            init;
        }

        public string? ErrorMessage
        {
            get;
            init;
        }

        public string? PayloadJson
        {
            get;
            init;
        }

        public string? ResultJson
        {
            get;
            init;
        }
    }
}