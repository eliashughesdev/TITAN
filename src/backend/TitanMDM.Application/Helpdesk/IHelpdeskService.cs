namespace TitanMDM.Application.Helpdesk;

public interface IHelpdeskService
{
    Task<HelpdeskTicketListResult> GetTicketsAsync(
        Guid organizationId,
        HelpdeskTicketQuery query,
        CancellationToken cancellationToken = default);

    Task<HelpdeskTicketDetailsDto?> GetTicketAsync(
        Guid organizationId,
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<HelpdeskTicketDetailsDto> CreateTicketAsync(
        Guid organizationId,
        Guid actorUserId,
        CreateHelpdeskTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<HelpdeskTicketDetailsDto?> AddCommentAsync(
        Guid organizationId,
        Guid ticketId,
        Guid actorUserId,
        AddHelpdeskCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<HelpdeskTicketDetailsDto?> AssignAsync(
        Guid organizationId,
        Guid ticketId,
        Guid actorUserId,
        AssignHelpdeskTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<HelpdeskTicketDetailsDto?> TransitionAsync(
        Guid organizationId,
        Guid ticketId,
        Guid actorUserId,
        TransitionHelpdeskTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<HelpdeskTicketDetailsDto?> ReopenAsync(
        Guid organizationId,
        Guid ticketId,
        Guid actorUserId,
        ReopenHelpdeskTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<HelpdeskRoutingPreviewDto>
        PreviewRoutingDiagnosticAsync(
            Guid organizationId,
            Guid requesterUserId,
            HelpdeskRoutingPreviewRequest request,
            CancellationToken cancellationToken = default);

    Task<HelpdeskRoutingHealthSnapshot> GetRoutingHealthAsync(
    Guid organizationId,
    CancellationToken cancellationToken = default);

    Task<HelpdeskRoutingDiagnosticSnapshot?> GetRoutingDiagnosticAsync(
        Guid organizationId,
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAutomaticAssignmentEnterpriseAsync(
        Guid organizationId,
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<HelpdeskRoutingQueueResult> RetryAutomaticAssignmentForOpenTicketsAsync(
        Guid organizationId,
        int maxTickets,
        bool dryRun,
        CancellationToken cancellationToken = default);
}