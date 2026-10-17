using TitanMDM.Application.Automation;
using TitanMDM.Application.Security;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Integrations;

/*
 * AUT-2 — Contratos de herramientas para Titan Assistant y
 * Fiorella.
 *
 * Todas las acciones de escritura permanecen deshabilitadas
 * (WriteEnabled = false) y, cuando aplica, requieren
 * aprobación explícita. Esto documenta la superficie que
 * AUT-2 podrá habilitar sin conceder hoy capacidades nuevas.
 */
internal static class AutomationToolContractCatalog
{
    public static IReadOnlyCollection<AutomationToolContractDto>
        All { get; } = new[]
        {
            new AutomationToolContractDto(
                "RunAssistantTool",
                "Titan Assistant / Fiorella",
                AutomationPermissions.Admin,
                false,
                true,
                "Permite que un asistente invoque una acción " +
                "autorizada. Deshabilitado hasta definir " +
                "aprobación y permisos en AUT-2."),

            new AutomationToolContractDto(
                "AssignTicket",
                "Helpdesk",
                PermissionCodes.Helpdesk.TicketsAssign,
                false,
                true,
                "Asignación de tickets a un técnico concreto. " +
                "Requiere permiso de asignación y aprobación."),

            new AutomationToolContractDto(
                "RequestDeviceSync",
                "Plataforma de dispositivos",
                PermissionCodes.Devices.Commands,
                false,
                true,
                "Fuerza la sincronización de un dispositivo."),

            new AutomationToolContractDto(
                "ApplyPolicy",
                "Cumplimiento",
                PermissionCodes.Policies.Manage,
                false,
                true,
                "Aplica una política aprobada a un dispositivo."),

            new AutomationToolContractDto(
                "GenerateReport",
                "Reportes",
                PermissionCodes.Reports.Export,
                false,
                false,
                "Genera y distribuye un reporte; sólo lectura " +
                "sobre el motor.")
        };
}
