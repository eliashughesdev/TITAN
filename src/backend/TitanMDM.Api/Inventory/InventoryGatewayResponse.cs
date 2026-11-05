namespace TitanMDM.Api.Inventory;

public sealed class InventoryGatewayResponse
{
    public int StatusCode { get; set; }

    public byte[] Body { get; set; } = Array.Empty<byte>();

    public string ContentType { get; set; } = "application/json";

    public string? ContentDisposition { get; set; }

    public InventoryGatewayResponse(
        int statusCode,
        byte[] body,
        string contentType,
        string? contentDisposition)
    {
        StatusCode = statusCode;
        Body = body;
        ContentType = contentType;
        ContentDisposition = contentDisposition;
    }
}