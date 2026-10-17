using TitanMDM.Application.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Events;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Integrations;

/*
 * AUT-1.9 — Catálogo de integraciones reales vs preparadas.
 *
 * Deriva el estado de conexión del catálogo de eventos: un
 * módulo está "connected" cuando al menos uno de sus eventos
 * ya se emite hoy mediante IAutomationEventDispatcher; en
 * caso contrario queda "prepared" con su contrato listo y el
 * punto de conexión documentado para el emisor.
 */
internal static class AutomationIntegrationCatalog
{
    public static IReadOnlyCollection<AutomationIntegrationDto>
        All { get; } = new[]
        {
            Build(
                AutomationModules.General,
                "Dispositivos y eventos generales",
                "Plataforma de dispositivos",
                "SecurityPostureService, LostModeService, " +
                "DeviceLocationService, DeviceRegistrationService, " +
                "DeviceAgentService, DeviceOfflineMonitor",
                "Eventos de ciclo de vida del dispositivo " +
                "(enrolamiento, estado, geocerca, batería, modo " +
                "perdido) ya publicados en el motor."),

            Build(
                AutomationModules.Compliance,
                "Cumplimiento y seguridad",
                "Plataforma de cumplimiento",
                "SecurityPostureService",
                "Los eventos de incumplimiento y riesgo de " +
                "seguridad se publican desde la postura de " +
                "seguridad."),

            Build(
                AutomationModules.Windows,
                "Windows (inventario y parches)",
                "Agente Windows",
                "pendiente: publicar " +
                "IAutomationEventDispatcher.DispatchAsync en el " +
                "flujo de Windows Update / inventario",
                "Contrato preparado. Requiere que el módulo Windows " +
                "emita los eventos; no se toca la fuente Windows."),

            Build(
                AutomationModules.Inventory,
                "Inventario",
                "Inventario",
                "pendiente: publicar " +
                "IAutomationEventDispatcher.DispatchAsync al " +
                "detectar cambios de inventario",
                "Contrato preparado para cambios de inventario."),

            Build(
                AutomationModules.Android,
                "Android Enterprise",
                "Agente Android",
                "pendiente: publicar " +
                "IAutomationEventDispatcher.DispatchAsync en el " +
                "flujo Android (enrolamiento, políticas, apps)",
                "Contrato preparado. No se toca la fuente Android."),

            Build(
                AutomationModules.Helpdesk,
                "Helpdesk",
                "Helpdesk",
                "pendiente: publicar " +
                "IAutomationEventDispatcher.DispatchAsync en los " +
                "flujos de ticket y SLA",
                "Contrato preparado para eventos de ticket."),

            Build(
                AutomationModules.Ponches,
                "Ponches",
                "Ponches",
                "pendiente: publicar " +
                "IAutomationEventDispatcher.DispatchAsync en " +
                "sincronización y detección de anomalías",
                "Contrato preparado. No se toca la fuente Ponches.")
        };

    private static AutomationIntegrationDto Build(
        string module,
        string label,
        string owner,
        string connectionPoint,
        string notes)
    {
        var events = AutomationEventCatalog.All
            .Where(x => x.Module == module)
            .Select(x => x.Code)
            .ToArray();

        var connected = AutomationEventCatalog.All
            .Any(x =>
                x.Module == module &&
                x.Connected);

        return new AutomationIntegrationDto(
            module,
            label,
            connected ? "connected" : "prepared",
            connected,
            events,
            owner,
            connectionPoint,
            notes);
    }
}
