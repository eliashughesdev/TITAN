using TitanMDM.Domain.Entities;

namespace TitanMDM.UnitTests;

/*
 * ============================================================
 * TITANMDM HELPDESK — CUERPOS HTML (firmas de correo)
 * ============================================================
 *
 * Verifica la persistencia controlada del HTML sanitizado en
 * tickets y comentarios.
 */

public sealed class HelpdeskEmailHtmlBodiesTests
{
    [Fact]
    public void TicketStoresAndClearsDescriptionHtml()
    {
        var ticket =
            CreateTicket();

        Assert.Null(
            ticket.DescriptionHtml);

        ticket.SetDescriptionHtml(
            "<b>Firma</b>");

        Assert.Equal(
            "<b>Firma</b>",
            ticket.DescriptionHtml);

        ticket.SetDescriptionHtml(
            "   ");

        Assert.Null(
            ticket.DescriptionHtml);
    }

    [Fact]
    public void CommentStoresAndClearsBodyHtml()
    {
        var comment =
            new HelpdeskTicketComment(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "cuerpo",
                isInternal:
                    false);

        Assert.Null(
            comment.BodyHtml);

        comment.SetBodyHtml(
            "<img src=\"cid:logo@x\" />");

        Assert.Equal(
            "<img src=\"cid:logo@x\" />",
            comment.BodyHtml);

        comment.SetBodyHtml(
            null);

        Assert.Null(
            comment.BodyHtml);
    }

    [Fact]
    public void HtmlIsCappedToMaximumLength()
    {
        var ticket =
            CreateTicket();

        var oversized =
            new string(
                'a',
                250_000);

        ticket.SetDescriptionHtml(
            oversized);

        Assert.NotNull(
            ticket.DescriptionHtml);

        Assert.Equal(
            200_000,
            ticket.DescriptionHtml!
                .Length);
    }

    private static HelpdeskTicket CreateTicket()
    {
        return new HelpdeskTicket(
            Guid.NewGuid(),
            (
                "HD-" +
                Guid.NewGuid()
                    .ToString("N")
            )[..20],
            "Asunto",
            "Descripción",
            "incident",
            "medium",
            "general",
            "email",
            Guid.NewGuid(),
            null,
            null);
    }
}
