using TitanMDM.Infrastructure.Helpdesk;

namespace TitanMDM.UnitTests;

/*
 * ============================================================
 * TITANMDM HELPDESK — SANITIZACIÓN DE HTML DE CORREO
 * ============================================================
 *
 * Garantiza que el HTML entrante se neutraliza antes de
 * persistirse o renderizarse, conservando firmas e imágenes
 * inline seguras (cid:).
 */

public sealed class HelpdeskEmailHtmlSanitizerTests
{
    [Fact]
    public void RemovesScriptsAndEventHandlers()
    {
        var result =
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "<p onclick=\"alert(1)\">Hola" +
                "<script>alert('x')</script></p>");

        Assert.NotNull(result);

        Assert.DoesNotContain(
            "script",
            result!,
            StringComparison
                .OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "onclick",
            result!,
            StringComparison
                .OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "alert",
            result!,
            StringComparison
                .OrdinalIgnoreCase);

        Assert.Contains(
            "Hola",
            result!);
    }

    [Fact]
    public void RemovesJavascriptScheme()
    {
        var result =
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "<a href=\"javascript:alert(1)\">x</a>");

        Assert.DoesNotContain(
            "javascript:",
            result ??
                string.Empty,
            StringComparison
                .OrdinalIgnoreCase);
    }

    [Fact]
    public void KeepsInlineCidImage()
    {
        var result =
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "<img src=\"cid:logo@signature\" alt=\"firma\" />");

        Assert.NotNull(result);

        Assert.Contains(
            "cid:logo@signature",
            result!);
    }

    [Fact]
    public void RemovesDataSchemeImage()
    {
        var result =
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "<img src=\"data:text/html;base64,PHNjcmlwdD4=\" />");

        Assert.DoesNotContain(
            "data:",
            result ??
                string.Empty,
            StringComparison
                .OrdinalIgnoreCase);
    }

    [Fact]
    public void ForcesSafeExternalLinkAttributes()
    {
        var result =
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "<a href=\"https://example.com\">Enlace</a>");

        Assert.NotNull(result);

        Assert.Contains(
            "target=\"_blank\"",
            result!);

        Assert.Contains(
            "noopener noreferrer",
            result!);
    }

    [Fact]
    public void KeepsSafeFormattingTags()
    {
        var result =
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "<b>Negrita</b> y <em>cursiva</em>");

        Assert.NotNull(result);

        Assert.Contains(
            "<b>",
            result!);

        Assert.Contains(
            "<em>",
            result!);
    }

    [Fact]
    public void ReturnsNullForEmptyOrWhitespace()
    {
        Assert.Null(
            HelpdeskEmailHtmlSanitizer.Sanitize(
                null));

        Assert.Null(
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "   "));
    }

    [Fact]
    public void ReturnsNullWhenOnlyUnsafeMarkupRemains()
    {
        Assert.Null(
            HelpdeskEmailHtmlSanitizer.Sanitize(
                "<script>alert(1)</script>"));
    }

    [Fact]
    public void ToPlainTextStripsTagsAndDecodesEntities()
    {
        var text =
            HelpdeskEmailHtmlSanitizer.ToPlainText(
                "<p>Hola&nbsp;mundo<br>linea2</p>");

        Assert.Contains(
            "Hola mundo",
            text);

        Assert.Contains(
            "linea2",
            text);

        Assert.DoesNotContain(
            "<",
            text);
    }
}
