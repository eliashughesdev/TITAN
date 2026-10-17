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

    public const string Create = "automation.create";

    public const string Edit = "automation.edit";

    public const string Manage = "automation.manage";

    public const string Execute = "automation.execute";

    public const string Simulate = "automation.simulate";

    public const string Admin = "automation.admin";

    /*
     * Permisos que, por compatibilidad con el modelo anterior,
     * siguen considerándose equivalentes a una capacidad
     * granular. Un rol con "automation.manage" mantiene las
     * capacidades de creación y edición.
     */
    public static readonly IReadOnlyCollection<string>
        CreateEquivalents = new[]
        {
            Create,
            Manage
        };

    public static readonly IReadOnlyCollection<string>
        EditEquivalents = new[]
        {
            Edit,
            Manage
        };

    public static readonly IReadOnlyCollection<string>
        AdminEquivalents = new[]
        {
            Admin,
            Manage
        };

    /*
     * Devuelve el conjunto de códigos que conceden la
     * capacidad solicitada. "automation.admin" satisface
     * cualquier capacidad del motor y "automation.manage"
     * mantiene las capacidades de ver/crear/editar/ejecutar.
     *
     * Sólo se expanden códigos propios del motor; permisos
     * externos (p. ej. "devices.commands") se devuelven tal
     * cual para no ampliar privilegios fuera del motor.
     */
    public static IReadOnlyCollection<string> GrantingCodes(
        string permissionCode)
    {
        return permissionCode switch
        {
            View => new[] { View, Manage, Admin },
            Create => new[] { Create, Manage, Admin },
            Edit => new[] { Edit, Manage, Admin },
            Manage => new[] { Manage, Admin },
            Execute => new[] { Execute, Admin },
            Simulate => new[] { Simulate, Admin },
            Admin => new[] { Admin, Manage },
            _ => new[] { permissionCode }
        };
    }

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
            Create,
            "Crear automatizaciones",
            "Automation",
            "Crear reglas y definiciones de automatización."
        ),
        (
            Edit,
            "Editar automatizaciones",
            "Automation",
            "Editar, activar, desactivar y programar reglas existentes."
        ),
        (
            Manage,
            "Administrar automatizaciones",
            "Automation",
            "Capacidad agregada de crear y editar automatizaciones (compatibilidad)."
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
        ),
        (
            Admin,
            "Administrar el motor de automatizaciones",
            "Automation",
            "Eliminar reglas y operaciones destructivas del motor."
        )
    };
}
