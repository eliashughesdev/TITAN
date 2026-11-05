namespace TitanMDM.Api.Inventory;

public sealed class InventoryTimeoutException
    : Exception
{
    public InventoryTimeoutException(
        string message)
        : base(message)
    {
    }
}