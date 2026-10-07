namespace TitanMDM.Api.Ponches;

public interface IPonchesGateway
{
    Task<PonchesGatewayResponse> SendAsync(
        PonchesGatewayRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PonchesGatewayRequest(
    string Route,
    HttpMethod Method,
    string ActorUserId,
    string OrganizationId,
    bool IsAdministrator,
    IReadOnlyCollection<string> Operations,
    Stream? Body = null,
    string? ContentType = null,
    bool DeviceOperation = false);

public sealed record PonchesGatewayResponse(
    int StatusCode,
    byte[] Body,
    string ContentType,
    string? ContentDisposition);