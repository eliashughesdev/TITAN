using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

public sealed class HelpdeskRequesterIntelligenceWorker(
    IServiceScopeFactory scopes,
    ILogger<HelpdeskRequesterIntelligenceWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            /*
             * Arranca antes que el ciclo normal de routing
             * pero deja terminar el bootstrap inicial.
             */
            await Task.Delay(
                TimeSpan.FromSeconds(
                    8),
                stoppingToken);

            using var timer =
                new PeriodicTimer(
                    TimeSpan.FromSeconds(
                        10));

            do
            {
                try
                {
                    await ExecuteCycleAsync(
                        stoppingToken);
                }
                catch (
                    OperationCanceledException)
                    when (
                        stoppingToken
                            .IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Falló Helpdesk Requester Intelligence.");
                }
            }
            while (
                await timer.WaitForNextTickAsync(
                    stoppingToken));
        }
        catch (
            OperationCanceledException)
            when (
                stoppingToken
                    .IsCancellationRequested)
        {
            // Shutdown normal.
        }
    }

    private async Task ExecuteCycleAsync(
        CancellationToken cancellationToken)
    {
        List<TicketKey> pending;

        using (
            var scope =
                scopes.CreateScope())
        {
            var db =
                scope.ServiceProvider
                    .GetRequiredService<
                        TitanMdmDbContext>();

            /*
             * Le damos unos segundos al MailWorker para persistir
             * body, remitente y adjuntos.
             */
            var cutoff =
                DateTime.UtcNow
                    .AddSeconds(
                        -5);

            pending =
                await db.HelpdeskTickets
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.AssigneeUserId ==
                                null
                            &&
                            x.CreatedAtUtc <=
                                cutoff
                            &&
                            x.Status !=
                                "closed"
                            &&
                            x.Status !=
                                "resolved"
                            &&
                            x.Status !=
                                "pendinguser")
                    .OrderBy(
                        x =>
                            x.CreatedAtUtc)
                    .Select(
                        x =>
                            new TicketKey(
                                x.OrganizationId,
                                x.Id,
                                x.Number))
                    .Take(
                        100)
                    .ToListAsync(
                        cancellationToken);
        }

        foreach (
            var key
            in pending)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            using var scope =
                scopes.CreateScope();

            try
            {
                var intelligence =
                    scope.ServiceProvider
                        .GetRequiredService<
                            HelpdeskRequesterIntelligenceService>();

                var result =
                    await intelligence.EnrichAsync(
                        key.OrganizationId,
                        key.TicketId,
                        cancellationToken);

                if (result.Changed)
                {
                    logger.LogInformation(
                        "Ticket {TicketNumber} enriquecido antes del routing. " +
                        "Site={SiteId}, Location={SiteLocationId}, Device={DeviceName}.",
                        key.Number,
                        result.SiteId,
                        result.SiteLocationId,
                        result.DeviceName);
                }
            }
            catch (
                OperationCanceledException)
                when (
                    cancellationToken
                        .IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                /*
                 * Una falla enriqueciendo un ticket NO puede
                 * detener el worker completo.
                 */
                logger.LogError(
                    exception,
                    "No se pudo enriquecer ticket {TicketNumber} ({TicketId}).",
                    key.Number,
                    key.TicketId);
            }
        }
    }

    private sealed record TicketKey(
        Guid OrganizationId,
        Guid TicketId,
        string Number);
}
