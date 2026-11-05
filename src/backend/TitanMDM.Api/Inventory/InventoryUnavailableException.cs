namespace TitanMDM.Api.Inventory;

public sealed class InventoryUnavailableException
    : Exception
{
    public InventoryUnavailableException(
        string message,
        Exception? innerException = null)
        : base(
            message,
            innerException)
    {
    }
}