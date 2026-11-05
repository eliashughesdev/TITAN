namespace TitanMDM.Application.Notifications;

/*
 * Fase 3 - Centro de notificaciones unificado.
 *
 * Agrega alertas operativas en vivo (SQL Server) de todos los
 * modulos, filtradas por permisos RBAC y alcance de sede del
 * usuario. Cada Id es estable mientras la condicion no cambie,
 * de modo que el cliente puede recordar cuales ya leyo.
 */
public interface INotificationFeedService
{
    Task<NotificationFeedDto> GetFeedAsync(
        NotificationFeedRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationFeedRequest(
    Guid OrganizationId,
    Guid UserId,
    IReadOnlySet<string> Permissions,
    IReadOnlyCollection<Guid>? SiteIds);

public static class NotificationSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public sealed record NotificationItemDto(
    string Id,
    string Category,
    string Severity,
    string Title,
    string Message,
    string? Link,
    DateTime OccurredAtUtc);

public sealed record NotificationFeedDto(
    IReadOnlyList<NotificationItemDto> Items,
    IReadOnlyList<string> UnavailableSources,
    DateTime GeneratedAtUtc);
