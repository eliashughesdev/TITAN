namespace TitanMDM.Infrastructure.Automation.Enterprise.Events;

/*
 * AUT-1.3 — Catálogo de eventos del motor.
 *
 * "Connected = true" significa que el evento ya se emite
 * desde un módulo estable y el motor lo procesa hoy.
 * "Connected = false" es un descriptor preparado para
 * adaptadores futuros: el contrato existe, la conexión
 * se habilita cuando el módulo emisor lo publique.
 */
internal sealed record AutomationEventDescriptor(
    string Code,
    string Module,
    string Label,
    string Description,
    bool Connected,
    IReadOnlyCollection<string> PayloadFields,
    int CycleCost = 1);

internal static class AutomationEventCatalog
{
    private static readonly AutomationEventDescriptor[]
        Descriptors =
    {
        // ---- General / dispositivo -----------------------------
        new(
            "Manual",
            "general",
            "Ejecución manual",
            "Un administrador ejecuta la regla manualmente.",
            true,
            Array.Empty<string>()),

        new(
            "Schedule",
            "general",
            "Programación",
            "Disparador por calendario configurado en la regla.",
            true,
            Array.Empty<string>()),

        new(
            "DeviceEnrolled",
            "general",
            "Equipo enrolado",
            "Un dispositivo completa el enrolamiento.",
            true,
            new[]
            {
                "deviceName", "platform", "serialNumber",
                "siteId", "organizationId"
            }),

        new(
            "DeviceOnline",
            "general",
            "Equipo conectado",
            "Un dispositivo pasa de offline a online.",
            true,
            new[]
            {
                "deviceName", "platform", "status",
                "lastSeenAtUtc"
            }),

        new(
            "DeviceOffline",
            "general",
            "Equipo desconectado",
            "Un dispositivo deja de reportar.",
            true,
            new[]
            {
                "deviceName", "platform", "status",
                "lastSeenAtUtc", "offlineThresholdMinutes"
            }),

        new(
            "DeviceNonCompliant",
            "compliance",
            "Incumplimiento detectado",
            "El dispositivo incumple la política asignada.",
            true,
            new[]
            {
                "deviceName", "platform", "status",
                "reason", "policyName"
            }),

        new(
            "SecurityRisk",
            "compliance",
            "Riesgo de seguridad",
            "La postura de seguridad reporta un riesgo.",
            true,
            new[]
            {
                "deviceName", "platform", "riskLevel",
                "reason"
            }),

        new(
            "GeofenceEnter",
            "general",
            "Entrada a geocerca",
            "El dispositivo entra en una geocerca.",
            true,
            new[] { "geofenceId", "geofenceName", "deviceName" }),

        new(
            "GeofenceExit",
            "general",
            "Salida de geocerca",
            "El dispositivo sale de una geocerca.",
            true,
            new[] { "geofenceId", "geofenceName", "deviceName" }),

        new(
            "LowBattery",
            "general",
            "Batería baja",
            "La batería del dispositivo cruza el umbral.",
            true,
            new[]
            {
                "deviceName", "batteryLevel", "threshold"
            }),

        new(
            "LostModeActivated",
            "general",
            "Modo perdido activado",
            "El dispositivo entra en modo perdido.",
            true,
            new[] { "deviceName", "message" }),

        new(
            "DeviceSiteResolved",
            "general",
            "Sitio resuelto",
            "El agente resuelve el sitio del dispositivo.",
            true,
            new[] { "deviceName", "siteId", "siteName" }),

        // ---- Windows -------------------------------------------
        new(
            "WindowsUpdateScan",
            "windows",
            "Escaneo de actualizaciones Windows",
            "Resultado de un escaneo de Windows Update.",
            false,
            new[] { "deviceName", "pendingUpdates" }),

        new(
            "WindowsUpdateInstallFailed",
            "windows",
            "Instalación Windows fallida",
            "Una instalación de actualización falló.",
            false,
            new[] { "deviceName", "kbArticleId", "error" }),

        new(
            "WindowsInventoryChanged",
            "inventory",
            "Cambio de inventario Windows",
            "El inventario del equipo cambió.",
            false,
            new[] { "deviceName", "changedComponent" }),

        // ---- Android -------------------------------------------
        new(
            "AndroidEnrolled",
            "android",
            "Dispositivo Android enrolado",
            "Un dispositivo Android completa el enrolamiento.",
            false,
            new[] { "deviceName", "serialNumber" }),

        new(
            "AndroidDisconnected",
            "android",
            "Dispositivo Android desconectado",
            "El dispositivo Android deja de reportar.",
            false,
            new[] { "deviceName" }),

        new(
            "AndroidNonCompliant",
            "android",
            "Incumplimiento Android",
            "El dispositivo Android incumple la política.",
            false,
            new[] { "deviceName", "reason" }),

        new(
            "AndroidRequiredAppPending",
            "android",
            "Aplicación obligatoria pendiente",
            "Falta instalar una aplicación obligatoria.",
            false,
            new[] { "deviceName", "packageName" }),

        new(
            "AndroidPolicyError",
            "android",
            "Error de política Android",
            "La aplicación de una política falló.",
            false,
            new[] { "deviceName", "policyName", "error" }),

        new(
            "AndroidInstallError",
            "android",
            "Error de instalación Android",
            "Falló la instalación de una aplicación.",
            false,
            new[] { "deviceName", "packageName", "error" }),

        // ---- Helpdesk ------------------------------------------
        new(
            "TicketCreated",
            "helpdesk",
            "Ticket creado",
            "Se crea un ticket de helpdesk.",
            false,
            new[]
            {
                "ticketId", "reference", "priority",
                "category", "requesterUserId"
            }),

        new(
            "TicketAssigned",
            "helpdesk",
            "Ticket asignado",
            "El ticket se asigna a un técnico.",
            false,
            new[] { "ticketId", "reference", "assigneeUserId" }),

        new(
            "TicketUnassigned",
            "helpdesk",
            "Ticket sin asignación",
            "El ticket queda sin técnico asignado.",
            false,
            new[] { "ticketId", "reference" }),

        new(
            "TicketPriorityChanged",
            "helpdesk",
            "Cambio de prioridad",
            "Cambia la prioridad del ticket.",
            false,
            new[]
            {
                "ticketId", "reference",
                "previousPriority", "priority"
            }),

        new(
            "TicketStatusChanged",
            "helpdesk",
            "Cambio de estado",
            "Cambia el estado del ticket.",
            false,
            new[]
            {
                "ticketId", "reference",
                "previousStatus", "status"
            }),

        new(
            "TicketSlaNearDue",
            "helpdesk",
            "SLA próximo a vencer",
            "El ticket está cerca de incumplir su SLA.",
            false,
            new[] { "ticketId", "reference", "dueAtUtc" }),

        new(
            "TicketSlaBreached",
            "helpdesk",
            "SLA vencido",
            "El ticket incumple su SLA.",
            false,
            new[] { "ticketId", "reference", "dueAtUtc" }),

        new(
            "TicketNoResponse",
            "helpdesk",
            "Ticket sin respuesta",
            "El ticket no tiene respuesta del técnico.",
            false,
            new[] { "ticketId", "reference" }),

        new(
            "TicketReopened",
            "helpdesk",
            "Ticket reabierto",
            "El ticket se reabre.",
            false,
            new[] { "ticketId", "reference" }),

        // ---- Ponches -------------------------------------------
        new(
            "PonchesSyncFailure",
            "ponches",
            "Fallo de sincronización",
            "La sincronización de ponches falló.",
            false,
            new[] { "clockId", "error" }),

        new(
            "PonchesClockOffline",
            "ponches",
            "Reloj desconectado",
            "Un reloj de ponches está offline.",
            false,
            new[] { "clockId", "siteId" }),

        new(
            "PonchesAnomaly",
            "ponches",
            "Evento anómalo",
            "Se detecta un ponche anómalo.",
            false,
            new[] { "clockId", "employeeId", "reason" }),

        new(
            "PonchesPendingOperation",
            "ponches",
            "Operación pendiente",
            "Hay operaciones pendientes en ponches.",
            false,
            new[] { "clockId", "pendingCount" })
    };

    public static IReadOnlyCollection<
        AutomationEventDescriptor> All =>
        Descriptors;

    public static IReadOnlyCollection<
        AutomationEventDescriptor> Connected =>
        Descriptors
            .Where(x => x.Connected)
            .ToArray();

    public static AutomationEventDescriptor? Find(
        string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return Descriptors.FirstOrDefault(
            x => x.Code.Equals(
                code.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsConnected(string? code)
    {
        return Find(code)?.Connected == true;
    }
}
