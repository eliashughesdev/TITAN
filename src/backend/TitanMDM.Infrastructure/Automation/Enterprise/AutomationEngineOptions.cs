namespace TitanMDM.Infrastructure.Automation.Enterprise;

/*
 * AUT-1.5 — Opciones del worker de automatizaciones.
 */
public sealed class AutomationEngineOptions
{
    public const string SectionName =
        "AutomationEngine";

    public bool Enabled { get; set; } = true;

    public int PollSeconds { get; set; } = 10;

    public int BatchSize { get; set; } = 10;

    public int LeaseSeconds { get; set; } = 120;

    public int StartupDelaySeconds { get; set; } = 10;
}
