using System.Globalization;

namespace TitanMDM.Infrastructure.Persistence;

/// <summary>
/// Reglas de fecha del modulo ITAM.
///
/// Las fechas de ITAM se guardan como texto (herencia de la migracion
/// MongoDB): ISO con zona ("2026-11-03T18:40:06.028520+00:00"), ISO sin
/// zona del legado (UTC) o fecha calendario ("2026-11-04").
///
/// "Hoy" se evalua siempre en la zona del negocio (por defecto
/// America/Santo_Domingo, UTC-4 sin horario de verano), nunca en UTC ni
/// en la zona del servidor: una entrega a las 22:30 en RD es 02:30 UTC
/// del dia siguiente.
/// </summary>
public sealed class ItamDateRules
{
    public const string DefaultTimeZoneId = "America/Santo_Domingo";

    private static readonly string[] CalendarFormats = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };

    private readonly TimeZoneInfo _timeZone;

    public ItamDateRules(string? timeZoneId = null)
    {
        _timeZone = ResolveTimeZone(timeZoneId);
    }

    public TimeZoneInfo TimeZone => _timeZone;

    public DateOnly Today(DateTimeOffset? now = null) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now ?? DateTimeOffset.UtcNow, _timeZone).DateTime);

    /// <summary>Instante absoluto de un valor ISO (sin zona = UTC).</summary>
    public static bool TryParseInstant(string? value, out DateTimeOffset instant)
    {
        instant = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out instant);
    }

    /// <summary>Fecha calendario en la zona del negocio.</summary>
    public DateOnly? ToBusinessDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        // Fechas calendario: se toman literales (no son un instante).
        if (DateOnly.TryParseExact(trimmed, CalendarFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var calendar))
        {
            return calendar;
        }

        return TryParseInstant(trimmed, out var instant)
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _timeZone).DateTime)
            : null;
    }

    /// <summary>Clave de orden cronologico real (no lexicografico).</summary>
    public static DateTimeOffset SortKey(string? value) =>
        TryParseInstant(value, out var instant) ? instant : DateTimeOffset.MinValue;

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        foreach (var candidate in new[] { id, DefaultTimeZoneId, "SA Western Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // RD no usa horario de verano: UTC-4 fijo.
        return TimeZoneInfo.CreateCustomTimeZone("RD", TimeSpan.FromHours(-4), "Republica Dominicana", "AST");
    }
}

/// <summary>Periodos operativos de entregas/devoluciones.</summary>
public static class ItamPeriods
{
    public const string All = "all";
    public const string Today = "today";
    public const string Pending = "pending";
    public const string Overdue = "overdue";
}
