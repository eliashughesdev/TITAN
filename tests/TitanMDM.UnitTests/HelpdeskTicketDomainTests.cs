using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Helpdesk;

namespace TitanMDM.UnitTests;

public sealed class HelpdeskTicketDomainTests
{
    [Fact]
    public void NewTicket_ShouldStartInNewStatus()
    {
        var ticket =
            CreateTicket();

        Assert.Equal(
            HelpdeskTicketStatus.New,
            ticket.Status);

        Assert.Null(
            ticket.AssigneeUserId);

        Assert.Null(
            ticket.ResolvedAtUtc);

        Assert.True(
            ticket.CreatedAtUtc <=
            DateTime.UtcNow);
    }

    [Fact]
    public void Assign_ShouldMoveNewTicketToOpen()
    {
        var ticket =
            CreateTicket();

        var technicianId =
            Guid.NewGuid();

        ticket.Assign(
            technicianId);

        Assert.Equal(
            technicianId,
            ticket.AssigneeUserId);

        Assert.Equal(
            HelpdeskTicketStatus.Open,
            ticket.Status);
    }

    [Fact]
    public void Assign_ShouldNotChangeInProgressTicketStatus()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.InProgress);

        var technicianId =
            Guid.NewGuid();

        ticket.Assign(
            technicianId);

        Assert.Equal(
            HelpdeskTicketStatus.InProgress,
            ticket.Status);

        Assert.Equal(
            technicianId,
            ticket.AssigneeUserId);
    }

    [Fact]
    public void New_ShouldAllowOpen()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Open);

        Assert.Equal(
            HelpdeskTicketStatus.Open,
            ticket.Status);
    }

    [Fact]
    public void New_ShouldAllowInProgress()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.InProgress);

        Assert.Equal(
            HelpdeskTicketStatus.InProgress,
            ticket.Status);
    }

    [Fact]
    public void Open_ShouldAllowPendingUser()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Open);

        ticket.Transition(
            HelpdeskTicketStatus.PendingUser);

        Assert.Equal(
            HelpdeskTicketStatus.PendingUser,
            ticket.Status);

        Assert.NotNull(
            ticket.SlaPausedAtUtc);
    }

    [Fact]
    public void PendingUser_ShouldAllowInProgress()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.PendingUser);

        ticket.Transition(
            HelpdeskTicketStatus.InProgress);

        Assert.Equal(
            HelpdeskTicketStatus.InProgress,
            ticket.Status);

        Assert.Null(
            ticket.SlaPausedAtUtc);
    }

    [Fact]
    public void Resolved_ShouldAllowClosed()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Resolved);

        ticket.Transition(
            HelpdeskTicketStatus.Closed);

        Assert.Equal(
            HelpdeskTicketStatus.Closed,
            ticket.Status);

        Assert.NotNull(
            ticket.ResolvedAtUtc);
    }

    [Fact]
    public void Closed_ShouldAllowReopenToOpen()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Resolved);

        ticket.Transition(
            HelpdeskTicketStatus.Closed);

        ticket.Reopen();

        Assert.Equal(
            HelpdeskTicketStatus.Open,
            ticket.Status);

        Assert.Null(
            ticket.ResolvedAtUtc);
    }

    [Fact]
    public void Resolved_ShouldAllowReopenToOpen()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Resolved);

        ticket.Reopen();

        Assert.Equal(
            HelpdeskTicketStatus.Open,
            ticket.Status);

        Assert.Null(
            ticket.ResolvedAtUtc);
    }

    [Fact]
    public void Open_ShouldNotAllowClosedDirectly()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Open);

        var exception =
            Assert.Throws<
                InvalidOperationException>(
                () =>
                    ticket.Transition(
                        HelpdeskTicketStatus.Closed));

        Assert.Contains(
            "no permitida",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void New_ShouldNotAllowClosedDirectly()
    {
        var ticket =
            CreateTicket();

        Assert.Throws<
            InvalidOperationException>(
            () =>
                ticket.Transition(
                    HelpdeskTicketStatus.Closed));
    }

    [Fact]
    public void Closed_ShouldNotAllowInProgressDirectly()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Resolved);

        ticket.Transition(
            HelpdeskTicketStatus.Closed);

        Assert.Throws<
            InvalidOperationException>(
            () =>
                ticket.Transition(
                    HelpdeskTicketStatus.InProgress));
    }

    [Fact]
    public void InvalidStatus_ShouldFail()
    {
        var ticket =
            CreateTicket();

        Assert.Throws<
            ArgumentException>(
            () =>
                ticket.Transition(
                    "invented-status"));
    }

    [Fact]
    public void PendingUser_ShouldPauseSla()
    {
        var ticket =
            CreateTicket();

        var firstResponse =
            DateTime.UtcNow.AddHours(1);

        var resolution =
            DateTime.UtcNow.AddHours(4);

        ticket.ApplySla(
            firstResponse,
            resolution);

        ticket.Transition(
            HelpdeskTicketStatus.PendingUser);

        Assert.NotNull(
            ticket.SlaPausedAtUtc);

        Assert.Null(
            ticket.FirstResponseDueAtUtc);

        Assert.Null(
            ticket.ResolveDueAtUtc);

        Assert.Equal(
            firstResponse,
            ticket.SuspendedFirstResponseDueAtUtc);

        Assert.Equal(
            resolution,
            ticket.SuspendedResolveDueAtUtc);
    }

    [Fact]
    public async Task LeavingPendingUser_ShouldResumeSla()
    {
        var ticket =
            CreateTicket();

        var firstResponse =
            DateTime.UtcNow.AddHours(1);

        var resolution =
            DateTime.UtcNow.AddHours(4);

        ticket.ApplySla(
            firstResponse,
            resolution);

        ticket.Transition(
            HelpdeskTicketStatus.PendingUser);

        await Task.Delay(20);

        ticket.Transition(
            HelpdeskTicketStatus.InProgress);

        Assert.Null(
            ticket.SlaPausedAtUtc);

        Assert.NotNull(
            ticket.FirstResponseDueAtUtc);

        Assert.NotNull(
            ticket.ResolveDueAtUtc);

        Assert.True(
            ticket.FirstResponseDueAtUtc >=
            firstResponse);

        Assert.True(
            ticket.ResolveDueAtUtc >=
            resolution);

        Assert.True(
            ticket.TotalSlaPausedSeconds >= 0);
    }

    [Fact]
    public void ApplySla_ShouldRejectInvalidDeadlineOrder()
    {
        var ticket =
            CreateTicket();

        var firstResponse =
            DateTime.UtcNow.AddHours(5);

        var resolution =
            DateTime.UtcNow.AddHours(2);

        Assert.Throws<
            ArgumentException>(
            () =>
                ticket.ApplySla(
                    firstResponse,
                    resolution));
    }

    [Fact]
    public void MarkFirstResponse_ShouldBeIdempotent()
    {
        var ticket =
            CreateTicket();

        ticket.MarkFirstResponse();

        var first =
            ticket.FirstRespondedAtUtc;

        ticket.MarkFirstResponse();

        Assert.Equal(
            first,
            ticket.FirstRespondedAtUtc);
    }

    [Fact]
    public void Resolve_ShouldPopulateResolvedAt()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Resolved);

        Assert.NotNull(
            ticket.ResolvedAtUtc);
    }

    [Fact]
    public void Reopen_ShouldClearResolvedAt()
    {
        var ticket =
            CreateTicket();

        ticket.Transition(
            HelpdeskTicketStatus.Resolved);

        Assert.NotNull(
            ticket.ResolvedAtUtc);

        ticket.Reopen();

        Assert.Null(
            ticket.ResolvedAtUtc);
    }

    [Fact]
    public void AssignLocationWithoutSite_ShouldFail()
    {
        var ticket =
            CreateTicket();

        Assert.Throws<
            InvalidOperationException>(
            () =>
                ticket.AssignSite(
                    null,
                    Guid.NewGuid()));
    }

    [Fact]
    public void AssignSiteAndLocation_ShouldSucceed()
    {
        var ticket =
            CreateTicket();

        var siteId =
            Guid.NewGuid();

        var locationId =
            Guid.NewGuid();

        ticket.AssignSite(
            siteId,
            locationId);

        Assert.Equal(
            siteId,
            ticket.SiteId);

        Assert.Equal(
            locationId,
            ticket.SiteLocationId);
    }

    [Fact]
    public void Reclassify_ShouldNormalizeCategory()
    {
        var ticket =
            CreateTicket();

        ticket.Reclassify(
            "  NETWORK  ");

        Assert.Equal(
            "network",
            ticket.Category);
    }

    [Fact]
    public void InvalidPriority_ShouldFail()
    {
        var ticket =
            CreateTicket();

        Assert.Throws<
            ArgumentException>(
            () =>
                ticket.ChangePriority(
                    "super-important"));
    }

    [Fact]
    public void Priority_ShouldNormalize()
    {
        var ticket =
            CreateTicket();

        ticket.ChangePriority(
            " HIGH ");

        Assert.Equal(
            "high",
            ticket.Priority);
    }

    [Fact]
    public void InvalidType_ShouldFail()
    {
        var ticket =
            CreateTicket();

        Assert.Throws<
            ArgumentException>(
            () =>
                ticket.ChangeType(
                    "unknown"));
    }

    [Fact]
    public void EmailRequester_ShouldNormalizeEmail()
    {
        var ticket =
            CreateTicket();

        ticket.SetEmailRequester(
            "Test User",
            " USER@EXAMPLE.COM ");

        Assert.Equal(
            "user@example.com",
            ticket.ExternalRequesterEmail);

        Assert.Equal(
            "Test User",
            ticket.ExternalRequesterName);
    }

    [Fact]
    public void EmptyTechnician_ShouldFail()
    {
        var ticket =
            CreateTicket();

        Assert.Throws<
            ArgumentException>(
            () =>
                ticket.Assign(
                    Guid.Empty));
    }

    private static HelpdeskTicket CreateTicket()
    {
        return new HelpdeskTicket(
            organizationId:
                Guid.NewGuid(),
            number:
                "HD-TEST-0001",
            subject:
                "Prueba Helpdesk",
            description:
                "Ticket generado para pruebas unitarias.",
            type:
                "incident",
            priority:
                "medium",
            category:
                "general",
            source:
                "console",
            requesterUserId:
                Guid.NewGuid(),
            deviceId:
                null,
            queueId:
                null);
    }
}