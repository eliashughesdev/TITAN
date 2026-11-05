namespace TitanMDM.Api.Inventory;

public interface IInventoryGateway
{
    Task<InventoryGatewayResponse> SendAsync(
        InventoryGatewayRequest request,

        CancellationToken cancellationToken = default);
}