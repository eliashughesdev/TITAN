namespace TitanMDM.Application.Automation;

/*
 * AUT-1.8 — Códigos de permiso del motor de automatización.
 *
 * Se definen en un archivo propio (no en PermissionCodes.cs)
 * para no tocar archivos compartidos mientras otras pestañas
 * trabajan. El sembrado se realiza desde la propia extensión
 * de DI del motor (AddTitanAutomationPlatform).
 */
public static class AutomationPermissions
{
    public const string View = "automation.view";

    public const string Manage = "automation.manage";

    public const string Execute = "automation.execute";

    public const string Simulate = "automation.simulate";

    public static readonly IReadOnlyList<
        (string Code, string Name, string Module,
            string Description)> All = new[]
    {
        (
            View,
            "Ver automatizaciones",
            "Automation",
            "Ver reglas, ejecuciones y tablero de automatizaciones."
        ),
        (
            Manage,
            "Administrar automatizaciones",
            "Automation",
            "Crear, editar, activar, desactivar y eliminar reglas."
        ),
        (
            Execute,
            "Ejecutar automatizaciones",
            "Automation",
            "Ejecutar reglas manualmente y reintentar ejecuciones."
        ),
        (
            Simulate,
            "Simular automatizaciones",
            "Automation",
            "Validar y simular reglas sin producir efectos reales."
        )
    };
}
