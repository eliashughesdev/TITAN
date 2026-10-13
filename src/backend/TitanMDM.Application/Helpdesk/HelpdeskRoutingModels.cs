namespace TitanMDM.Application.Helpdesk;

public sealed record HelpdeskRoutingHealthSnapshot(
    int OpenTickets,
    int UnassignedTickets,
    int PendingUserTickets,
    int AutoAssignedLast24Hours,
    int RoutingWaitingLast24Hours,
    int SlaEscalatedLast24Hours,
    double AverageAutoAssignMinutesLast24Hours,
    int ActiveTeams,
    int ActiveMembers,
    int MembersOnDutyNow,
    int AutomaticRetriesPending,
    int FirstResponseSlaOverdue,
    int ResolutionSlaOverdue);

public sealed record HelpdeskRoutingDiagnosticSnapshot(
    Guid TicketId,
    string TicketNumber,
    string Status,
    string Category,
    string? Subject,
    string? RequesterEmail,
    Guid? RequesterUserId,
    Guid? SiteId,
    string? SiteName,
    Guid? SiteLocationId,
    string? SiteLocationName,
    Guid? RequestedTeamId,
    Guid? AssigneeUserId,
    bool AlreadyAssigned,
    bool PendingUser,
    string EngineReason,
    IReadOnlyList<string> RequesterBlockReasons,
    HelpdeskRoutingCandidatePreview? SelectedCandidate,
    IReadOnlyList<HelpdeskRoutingCandidatePreview> Candidates);

public sealed record HelpdeskRoutingCandidatePreview(
    Guid TeamId,
    string TeamName,
    Guid UserId,
    string TechnicianName,
    string? Email,
    bool IsAvailable,
    bool AcceptsAutomaticAssignments,
    int Capacity,
    int OpenTickets,
    bool HasSchedule,
    bool OnDutyNow,
    string Decision,
    string Reason);

public sealed record HelpdeskRoutingQueueItemResult(
    Guid TicketId,
    string TicketNumber,
    string Category,
    string Status,
    string Outcome,
    string Reason);

public sealed record HelpdeskRoutingQueueResult(
    int Considered,
    int Assigned,
    int Skipped,
    int Failed,
    bool DryRun,
    IReadOnlyList<HelpdeskRoutingQueueItemResult> Items);
