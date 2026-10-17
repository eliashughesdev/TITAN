using TitanMDM.Infrastructure.Automation.Enterprise.Events;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Engine;

/*
 * AUT-1.2 — Registro de campos evaluables.
 *
 * Valida que el nombre de campo tenga una forma segura
 * (identificadores separados por punto) y expone los
 * campos conocidos por evento para ayuda y validación.
 * Un campo desconocido no es un error fatal: se advierte,
 * porque los adaptadores pueden aportar campos nuevos.
 */
internal static class AutomationFieldRegistry
{
    public static bool IsValidSyntax(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return false;

        foreach (
            var segment in field.Split('.'))
        {
            if (segment.Length == 0 ||
                segment.Length > 64)
            {
                return false;
            }

            foreach (var ch in segment)
            {
                if (!char.IsLetterOrDigit(ch) &&
                    ch != '_' &&
                    ch != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static IReadOnlyCollection<string>
        KnownFields(string? trigger)
    {
        var descriptor =
            AutomationEventCatalog.Find(trigger);

        if (descriptor is null)
            return Array.Empty<string>();

        return descriptor.PayloadFields;
    }

    public static bool IsKnownField(
        string? trigger,
        string field)
    {
        var known = KnownFields(trigger);

        if (known.Count == 0)
            return true;

        return known.Any(x =>
            x.Equals(
                field,
                StringComparison
                    .OrdinalIgnoreCase) ||
            field.StartsWith(
                x + ".",
                StringComparison
                    .OrdinalIgnoreCase));
    }
}
