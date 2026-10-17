namespace TitanMDM.Application.Assistant;

/// <summary>
/// Control de cuota de uso del asistente por usuario y
/// organización. Evita que una cuenta consuma recursos del
/// proveedor sin límite. La cuota no concede ningún permiso.
/// </summary>
public interface IAssistantQuota
{
    /// <summary>
    /// Intenta consumir una unidad de cuota. Devuelve <c>false</c>
    /// cuando el usuario superó el límite de la ventana actual.
    /// </summary>
    bool TryConsume(
        Guid organizationId,
        Guid userId);
}
