using Microsoft.EntityFrameworkCore;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

/// <summary>
/// Ejecuta enriquecimiento AI antes del routing.
///
/// No asigna técnicos.
/// No modifica capacidad.
/// No modifica turnos.
/// </summary>
public sealed class HelpdeskAiRoutingEnrichmentWorker(
    IServiceScopeFactory scopes,
    ILogger<HelpdeskAiRoutingEnrichmentWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            /*
             * Requester Intelligence empieza antes.
             *
             * Esperamos unos segundos adicionales para permitir:
             *
             * email
             * -> identidad
             * -> Entra
             * -> dispositivo
             * -> site
             */
            await Task.Delay(
                TimeSpan.FromSeconds(
                    12),
                stoppingToken);

            using var timer =
                new PeriodicTimer(
                    TimeSpan.FromSeconds(
                        15));

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
                        "Falló Helpdesk AI Routing Enrichment.");
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
        }
    }

    private async Task ExecuteCycleAsync(
        CancellationToken cancellationToken)
    {
        List<TicketKey> tickets;

        using (
            var scope =
                scopes.CreateScope())
        {
            var db =
                scope.ServiceProvider
                    .GetRequiredService<
                        TitanMdmDbContext>();

            var cutoff =
                DateTime.UtcNow
                    .AddSeconds(
                        -8);

            tickets =
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
                                "pendinguser"
                            &&
                            (
                                x.RequestedTeamId ==
                                    null
                                ||
                                x.Category ==
                                    "general"
                                ||
                                x.SiteId ==
                                    null
                            ))
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
                        50)
                    .ToListAsync(
                        cancellationToken);
        }

        /*
         * Limitamos AI por ciclo.
         *
         * Evita:
         *
         * - costos inesperados;
         * - bursts;
         * - rate limiting;
         * - procesamiento duplicado.
         */
        var processed =
            0;

        foreach (
            var ticket
            in tickets)
        {
            if (
                processed >=
                10)
            {
                break;
            }

            cancellationToken
                .ThrowIfCancellationRequested();

            using var scope =
                scopes.CreateScope();

            try
            {
                var service =
                    scope.ServiceProvider
                        .GetRequiredService<
                            HelpdeskAiRoutingEnrichmentService>();

                var result =
                    await service.EnrichAsync(
                        ticket.OrganizationId,
                        ticket.TicketId,
                        cancellationToken);

                if (result.Changed)
                {
                    logger.LogInformation(
                        "AI routing enriqueció {TicketNumber}: " +
                        "Category={Category}, Team={Team}, Confidence={Confidence}.",
                        ticket.Number,
                        result.Category,
                        result.TeamName,
                        result.Confidence);
                }

                processed++;
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
                logger.LogError(
                    exception,
                    "No se pudo enriquecer routing AI del ticket {TicketNumber}.",
                    ticket.Number);
            }
        }
    }

    private sealed record TicketKey(
        Guid OrganizationId,
        Guid TicketId,
        string Number);
}
