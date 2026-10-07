using System.Text.Json;

namespace TitanMDM.Domain.Entities;

public sealed record HelpdeskWeeklySlot(
    int Day,
    string Start,
    string End);

public sealed class HelpdeskTechnicianSchedule
{
    private HelpdeskTechnicianSchedule() { }

    public HelpdeskTechnicianSchedule(
        Guid organizationId,
        Guid teamId,
        Guid userId)
    {
        if (organizationId == Guid.Empty ||
            teamId == Guid.Empty ||
            userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organización, grupo y técnico son obligatorios.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TeamId = teamId;
        UserId = userId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }

    public int Priority { get; private set; } = 1;
    public bool IsEnabled { get; private set; }

    public string TimeZoneId { get; private set; } =
        "America/Santo_Domingo";

    public string SlotsJson { get; private set; } = "[]";

    public DateTime UpdatedAtUtc { get; private set; } =
        DateTime.UtcNow;

    public void Configure(
        int priority,
        bool enabled,
        string timeZoneId,
        IEnumerable<HelpdeskWeeklySlot> slots)
    {
        if (priority is < 1 or > 100)
        {
            throw new ArgumentException(
                "La prioridad debe estar entre 1 y 100.");
        }

        var normalizedZone = timeZoneId?.Trim() ?? "";

        if (normalizedZone.Length is < 1 or > 100)
        {
            throw new ArgumentException(
                "Indica una zona horaria válida.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedZone);
        }
        catch (Exception ex) when (
            ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                "La zona horaria no está disponible en el servidor.");
        }

        var list = slots.ToArray();

        if (list.Length > 28 || (enabled && list.Length == 0))
        {
            throw new ArgumentException(
                "Un técnico habilitado necesita un horario; " +
                "máximo 28 franjas.");
        }

        var occupied = new HashSet<int>();

        foreach (var slot in list)
        {
            if (slot.Day is < 0 or > 6 ||
                !TimeOnly.TryParseExact(
                    slot.Start, "HH:mm", out var start) ||
                !TimeOnly.TryParseExact(
                    slot.End, "HH:mm", out var end) ||
                start == end)
            {
                throw new ArgumentException(
                    "Horario inválido. Usa días 0–6 y " +
                    "horas HH:mm distintas.");
            }

            var startMinute = start.Hour * 60 + start.Minute;
            var endMinute = end.Hour * 60 + end.Minute;

            var from = slot.Day * 1440 + startMinute;
            var duration = (endMinute - startMinute + 1440) % 1440;

            // Se valida la semana completa para detectar también
            // solapamientos de turnos que cruzan medianoche.
            for (var minute = 0; minute < duration; minute++)
            {
                if (!occupied.Add((from + minute) % 10080))
                {
                    throw new ArgumentException(
                        "Las franjas del técnico no pueden solaparse.");
                }
            }
        }

        var json = JsonSerializer.Serialize(list);

        if (json.Length > 4000)
        {
            throw new ArgumentException(
                "El horario excede el tamaño permitido.");
        }

        Priority = priority;
        IsEnabled = enabled;
        TimeZoneId = normalizedZone;
        SlotsJson = json;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public IReadOnlyList<HelpdeskWeeklySlot> GetSlots() =>
        JsonSerializer.Deserialize<HelpdeskWeeklySlot[]>(SlotsJson)
        ?? [];

    public bool IsOnDuty(DateTime utcNow)
    {
        if (!IsEnabled)
            return false;

        try
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));

            var day = (int)local.DayOfWeek;
            var time = TimeOnly.FromDateTime(local);

            foreach (var slot in GetSlots())
            {
                if (!TimeOnly.TryParseExact(
                        slot.Start, "HH:mm", out var start) ||
                    !TimeOnly.TryParseExact(
                        slot.End, "HH:mm", out var end))
                {
                    continue;
                }

                // Turno dentro del mismo día.
                if (start < end &&
                    day == slot.Day &&
                    time >= start &&
                    time < end)
                {
                    return true;
                }

                // Turno que continúa al día siguiente.
                if (start > end &&
                    ((day == slot.Day && time >= start) ||
                     (day == (slot.Day + 1) % 7 && time < end)))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (
            ex is TimeZoneNotFoundException or
                  InvalidTimeZoneException or
                  JsonException)
        {
            return false;
        }

        return false;
    }
}