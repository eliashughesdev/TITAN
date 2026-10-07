using Microsoft.EntityFrameworkCore;
using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

public sealed class HelpdeskMonitoringService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HelpdeskMonitoringService> _logger;

    private readonly int _scanMinutes;
    private readonly int _unassignedMinutes;
    private readonly int _attentionMinutes;
    private readonly int _idleMinutes;
    private readonly int _repeatMinutes;
    private readonly int _overdueRepeatMinutes;

    public HelpdeskMonitoringService(
        IServiceScopeFactory scopes,
        ILogger<HelpdeskMonitoringService> logger,
        IConfiguration configuration)
    {
        _scopes = scopes;
        _logger = logger;

        int Read(
            string key,
            int fallback,
            int minimum,
            int maximum)
        {
            return Math.Clamp(
                configuration.GetValue<int?>(
                    "Helpdesk:Monitoring:" + key)
                    ?? fallback,
                minimum,
                maximum);
        }

        _scanMinutes =
            Read("ScanMinutes", 1, 1, 60);

        _unassignedMinutes =
            Read("UnassignedMinutes", 15, 1, 1440);

        _attentionMinutes =
            Read("AttentionMinutes", 15, 1, 1440);

        _idleMinutes =
            Read("IdleMinutes", 120, 15, 10080);

        _repeatMinutes =
            Read("RepeatMinutes", 60, 15, 1440);

        _overdueRepeatMinutes =
            Read("OverdueRepeatMinutes", 120, 15, 1440);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(15),
                stoppingToken);

            using var timer = new PeriodicTimer(
                TimeSpan.FromMinutes(_scanMinutes));

            do
            {
                try
                {
                    await ScanAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Falló el monitor automático de Helpdesk.");
                }
            }
            while (
                await timer.WaitForNextTickAsync(
                    stoppingToken));
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Detención normal del backend.
        }
    }

    private async Task ScanAsync(
        CancellationToken cancellationToken)
    {
        List<TicketKey> keys;

        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<TitanMdmDbContext>();

            keys = await db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.Status != "resolved" &&
                        x.Status != "closed")
                .OrderBy(x => x.CreatedAtUtc)
                .Select(
                    x => new TicketKey(
                        x.OrganizationId,
                        x.Id))
                .ToListAsync(cancellationToken);
        }

        var count = 0;

        foreach (var key in keys)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            using var scope = _scopes.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<TitanMdmDbContext>();

            try
            {
                count += await RecordAsync(
                    db,
                    key,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Falló el seguimiento del ticket {TicketId}.",
                    key.Id);
            }
        }

        if (count > 0)
        {
            _logger.LogInformation(
                "Helpdesk registró {Count} avisos internos.",
                count);
        }
    }

    private async Task<int> RecordAsync(
        TitanMdmDbContext db,
        TicketKey key,
        CancellationToken cancellationToken)
    {
        var strategy =
            db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // La comprobación de avisos y su escritura
            // se realizan dentro de la misma transacción.
            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            var ticket = await db.HelpdeskTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == key.Id &&
                        x.OrganizationId ==
                            key.OrganizationId &&
                        x.Status != "resolved" &&
                        x.Status != "closed",
                    cancellationToken);

            if (ticket is null)
                return 0;

            var now = DateTime.UtcNow;

            var since = now.AddMinutes(
                -Math.Max(
                    _repeatMinutes,
                    _overdueRepeatMinutes));

            var recent = await db.HelpdeskTicketEvents
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            key.OrganizationId &&
                        x.TicketId == key.Id &&
                        x.CreatedAtUtc >= since)
                .Select(
                    x => new
                    {
                        x.EventType,
                        x.CreatedAtUtc
                    })
                .ToListAsync(cancellationToken);

            var pending =
                new List<HelpdeskTicketEvent>();

            void Add(
                string type,
                string text,
                int repeatMinutes)
            {
                var alreadyRecorded = recent.Any(
                    x =>
                        x.EventType == type &&
                        x.CreatedAtUtc >
                            now.AddMinutes(-repeatMinutes));

                if (alreadyRecorded)
                    return;

                if (pending.Any(
                        x => x.EventType == type))
                {
                    return;
                }

                var summary =
                    $"Ticket {ticket.Number}: {text}";

                if (summary.Length > 500)
                    summary = summary[..500];

                pending.Add(
                    new HelpdeskTicketEvent(
                        key.OrganizationId,
                        key.Id,
                        null,
                        type,
                        summary));
            }

            if (ticket.AssigneeUserId is null &&
                ticket.CreatedAtUtc <=
                    now.AddMinutes(-_unassignedMinutes))
            {
                Add(
                    "unassigned_reminder",
                    "continúa sin técnico asignado; revisa " +
                    "la cobertura, especialidad y disponibilidad.",
                    _repeatMinutes);
            }

            if (ticket.AssigneeUserId.HasValue &&
                ticket.FirstRespondedAtUtc is null &&
                (
                    ticket.Status == "new" ||
                    ticket.Status == "open"
                ) &&
                ticket.UpdatedAtUtc <=
                    now.AddMinutes(-_attentionMinutes))
            {
                Add(
                    "assigned_attention_reminder",
                    "está asignado y todavía no tiene " +
                    "primera respuesta del técnico.",
                    _repeatMinutes);
            }

            // En espera del usuario no genera recordatorios
            // de inactividad del técnico.
            if (ticket.AssigneeUserId.HasValue &&
                ticket.FirstRespondedAtUtc.HasValue &&
                ticket.Status != "pendinguser" &&
                ticket.UpdatedAtUtc <=
                    now.AddMinutes(-_idleMinutes))
            {
                Add(
                    "inactivity_reminder",
                    "no registra una actualización reciente; " +
                    "revisa la atención y documenta el avance.",
                    _repeatMinutes);
            }

            if (ticket.FirstRespondedAtUtc is null &&
                ticket.FirstResponseDueAtUtc.HasValue)
            {
                if (ticket.FirstResponseDueAtUtc.Value <= now)
                {
                    Add(
                        "first_response_overdue",
                        "superó el plazo de primera respuesta.",
                        _overdueRepeatMinutes);
                }
                else if (
                    ticket.FirstResponseDueAtUtc.Value <=
                        now.AddMinutes(30))
                {
                    Add(
                        "first_response_warning",
                        "el plazo de primera respuesta vence " +
                        "en los próximos 30 minutos.",
                        _repeatMinutes);
                }
            }

            if (ticket.ResolvedAtUtc is null &&
                ticket.ResolveDueAtUtc.HasValue)
            {
                if (ticket.ResolveDueAtUtc.Value <= now)
                {
                    Add(
                        "resolution_overdue",
                        "superó el plazo de resolución.",
                        _overdueRepeatMinutes);
                }
                else if (
                    ticket.ResolveDueAtUtc.Value <=
                        now.AddHours(1))
                {
                    Add(
                        "resolution_warning",
                        "el plazo de resolución vence " +
                        "en los próximos 60 minutos.",
                        _repeatMinutes);
                }
            }

            if (pending.Count == 0)
                return 0;

            db.HelpdeskTicketEvents.AddRange(pending);

            try
            {
                await db.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return pending.Count;
            }
            finally
            {
                foreach (var activity in pending)
                {
                    db.Entry(activity).State =
                        EntityState.Detached;
                }
            }
        });
    }

    private sealed record TicketKey(
        Guid OrganizationId,
        Guid Id);
}