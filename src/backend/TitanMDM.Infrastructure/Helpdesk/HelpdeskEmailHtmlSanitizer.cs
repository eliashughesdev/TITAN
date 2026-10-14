using System.Net;
using System.Text.RegularExpressions;

using Ganss.Xss;

namespace TitanMDM.Infrastructure.Helpdesk;

/*
 * ============================================================
 * TITANMDM HELPDESK — SANITIZACIÓN DE HTML DE CORREO
 * ============================================================
 *
 * Los correos entrantes pueden contener HTML de firma con
 * imágenes inline (cid:), tablas y estilos. Este servicio:
 *
 *  - sanea el HTML con una lista blanca estricta de etiquetas,
 *    atributos, esquemas y propiedades CSS,
 *  - elimina scripts, manejadores de eventos y esquemas
 *    peligrosos (javascript:, vbscript:, data:),
 *  - fuerza enlaces externos seguros (target=_blank + rel),
 *  - deriva una versión de texto plano para búsqueda y respaldo.
 *
 * El HTML sanitizado es el ÚNICO que se persiste y sirve.
 */

public static class HelpdeskEmailHtmlSanitizer
{
    public const int MaxHtmlLength =
        200_000;

    private static readonly string[] AllowedTags =
    {
        "a", "abbr", "b", "blockquote", "br", "caption", "code",
        "col", "colgroup", "dd", "div", "dl", "dt", "em",
        "figcaption", "figure", "font", "h1", "h2", "h3", "h4",
        "h5", "h6", "hr", "i", "img", "li", "ol", "p", "pre", "q",
        "s", "small", "span", "strike", "strong", "sub", "sup",
        "table", "tbody", "td", "tfoot", "th", "thead", "tr", "u",
        "ul"
    };

    private static readonly string[] AllowedAttributes =
    {
        "align", "alt", "border", "cellpadding", "cellspacing",
        "class", "colspan", "color", "dir", "face", "height",
        "href", "hspace", "lang", "rel", "rowspan", "size", "src",
        "style", "target", "title", "valign", "vspace", "width"
    };

    /*
     * "cid" es imprescindible: las imágenes inline de firma se
     * referencian mediante Content-ID y se resuelven en el
     * cliente hacia el adjunto almacenado.
     */
    private static readonly string[] AllowedSchemes =
    {
        "http", "https", "mailto", "tel", "cid"
    };

    private static readonly string[] AllowedCssProperties =
    {
        "background-color", "border", "border-collapse",
        "border-color", "border-style", "border-width", "color",
        "display", "font", "font-family", "font-size",
        "font-style", "font-weight", "height", "letter-spacing",
        "line-height", "margin", "margin-bottom", "margin-left",
        "margin-right", "margin-top", "max-width", "min-width",
        "padding", "padding-bottom", "padding-left",
        "padding-right", "padding-top", "text-align",
        "text-decoration", "vertical-align", "white-space",
        "width"
    };

    private static readonly Regex LineBreakRegex =
        new(
            @"<\s*(br|/p|/div|/li|/tr|/h[1-6])\s*/?>",
            RegexOptions.IgnoreCase |
            RegexOptions.Compiled);

    private static readonly Regex TagRegex =
        new(
            @"<[^>]+>",
            RegexOptions.Compiled);

    private static readonly Regex HorizontalSpaceRegex =
        new(
            @"[ \t]+",
            RegexOptions.Compiled);

    private static readonly Regex MultiNewlineRegex =
        new(
            @"\n\s*\n\s*\n+",
            RegexOptions.Compiled);

    /*
     * Debe declararse DESPUÉS de las listas permitidas: los
     * inicializadores estáticos se ejecutan en orden textual.
     */
    private static readonly HtmlSanitizer Sanitizer =
        BuildSanitizer();

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer =
            new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();

        foreach (var tag in AllowedTags)
        {
            sanitizer.AllowedTags.Add(
                tag);
        }

        sanitizer.AllowedAttributes.Clear();

        foreach (var attribute in AllowedAttributes)
        {
            sanitizer.AllowedAttributes.Add(
                attribute);
        }

        sanitizer.AllowedSchemes.Clear();

        foreach (var scheme in AllowedSchemes)
        {
            sanitizer.AllowedSchemes.Add(
                scheme);
        }

        sanitizer.AllowedCssProperties.Clear();

        foreach (var property in AllowedCssProperties)
        {
            sanitizer.AllowedCssProperties.Add(
                property);
        }

        sanitizer.AllowedAtRules.Clear();

        sanitizer.PostProcessNode +=
            static (_, args) =>
            {
                if (
                    args.Node
                        is AngleSharp.Dom.IElement element
                    &&
                    string.Equals(
                        element.LocalName,
                        "a",
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    element.SetAttribute(
                        "target",
                        "_blank");

                    element.SetAttribute(
                        "rel",
                        "noopener noreferrer");
                }
            };

        return sanitizer;
    }

    public static string? Sanitize(
        string? html)
    {
        if (
            string.IsNullOrWhiteSpace(
                html))
        {
            return null;
        }

        var input =
            html.Length >
                MaxHtmlLength
                ? html[..MaxHtmlLength]
                : html;

        try
        {
            var clean =
                Sanitizer
                    .Sanitize(
                        input)
                    .Trim();

            return clean.Length ==
                0
                    ? null
                    : clean;
        }
        catch
        {
            /*
             * HTML imposible de procesar: se descarta en lugar
             * de arriesgar render sin sanear.
             */
            return null;
        }
    }

    public static string ToPlainText(
        string? html)
    {
        if (
            string.IsNullOrWhiteSpace(
                html))
        {
            return string.Empty;
        }

        var text =
            LineBreakRegex.Replace(
                html,
                "\n");

        text =
            TagRegex.Replace(
                text,
                " ");

        text =
            WebUtility.HtmlDecode(
                text);

        text =
            text.Replace(
                '\u00A0',
                ' ');

        text =
            HorizontalSpaceRegex.Replace(
                text,
                " ");

        text =
            MultiNewlineRegex.Replace(
                text,
                "\n\n");

        return text.Trim();
    }
}
