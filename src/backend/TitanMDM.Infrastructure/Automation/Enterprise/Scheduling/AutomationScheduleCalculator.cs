using TitanMDM.Domain.Automation;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Scheduling;

/*
 * AUT-1.6 — Cálculo de la próxima ejecución por calendario.
 *
 * Trabaja siempre en UTC y valida la zona horaria. La
 * conversión hora local -> UTC tolera horario de verano
 * (horas inexistentes o ambiguas) sin lanzar excepción.
 */
internal static class AutomationScheduleCalculator
{
    public static DateTime? ComputeNext(
        AutomationScheduleRecurrence recurrence,
        DateTime fromUtc,
        int? intervalMinutes,
        string? timeOfDay,
        int? dayOfWeek,
        int? dayOfMonth,
        string? timeZoneId,
        DateTime? startAtUtc)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var from = DateTime.SpecifyKind(
            fromUtc,
            DateTimeKind.Utc);
        var anchor = startAtUtc.HasValue
            ? DateTime.SpecifyKind(
                startAtUtc.Value,
                DateTimeKind.Utc)
            : from;

        if (anchor > from)
            from = anchor.AddTicks(-1);

        switch (recurrence)
        {
            case AutomationScheduleRecurrence.Once:
                return anchor > fromUtc
                    ? anchor
                    : null;

            case AutomationScheduleRecurrence.Interval:
            {
                var minutes = Math.Max(
                    5,
                    intervalMinutes ?? 60);

                var step = TimeSpan.FromMinutes(
                    minutes);

                if (anchor >= from)
                    return anchor;

                var elapsed =
                    from - anchor;

                var steps =
                    (long)Math.Floor(
                        elapsed.TotalSeconds /
                        step.TotalSeconds) + 1;

                return anchor.Add(
                    TimeSpan.FromSeconds(
                        steps * step.TotalSeconds));
            }

            case AutomationScheduleRecurrence.Hourly:
            {
                var step = TimeSpan.FromHours(1);
                var next = anchor > from
                    ? anchor
                    : anchor.Add(
                        TimeSpan.FromSeconds(
                            step.TotalSeconds *
                            ((long)Math.Floor(
                                (from - anchor)
                                    .TotalSeconds /
                                step.TotalSeconds) + 1)));

                return next;
            }

            case AutomationScheduleRecurrence.Daily:
                return NextDaily(
                    from,
                    timeOfDay,
                    tz);

            case AutomationScheduleRecurrence.Weekly:
                return NextWeekly(
                    from,
                    timeOfDay,
                    dayOfWeek,
                    tz);

            case AutomationScheduleRecurrence.Monthly:
                return NextMonthly(
                    from,
                    timeOfDay,
                    dayOfMonth,
                    tz);

            default:
                return null;
        }
    }

    public static TimeZoneInfo ResolveTimeZone(
        string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                timeZoneId.Trim());
        }
        catch (
            TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static bool IsValidTimeOfDay(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(':');

        if (parts.Length != 2)
            return false;

        return int.TryParse(parts[0], out var hour) &&
            int.TryParse(parts[1], out var minute) &&
            hour is >= 0 and <= 23 &&
            minute is >= 0 and <= 59;
    }

    private static DateTime? NextDaily(
        DateTime fromUtc,
        string? timeOfDay,
        TimeZoneInfo tz)
    {
        var (hour, minute) = ParseTime(timeOfDay);

        var local = TimeZoneInfo.ConvertTimeFromUtc(
            fromUtc,
            tz);

        var candidate = new DateTime(
            local.Year,
            local.Month,
            local.Day,
            hour,
            minute,
            0,
            DateTimeKind.Unspecified);

        if (candidate <= local)
            candidate = candidate.AddDays(1);

        return LocalToUtc(candidate, tz);
    }

    private static DateTime? NextWeekly(
        DateTime fromUtc,
        string? timeOfDay,
        int? dayOfWeek,
        TimeZoneInfo tz)
    {
        var targetDay = Math.Clamp(
            dayOfWeek ?? 1,
            0,
            6);

        var (hour, minute) = ParseTime(timeOfDay);

        var local = TimeZoneInfo.ConvertTimeFromUtc(
            fromUtc,
            tz);

        var delta =
            ((targetDay - (int)local.DayOfWeek) +
             7) % 7;

        var candidate = new DateTime(
            local.Year,
            local.Month,
            local.Day,
            hour,
            minute,
            0,
            DateTimeKind.Unspecified)
            .AddDays(delta);

        if (candidate <= local)
            candidate = candidate.AddDays(7);

        return LocalToUtc(candidate, tz);
    }

    private static DateTime? NextMonthly(
        DateTime fromUtc,
        string? timeOfDay,
        int? dayOfMonth,
        TimeZoneInfo tz)
    {
        var (hour, minute) = ParseTime(timeOfDay);

        var local = TimeZoneInfo.ConvertTimeFromUtc(
            fromUtc,
            tz);

        var day = Math.Clamp(
            dayOfMonth ?? 1,
            1,
            28);

        var candidate = BuildMonthly(
            local.Year,
            local.Month,
            day,
            hour,
            minute);

        if (candidate <= local)
        {
            var nextMonth = local
                .AddMonths(1);

            candidate = BuildMonthly(
                nextMonth.Year,
                nextMonth.Month,
                day,
                hour,
                minute);
        }

        return LocalToUtc(candidate, tz);
    }

    private static DateTime BuildMonthly(
        int year,
        int month,
        int day,
        int hour,
        int minute)
    {
        var maxDay = DateTime.DaysInMonth(
            year,
            month);

        return new DateTime(
            year,
            month,
            Math.Min(day, maxDay),
            hour,
            minute,
            0,
            DateTimeKind.Unspecified);
    }

    private static (int Hour, int Minute) ParseTime(
        string? value)
    {
        if (!IsValidTimeOfDay(value))
            return (0, 0);

        var parts = value!.Split(':');

        return (
            int.Parse(parts[0]),
            int.Parse(parts[1]));
    }

    private static DateTime LocalToUtc(
        DateTime local,
        TimeZoneInfo tz)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeToUtc(
                local,
                tz);
        }
        catch (ArgumentException)
        {
            // Hora inexistente o ambigua por DST.
            var offset = tz.GetUtcOffset(local);

            return DateTime.SpecifyKind(
                    local,
                    DateTimeKind.Unspecified)
                .Subtract(offset);
        }
    }
}
