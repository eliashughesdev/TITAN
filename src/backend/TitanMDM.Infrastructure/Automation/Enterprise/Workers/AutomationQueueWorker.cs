using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TitanMDM.Domain.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Queue;
using TitanMDM.Infrastructure.Automation.Enterprise.Scheduling;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Workers;

/*
 * AUT-1.5 / AUT-1.6 — Worker único del motor empresarial.
 *
 * 1) Recupera leases expirados tras un reinicio.
 * 2) Dispara programaciones vencidas.
 * 3) Procesa items pendientes de la cola.
 *
 * No se añade un segundo worker incompatible: convive con
 * DeviceOfflineMonitor y los workers de Helpdesk.
 */
internal sealed class AutomationQueueWorker
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationQueueWorker> _logger;
    private readonly AutomationEngineOptions _options;

    public AutomationQueueWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationQueueWorker> logger,
        IOptions<AutomationEngineOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Automation engine worker is disabled.");

            return;
        }

        _logger.LogInformation(
            "TitanMDM Automation engine worker started.");

        await SafeDelay(
            TimeSpan.FromSeconds(
                Math.Max(
                    0,
                    _options.StartupDelaySeconds)),
            stoppingToken);

        var pollInterval = TimeSpan.FromSeconds(
            Math.Max(2, _options.PollSeconds));

        while (!stoppingToken
            .IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken
                    .IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Automation engine cycle failed.");
            }

            await SafeDelay(
                pollInterval,
                stoppingToken);
        }
    }

    private async Task RunCycleAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var provider = scope.ServiceProvider;

        var queue = provider
            .GetRequiredService<
                IAutomationQueueService>();

        var processor = provider
            .GetRequiredService<
                IAutomationQueueProcessor>();

        await queue.RecoverExpiredLeasesAsync(
            TimeSpan.FromSeconds(
                _options.LeaseSeconds),
            cancellationToken);

        await FireDueSchedulesAsync(
            provider,
            queue,
            processor,
            cancellationToken);

        var claimed = await queue.ClaimDueAsync(
            Math.Max(1, _options.BatchSize),
            TimeSpan.FromSeconds(
                _options.LeaseSeconds),
            cancellationToken);

        foreach (var item in claimed)
        {
            if (cancellationToken
                .IsCancellationRequested)
                break;

            await processor.ProcessAsync(
                item.Id,
                item.OrganizationId,
                cancellationToken);
        }
    }

    private async Task FireDueSchedulesAsync(
        IServiceProvider provider,
        IAutomationQueueService queue,
        IAutomationQueueProcessor processor,
        CancellationToken cancellationToken)
    {
        var db = provider
            .GetRequiredService<TitanMdmDbContext>();

        var now = DateTime.UtcNow;

        var schedules =
            await db.Set<AutomationSchedule>()
                .Where(x =>
                    x.IsEnabled &&
                    (x.NextRunAtUtc == null ||
                     x.NextRunAtUtc <= now))
                .Take(50)
                .ToListAsync(cancellationToken);

        if (schedules.Count == 0)
            return;

        foreach (var schedule in schedules)
        {
            var rule = await db
                .Set<AutomationRule>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            schedule.AutomationRuleId &&
                        x.OrganizationId ==
                            schedule.OrganizationId,
                    cancellationToken);

            if (rule is null || !rule.IsEnabled)
            {
                schedule.ScheduleNext(null);
                continue;
            }

            var runAt = schedule.NextRunAtUtc ?? now;

            var item = new AutomationQueueItem(
                schedule.OrganizationId,
                schedule.AutomationRuleId,
                "Schedule",
                "{}",
                null,
                $"sched-{schedule.Id:N}-{runAt:O}",
                cycleDepth: 0,
                simulated: false,
                actorUserId: null,
                priority: rule.Priority);

            var stored = await queue.EnqueueAsync(
                item,
                cancellationToken);

            schedule.RegisterRun(runAt);
            schedule.ScheduleNext(
                AutomationScheduleCalculator.ComputeNext(
                    schedule.Recurrence,
                    now,
                    schedule.IntervalMinutes,
                    schedule.TimeOfDay,
                    schedule.DayOfWeek,
                    schedule.DayOfMonth,
                    schedule.TimeZoneId,
                    schedule.StartAtUtc));

            await db.SaveChangesAsync(
                cancellationToken);

            if (stored.Id == item.Id)
            {
                await processor.ProcessAsync(
                    stored.Id,
                    stored.OrganizationId,
                    cancellationToken);
            }
        }
    }

    private static async Task SafeDelay(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                delay,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Apagado del host.
        }
    }
}
