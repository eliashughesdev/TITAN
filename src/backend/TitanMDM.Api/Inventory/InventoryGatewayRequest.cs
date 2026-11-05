namespace TitanMDM.Api.Inventory;

public sealed class InventoryGatewayRequest
{
    public InventoryGatewayRequest(
        string route,
        HttpMethod method,
        string? actorUserId,
        string? organizationId,
        bool isAdministrator,
        IEnumerable<string>? operations,
        byte[]? body,
        string? contentType,
        bool deviceOperation)
    {
        Route = route;
        Method = method;
        ActorUserId = actorUserId;
        OrganizationId = organizationId;
        IsAdministrator = isAdministrator;
        Operations = operations;
        Body = body;
        ContentType = contentType;
        DeviceOperation = deviceOperation;
    }

    public InventoryGatewayRequest()
    {
    }

    public string Route { get; set; } = string.Empty;

    public HttpMethod Method { get; set; } = HttpMethod.Get;

    public string? ActorUserId { get; set; }

    public string? OrganizationId { get; set; }

    public bool IsAdministrator { get; set; }

    public IEnumerable<string>? Operations { get; set; }

    public byte[]? Body { get; set; }

    public string? ContentType { get; set; }

    public bool DeviceOperation { get; set; }
}