using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TitanMDM.Application.Contracts.Persistence;
using TitanMDM.Application.Notifications;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Notifications;

public sealed class NotificationFeedService
    : INotificationFeedService
{
    private const int MaxItemsPerSource = 5;

    private static readonly TimeSpan RecentWindow = TimeSpan.FromHours(24);

    private static readonly DeviceCommandStatus[] FailedCommandStatuses =
    {
        DeviceCommandStatus.Failed,
        DeviceCommandStatus.Timeout,
    };

    private readonly TitanMdmDbContext _db;
    private readonly IInventoryService? _inventory;
    private readonly ILogger<NotificationFeedService> _logger;

    public NotificationFeedService(
        TitanMdmDbContext db,
        ILogger<NotificationFeedService> logger,
        IInventoryService? inventory = null)
    {
        _db = db;
        _logger = logger;
        _inventory = inventory;
    }

    public async Task<NotificationFeedDto> GetFeedAsync(
        NotificationFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        var items = new List<NotificationItemDto>();
        var unavailable = new List<string>();

        async Task Collect(string source, bool allowed, Func<Task<IEnumerable<NotificationItemDto>>> load)
        {
            if (!allowed)
            {
                return;
            }

            try
            {
                items.AddRange(await load());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Una fuente caida (p.ej. base ITAM) no tumba el centro de alertas.
                _logger.LogWarning(ex, "Fuente de notificaciones no disponible: {Source}.", source);
                unavailable.Add(source);
            }
        }

        var p = request.Permissions;

        await Collect(
            "security",
            Has(p, PermissionCodes.Security.View, PermissionCodes.Compliance.View),
            () => SecurityAsync(request, cancellationToken));

        await Collect(
            "geofencing",
            Has(p, PermissionCodes.Geofencing.View, PermissionCodes.Geofencing.Manage),
            () => GeofencingAsync(request, cancellationToken));

        await Collect(
            "commands",
            Has(p, PermissionCodes.Devices.Commands),
            () => CommandsAsync(request, cancellationToken));

        await Collect(
            "helpdesk",
            Has(p, PermissionCodes.Helpdesk.TicketsView, PermissionCodes.Helpdesk.TicketsAssign),
            () => HelpdeskAsync(request, cancellationToken));

        await Collect(
            "inventory",
            _inventory is not null &&
            Has(p, PermissionCodes.Inventory.View, PermissionCodes.Inventory.Manage, PermissionCodes.Inventory.DashboardView),
            () => InventoryAsync(cancellationToken));

        return new NotificationFeedDto(
            items
                .OrderByDescending(x => SeverityRank(x.Severity))
                .ThenByDescending(x => x.OccurredAtUtc)
                .ToList(),
            unavailable,
            DateTime.UtcNow);
    }

    // ============================================================
    // SEGURIDAD / CUMPLIMIENTO
    // ============================================================

    private async Task<IEnumerable<NotificationItemDto>> SecurityAsync(
        NotificationFeedRequest request,
        CancellationToken cancellationToken)
    {
        var postures =
            from posture in _db.DeviceSecurityPostures.AsNoTracking()
            join device in _db.Devices.AsNoTracking() on posture.DeviceId equals device.Id
            where posture.OrganizationId == request.OrganizationId && !device.IsDeleted
            select new { posture, device.SiteId };

        if (request.SiteIds is not null)
        {
            var sites = request.SiteIds.ToArray();
            postures = postures.Where(x => x.SiteId != null && sites.Contains(x.SiteId.Value));
        }

        var summary =
            await postures
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    NonCompliant = g.Count(x => x.posture.ComplianceStatus == "NonCompliant"),
                    Critical = g.Count(x => x.posture.RiskLevel == "Critical"),
                    LastChange = g.Max(x => (DateTime?)x.posture.UpdatedAtUtc),
                })
                .FirstOrDefaultAsync(cancellationToken);

        if (summary is null || (summary.NonCompliant == 0 && summary.Critical == 0))
        {
            return Array.Empty<NotificationItemDto>();
        }

        var occurred = summary.LastChange ?? DateTime.UtcNow;

        return new[]
        {
            new NotificationItemDto(
                $"security:{summary.NonCompliant}:{summary.Critical}",
                "security",
                summary.Critical > 0 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                summary.Critical > 0
                    ? $"{summary.Critical} dispositivo(s) en riesgo crítico"
                    : $"{summary.NonCompliant} dispositivo(s) no conformes",
                $"{summary.NonCompliant} no conforme(s) · {summary.Critical} crítico(s). La remediación automática atiende los fallos corregibles.",
                "/security",
                occurred),
        };
    }

    // ============================================================
    // GEOFENCING
    // ============================================================

    private async Task<IEnumerable<NotificationItemDto>> GeofencingAsync(
        NotificationFeedRequest request,
        CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow - RecentWindow;

        var events =
            from e in _db.GeofenceEvents.AsNoTracking()
            join g in _db.Geofences.AsNoTracking() on e.GeofenceId equals g.Id
            join d in _db.Devices.AsNoTracking() on e.DeviceId equals d.Id
            where e.OrganizationId == request.OrganizationId && e.OccurredAtUtc >= since && !d.IsDeleted
            select new { e.Id, e.EventType, e.OccurredAtUtc, Geofence = g.Name, Device = d.DeviceName, d.SiteId };

        if (request.SiteIds is not null)
        {
            var sites = request.SiteIds.ToArray();
            events = events.Where(x => x.SiteId != null && sites.Contains(x.SiteId.Value));
        }

        var latest =
            await events
                .OrderByDescending(x => x.OccurredAtUtc)
                .Take(MaxItemsPerSource)
                .ToListAsync(cancellationToken);

        return latest.Select(x =>
            new NotificationItemDto(
                $"geofence:{x.Id}",
                "geofencing",
                x.EventType == "Exit" ? NotificationSeverity.Warning : NotificationSeverity.Info,
                x.EventType == "Exit" ? $"Salida de {x.Geofence}" : $"Entrada a {x.Geofence}",
                $"{x.Device} {(x.EventType == "Exit" ? "abandonó" : "ingresó a")} la geocerca.",
                "/geofencing",
                x.OccurredAtUtc));
    }

    // ============================================================
    // COMANDOS MDM
    // ============================================================

    private async Task<IEnumerable<NotificationItemDto>> CommandsAsync(
        NotificationFeedRequest request,
        CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow - RecentWindow;

        var failed =
            from c in _db.DeviceCommands.AsNoTracking()
            join d in _db.Devices.AsNoTracking() on c.DeviceId equals d.Id
            where c.OrganizationId == request.OrganizationId &&
                  c.UpdatedAtUtc >= since &&
                  FailedCommandStatuses.Contains(c.Status)
            select new { c.UpdatedAtUtc, c.CommandType, d.SiteId };

        if (request.SiteIds is not null)
        {
            var sites = request.SiteIds.ToArray();
            failed = failed.Where(x => x.SiteId != null && sites.Contains(x.SiteId.Value));
        }

        var rows = await failed.Select(x => new { x.UpdatedAtUtc, x.CommandType }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<NotificationItemDto>();
        }

        var latest = rows.Max(x => x.UpdatedAtUtc);
        var top = string.Join(
            ", ",
            rows.GroupBy(x => x.CommandType)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key} ×{g.Count()}"));

        return new[]
        {
            new NotificationItemDto(
                $"commands:failed:{rows.Count}:{latest.Ticks}",
                "commands",
                NotificationSeverity.Warning,
                $"{rows.Count} comando(s) fallidos en 24 h",
                top,
                "/devices",
                latest),
        };
    }

    // ============================================================
    // HELPDESK
    // ============================================================

    private async Task<IEnumerable<NotificationItemDto>> HelpdeskAsync(
        NotificationFeedRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var result = new List<NotificationItemDto>();

        var open =
            _db.HelpdeskTickets
                .AsNoTracking()
                .Where(t =>
                    t.OrganizationId == request.OrganizationId &&
                    t.Status != "resolved" &&
                    t.Status != "closed");

        if (request.SiteIds is not null)
        {
            var sites = request.SiteIds.ToArray();
            open = open.Where(t => t.SiteId != null && sites.Contains(t.SiteId.Value));
        }

        if (Has(request.Permissions, PermissionCodes.Helpdesk.TicketsView))
        {
            var overdue =
                await open
                    .Where(t =>
                        t.AssigneeUserId == request.UserId &&
                        ((t.ResolveDueAtUtc != null && t.ResolveDueAtUtc < now) ||
                         (t.FirstRespondedAtUtc == null && t.FirstResponseDueAtUtc != null && t.FirstResponseDueAtUtc < now)))
                    .OrderBy(t => t.ResolveDueAtUtc ?? t.FirstResponseDueAtUtc)
                    .Take(MaxItemsPerSource)
                    .Select(t => new
                    {
                        t.Id,
                        t.Number,
                        t.Subject,
                        Due = t.ResolveDueAtUtc != null && t.ResolveDueAtUtc < now
                            ? t.ResolveDueAtUtc
                            : t.FirstResponseDueAtUtc,
                        Resolution = t.ResolveDueAtUtc != null && t.ResolveDueAtUtc < now,
                    })
                    .ToListAsync(cancellationToken);

            result.AddRange(overdue.Select(t =>
                new NotificationItemDto(
                    $"ticket-sla:{t.Id}:{t.Due!.Value.Ticks}",
                    "helpdesk",
                    NotificationSeverity.Critical,
                    $"SLA vencido · {t.Number}",
                    $"{(t.Resolution ? "Resolución" : "Primera respuesta")} vencida: {t.Subject}",
                    $"/helpdesk/tickets/{t.Id}",
                    t.Due.Value)));
        }

        if (Has(request.Permissions, PermissionCodes.Helpdesk.TicketsAssign))
        {
            var unassigned =
                await open
                    .Where(t => t.AssigneeUserId == null)
                    .GroupBy(_ => 1)
                    .Select(g => new { Count = g.Count(), Latest = g.Max(t => t.CreatedAtUtc) })
                    .FirstOrDefaultAsync(cancellationToken);

            if (unassigned is { Count: > 0 })
            {
                result.Add(
                    new NotificationItemDto(
                        $"tickets:unassigned:{unassigned.Count}:{unassigned.Latest.Ticks}",
                        "helpdesk",
                        NotificationSeverity.Warning,
                        $"{unassigned.Count} ticket(s) sin asignar",
                        "Requieren asignación manual o revisión del enrutamiento automático.",
                        "/helpdesk",
                        unassigned.Latest));
            }
        }

        return result;
    }

    // ============================================================
    // INVENTARIO (ITAM)
    // ============================================================

    private async Task<IEnumerable<NotificationItemDto>> InventoryAsync(
        CancellationToken cancellationToken)
    {
        var dashboard = await _inventory!.GetDashboardAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;
        var result = new List<NotificationItemDto>();

        if (dashboard.WarrantiesExpiringSoon > 0)
        {
            result.Add(new NotificationItemDto(
                $"inventory:warranty:{dashboard.WarrantiesExpiringSoon}:{today:yyyyMMdd}",
                "inventory",
                NotificationSeverity.Info,
                $"{dashboard.WarrantiesExpiringSoon} garantía(s) por vencer",
                "Vencen en los próximos 90 días.",
                "/inventory",
                today));
        }

        if (dashboard.AtRiskAssets > 0)
        {
            result.Add(new NotificationItemDto(
                $"inventory:risk:{dashboard.AtRiskAssets}",
                "inventory",
                NotificationSeverity.Warning,
                $"{dashboard.AtRiskAssets} activo(s) en riesgo",
                "Activos dañados, en mantenimiento prolongado o sin garantía.",
                "/inventory",
                today));
        }

        if (dashboard.PendingReceptions > 0)
        {
            result.Add(new NotificationItemDto(
                $"inventory:receptions:{dashboard.PendingReceptions}",
                "inventory",
                NotificationSeverity.Info,
                $"{dashboard.PendingReceptions} recepción(es) pendientes",
                "Equipos recibidos pendientes de ingreso al inventario.",
                "/inventory",
                today));
        }

        return result;
    }

    private static bool Has(IReadOnlySet<string> permissions, params string[] any) =>
        any.Any(permissions.Contains);

    private static int SeverityRank(string severity) =>
        severity switch
        {
            NotificationSeverity.Critical => 3,
            NotificationSeverity.Warning => 2,
            _ => 1,
        };
}
