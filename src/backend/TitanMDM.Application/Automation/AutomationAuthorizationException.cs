namespace TitanMDM.Application.Automation;

/*
 * AUT-1.8 — Error de autorización del motor de automatizaciones.
 *
 * Se lanza cuando un actor intenta una operación fuera de su
 * alcance autorizado (organización o localidad) o cuando una
 * regla programada intentaría ejecutar una acción para la que
 * su propietario no tiene permiso. El controlador lo traduce a
 * HTTP 403 en lugar de un 500 genérico.
 */
public sealed class AutomationAuthorizationException
    : Exception
{
    public AutomationAuthorizationException(
        string message)
        : base(message)
    {
    }
}
