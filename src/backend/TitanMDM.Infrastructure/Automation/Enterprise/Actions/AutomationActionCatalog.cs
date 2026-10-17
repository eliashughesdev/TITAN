namespace TitanMDM.Infrastructure.Automation.Enterprise.Actions;

internal sealed record AutomationActionParameter(
    string Name,
    string Label,
    bool Required,
    string? DefaultValue = null);

internal sealed record AutomationActionDescriptor(
    string Code,
    string Module,
    string Label,
    string Description,
    string Impact,
    bool RequiresDevice,
    bool Enabled,
    IReadOnlyCollection<AutomationActionParameter>
        Parameters);

/*
 * AUT-1.4 — Registro de acciones permitidas.
 *
 * Las acciones de alto impacto se registran como
 * "Enabled = false" (stub) y no pueden activarse todavía.
 */
internal static class AutomationActionCatalog
{
    private static readonly AutomationActionDescriptor[]
        Descriptors =
    {
        // ---- Comandos de dispositivo (Windows / Android) -------
        new(
            "RequestLocation",
            "general",
            "Solicitar ubicación",
            "Pide al dispositivo reportar su ubicación.",
            "low",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "SecurityScan",
            "compliance",
            "Escaneo de seguridad",
            "Solicita el estado de seguridad del equipo.",
            "low",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "ComplianceScan",
            "compliance",
            "Escaneo de cumplimiento",
            "Solicita una comprobación de cumplimiento.",
            "low",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "AppInventory",
            "inventory",
            "Inventario de aplicaciones",
            "Solicita el inventario de aplicaciones.",
            "low",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "WindowsInventory",
            "inventory",
            "Inventario Windows",
            "Solicita el inventario del equipo Windows.",
            "low",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "WindowsUpdateScan",
            "windows",
            "Escaneo de Windows Update",
            "Solicita un escaneo de actualizaciones.",
            "low",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "WindowsUpdateInstall",
            "windows",
            "Instalar actualizaciones Windows",
            "Instala actualizaciones indicadas.",
            "medium",
            true,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "kbArticleIds",
                    "Artículos KB (separados por coma)",
                    false),
                new AutomationActionParameter(
                    "acceptEula",
                    "Aceptar EULA",
                    false,
                    "true"),
                new AutomationActionParameter(
                    "downloadOnly",
                    "Solo descarga",
                    false,
                    "false")
            }),

        new(
            "RestartDevice",
            "windows",
            "Reiniciar equipo",
            "Reinicia el equipo.",
            "medium",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "LockDevice",
            "general",
            "Bloquear equipo",
            "Bloquea el equipo.",
            "medium",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "EnableLostMode",
            "general",
            "Activar modo perdido",
            "Activa el modo perdido con un mensaje.",
            "high",
            true,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "message",
                    "Mensaje",
                    false,
                    "Dispositivo administrado por TitanMDM."),
                new AutomationActionParameter(
                    "phoneNumber",
                    "Teléfono de contacto",
                    false)
            }),

        new(
            "DisableLostMode",
            "general",
            "Desactivar modo perdido",
            "Desactiva el modo perdido.",
            "medium",
            true,
            true,
            Array.Empty<AutomationActionParameter>()),

        new(
            "SendCommand",
            "general",
            "Enviar comando",
            "Envía un comando arbitrario autorizado.",
            "medium",
            true,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "commandType",
                    "Tipo de comando",
                    true),
                new AutomationActionParameter(
                    "payloadJson",
                    "Payload JSON",
                    false,
                    "{}")
            }),

        // ---- Acciones empresariales ----------------------------
        new(
            "CreateNotification",
            "notifications",
            "Crear notificación",
            "Crea una notificación en la bandeja del centro de operaciones.",
            "low",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "title", "Título", true),
                new AutomationActionParameter(
                    "message", "Mensaje", true),
                new AutomationActionParameter(
                    "severity", "Severidad (info|warning|critical)",
                    false,
                    "info"),
                new AutomationActionParameter(
                    "targetRole", "Rol destino", false),
                new AutomationActionParameter(
                    "targetUserId", "Usuario destino", false)
            }),

        new(
            "Notification",
            "notifications",
            "Notificación (compat.)",
            "Alias compatible de Crear notificación.",
            "low",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "title", "Título", true),
                new AutomationActionParameter(
                    "message", "Mensaje", true),
                new AutomationActionParameter(
                    "severity", "Severidad (info|warning|critical)",
                    false,
                    "info")
            }),

        new(
            "SendAviso",
            "notifications",
            "Enviar aviso",
            "Envía un aviso por el canal indicado (in_app|email).",
            "low",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "message", "Mensaje", true),
                new AutomationActionParameter(
                    "channel",
                    "Canal (in_app|email)",
                    false,
                    "in_app"),
                new AutomationActionParameter(
                    "severity", "Severidad", false,
                    "info")
            }),

        new(
            "RegisterAlert",
            "notifications",
            "Registrar alerta",
            "Registra una alerta en auditoría y bandeja.",
            "low",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "message", "Mensaje", true),
                new AutomationActionParameter(
                    "severity", "Severidad", false,
                    "warning")
            }),

        new(
            "EscalateTicket",
            "helpdesk",
            "Escalar ticket",
            "Reasigna el ticket mediante la lógica de enrutamiento existente.",
            "medium",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "ticketId", "Id del ticket", true),
                new AutomationActionParameter(
                    "useFallback",
                    "Usar fallback de asignación",
                    false,
                    "true")
            }),

        new(
            "RequestRuleReevaluation",
            "general",
            "Solicitar reevaluación de reglas",
            "Vuelve a evaluar las reglas de un evento (con control de ciclos).",
            "low",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "trigger", "Evento a reevaluar", true)
            }),

        new(
            "ScheduleTask",
            "general",
            "Programar una tarea",
            "Encola una acción autorizada para ejecutarse tras un retraso.",
            "low",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "delaySeconds", "Retraso (segundos)",
                    true,
                    "60"),
                new AutomationActionParameter(
                    "actionCode", "Código de acción", true),
                new AutomationActionParameter(
                    "actionParamsJson",
                    "Parámetros JSON",
                    false,
                    "{}")
            }),

        new(
            "CreateFollowUpRecord",
            "general",
            "Crear registro de seguimiento",
            "Registra un seguimiento en auditoría para su revisión.",
            "low",
            false,
            true,
            new[]
            {
                new AutomationActionParameter(
                    "summary", "Resumen", true),
                new AutomationActionParameter(
                    "targetType", "Tipo de destino",
                    false,
                    "Device")
            }),

        // ---- Alto impacto (stub deshabilitado) -----------------
        new(
            "AssignTicket",
            "helpdesk",
            "Asignar ticket",
            "Asigna el ticket a un técnico concreto (pendiente AUT-2).",
            "high",
            false,
            false,
            Array.Empty<AutomationActionParameter>()),

        new(
            "RequestDeviceSync",
            "general",
            "Solicitar sincronización de dispositivos",
            "Fuerza la sincronización de un dispositivo (pendiente AUT-2).",
            "high",
            true,
            false,
            Array.Empty<AutomationActionParameter>()),

        new(
            "ApplyPolicy",
            "compliance",
            "Aplicar política autorizada",
            "Aplica una política aprobada (pendiente AUT-2).",
            "high",
            true,
            false,
            Array.Empty<AutomationActionParameter>()),

        new(
            "GenerateReport",
            "general",
            "Generar reporte",
            "Genera y distribuye un reporte (pendiente AUT-2).",
            "high",
            false,
            false,
            Array.Empty<AutomationActionParameter>()),

        new(
            "RunAssistantTool",
            "general",
            "Ejecutar herramienta de Titan Assistant o Fiorella",
            "Invoca una herramienta de asistente (pendiente AUT-2).",
            "high",
            false,
            false,
            Array.Empty<AutomationActionParameter>())
    };

    public static IReadOnlyCollection<
        AutomationActionDescriptor> All =>
        Descriptors;

    public static IReadOnlyCollection<
        AutomationActionDescriptor> Enabled =>
        Descriptors
            .Where(x => x.Enabled)
            .ToArray();

    public static AutomationActionDescriptor? Find(
        string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return Descriptors.FirstOrDefault(
            x => x.Code.Equals(
                code.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsEnabled(string? code)
    {
        return Find(code)?.Enabled == true;
    }
}
