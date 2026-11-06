namespace TitanMDM.Application.Contracts.Persistence;

/// <summary>
/// Regla de negocio de Inventario incumplida: el API la devuelve como 400.
/// </summary>
public sealed class InventoryValidationException : Exception
{
    public InventoryValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
