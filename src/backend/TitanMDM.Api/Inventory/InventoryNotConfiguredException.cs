namespace TitanMDM.Api.Inventory;

public sealed class InventoryNotConfiguredException
    : Exception
{
    public InventoryNotConfiguredException(
        string message)
        : base(message)
    {
    }
}