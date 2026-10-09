using System.Globalization;
using System.Text;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Reports;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Reports;

public sealed class ReportsService
    : IReportsService
{
    private readonly TitanMdmDbContext
        _dbContext;

    public ReportsService(
        TitanMdmDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    // ============================================================
    // OVERVIEW
    // ============================================================

    public async Task<ReportsOverviewDto>
        GetOverviewAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        /*
         * ========================================================
         * VISIBLE DEVICES
         * ========================================================
         *
         * null:
         * Organization scope.
         *
         * []:
         * usuario sin Sites.
         *
         * [ids]:
         * únicamente dispositivos pertenecientes a esos Sites.
         * ========================================================
         */

        var devices =
            GetVisibleDevices(
                organizationId,
                accessibleSiteIds);

        var visibleDeviceIds =
            devices.Select(
                device =>
                    device.Id);

        /*
         * ========================================================
         * FLEET
         * ========================================================
         */

        var totalDevices =
            await devices
                .CountAsync(
                    cancellationToken);

        var online =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Online,
                    cancellationToken);

        var offline =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Offline,
                    cancellationToken);

        var managed =
            await devices
                .CountAsync(
                    device =>
                        device.IsManaged,
                    cancellationToken);

        var windows =
            await devices
                .CountAsync(
                    device =>
                        device.Platform ==
                            DevicePlatform.Windows,
                    cancellationToken);

        var android =
            await devices
                .CountAsync(
                    device =>
                        device.Platform ==
                            DevicePlatform.Android,
                    cancellationToken);

        var compliant =
            await devices
                .CountAsync(
                    device =>
                        device.ComplianceStatus ==
                            ComplianceStatus.Compliant,
                    cancellationToken);

        var nonCompliant =
            await devices
                .CountAsync(
                    device =>
                        device.ComplianceStatus ==
                            ComplianceStatus.NonCompliant,
                    cancellationToken);

        var quarantined =
            await devices
                .CountAsync(
                    device =>
                        device.Status ==
                            DeviceStatus.Quarantined
                        ||
                        device.ComplianceStatus ==
                            ComplianceStatus.Quarantined,
                    cancellationToken);

        var fleet =
            new FleetOverviewDto(
                totalDevices,
                online,
                offline,
                managed,
                Math.Max(
                    totalDevices - managed,
                    0),
                windows,
                android,
                compliant,
                nonCompliant,
                quarantined);

        /*
         * ========================================================
         * COMMANDS LAST 30 DAYS
         * ========================================================
         */

        var fromUtc =
            DateTime.UtcNow
                .AddDays(
                    -30);

        var commands =
            _dbContext
                .DeviceCommands
                .AsNoTracking()
                .Where(
                    command =>
                        command.OrganizationId ==
                            organizationId
                        &&
                        visibleDeviceIds.Contains(
                            command.DeviceId)
                        &&
                        command.CreatedAtUtc >=
                            fromUtc);

        var totalCommands =
            await commands
                .CountAsync(
                    cancellationToken);

        var success =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Success,
                    cancellationToken);

        var failed =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Failed,
                    cancellationToken);

        var timeout =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Timeout,
                    cancellationToken);

        var cancelled =
            await commands
                .CountAsync(
                    command =>
                        command.Status ==
                            DeviceCommandStatus.Cancelled,
                    cancellationToken);

        var active =
            Math.Max(
                totalCommands
                - success
                - failed
                - timeout
                - cancelled,
                0);

        var successRate =
            totalCommands == 0
                ? 0M
                : Math.Round(
                    success
                    /
                    (decimal)totalCommands
                    *
                    100M,
                    1);

        var commandOverview =
            new CommandOverviewDto(
                totalCommands,
                success,
                failed,
                timeout,
                cancelled,
                active,
                successRate);

        /*
         * ========================================================
         * SECURITY
         * ========================================================
         */

        var securityQuery =
            _dbContext
                .DeviceSecurityPostures
                .AsNoTracking()
                .Where(
                    posture =>
                        posture.OrganizationId ==
                            organizationId
                        &&
                        visibleDeviceIds.Contains(
                            posture.DeviceId));

        var evaluatedDevices =
            await securityQuery
                .CountAsync(
                    cancellationToken);

        var averageScore =
            evaluatedDevices == 0
                ? 0M
                : Math.Round(
                    await securityQuery
                        .AverageAsync(
                            posture =>
                                (decimal)
                                posture.ComplianceScore,
                            cancellationToken),
                    1);

        var critical =
            await securityQuery
                .CountAsync(
                    posture =>
                        posture.RiskLevel ==
                            "Critical",
                    cancellationToken);

        var high =
            await securityQuery
                .CountAsync(
                    posture =>
                        posture.RiskLevel ==
                            "High",
                    cancellationToken);

        var security =
            new SecurityOverviewDto(
                evaluatedDevices,
                averageScore,
                critical,
                high);

        /*
         * ========================================================
         * OPERATING SYSTEM BREAKDOWN
         * ========================================================
         */

        var operatingSystemsRows =
            await devices
                .GroupBy(
                    device =>
                        device.OperatingSystem
                        ??
                        "N/D")
                .Select(
                    group =>
                        new
                        {
                            Label =
                                group.Key,

                            Value =
                                group.Count()
                        })
                .OrderByDescending(
                    item =>
                        item.Value)
                .Take(
                    8)
                .ToArrayAsync(
                    cancellationToken);

        var operatingSystems =
            operatingSystemsRows
                .Select(
                    item =>
                        new ReportBreakdownDto(
                            item.Label,
                            item.Value))
                .ToArray();

        /*
         * ========================================================
         * DEPARTMENTS
         * ========================================================
         */

        var departmentsRows =
            await devices
                .GroupBy(
                    device =>
                        device.Department
                        ??
                        "Sin departamento")
                .Select(
                    group =>
                        new
                        {
                            Label =
                                group.Key,

                            Value =
                                group.Count()
                        })
                .OrderByDescending(
                    item =>
                        item.Value)
                .Take(
                    8)
                .ToArrayAsync(
                    cancellationToken);

        var departments =
            departmentsRows
                .Select(
                    item =>
                        new ReportBreakdownDto(
                            item.Label,
                            item.Value))
                .ToArray();

        /*
         * ========================================================
         * COMMAND TYPES
         * ========================================================
         */

        var commandTypesRows =
            await commands
                .GroupBy(
                    command =>
                        command.CommandType)
                .Select(
                    group =>
                        new
                        {
                            Label =
                                group.Key,

                            Value =
                                group.Count()
                        })
                .OrderByDescending(
                    item =>
                        item.Value)
                .Take(
                    10)
                .ToArrayAsync(
                    cancellationToken);

        var commandTypes =
            commandTypesRows
                .Select(
                    item =>
                        new ReportBreakdownDto(
                            item.Label,
                            item.Value))
                .ToArray();

        /*
         * ========================================================
         * RESULT
         * ========================================================
         */

        return new ReportsOverviewDto(
            DateTime.UtcNow,
            fleet,
            commandOverview,
            security,
            operatingSystems,
            departments,
            commandTypes);
    }

    // ============================================================
    // CSV
    // ============================================================

    public async Task<byte[]>
        ExportDevicesCsvAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default)
    {
        var devices =
            await GetDevicesForExportAsync(
                organizationId,
                accessibleSiteIds,
                cancellationToken);

        var builder =
            new StringBuilder();

        /*
         * UTF-8 BOM mejora compatibilidad con Excel
         * en equipos Windows y conserva acentos.
         */

        builder.Append(
            '\uFEFF');

        builder.AppendLine(
            "Nombre,Plataforma,Estado,Cumplimiento,Serial," +
            "Fabricante,Modelo,SistemaOperativo,Version,Usuario," +
            "Departamento,IP,Administrado,UltimoContactoUTC");

        foreach (
            var device
            in devices)
        {
            builder.AppendLine(
                string.Join(
                    ",",

                    Csv(
                        device.DeviceName),

                    Csv(
                        device.Platform
                            .ToString()),

                    Csv(
                        device.Status
                            .ToString()),

                    Csv(
                        device.ComplianceStatus
                            .ToString()),

                    Csv(
                        device.SerialNumber),

                    Csv(
                        device.Manufacturer
                        ??
                        string.Empty),

                    Csv(
                        device.Model
                        ??
                        string.Empty),

                    Csv(
                        device.OperatingSystem
                        ??
                        string.Empty),

                    Csv(
                        device.OperatingSystemVersion
                        ??
                        string.Empty),

                    Csv(
                        device.AssignedUser
                        ??
                        string.Empty),

                    Csv(
                        device.Department
                        ??
                        string.Empty),

                    Csv(
                        device.IpAddress
                        ??
                        string.Empty),

                    Csv(
                        device.IsManaged
                            ? "Sí"
                            : "No"),

                    Csv(
                        device.LastSeenAtUtc
                            ?.ToString(
                                "O",
                                CultureInfo.InvariantCulture)
                        ??
                        string.Empty)));
        }

        return Encoding
            .UTF8
            .GetBytes(
                builder.ToString());
    }

    // ============================================================
    // XLSX
    // ============================================================

    public async Task<byte[]>
        ExportDevicesExcelAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default)
    {
        var devices =
            await GetDevicesForExportAsync(
                organizationId,
                accessibleSiteIds,
                cancellationToken);

        using var stream =
            new MemoryStream();

        using (
            var document =
                SpreadsheetDocument.Create(
                    stream,
                    SpreadsheetDocumentType.Workbook,
                    true))
        {
            var workbookPart =
                document.AddWorkbookPart();

            workbookPart.Workbook =
                new Workbook();

            var stylesPart =
                workbookPart
                    .AddNewPart<
                        WorkbookStylesPart>();

            stylesPart.Stylesheet =
                CreateStylesheet();

            stylesPart
                .Stylesheet
                .Save();

            var worksheetPart =
                workbookPart
                    .AddNewPart<
                        WorksheetPart>();

            var sheetData =
                new SheetData();

            var columns =
                new Columns(
                    CreateColumn(
                        1,
                        1,
                        30),

                    CreateColumn(
                        2,
                        4,
                        18),

                    CreateColumn(
                        5,
                        5,
                        22),

                    CreateColumn(
                        6,
                        8,
                        24),

                    CreateColumn(
                        9,
                        9,
                        16),

                    CreateColumn(
                        10,
                        11,
                        24),

                    CreateColumn(
                        12,
                        12,
                        18),

                    CreateColumn(
                        13,
                        13,
                        14),

                    CreateColumn(
                        14,
                        14,
                        25));

            worksheetPart.Worksheet =
                new Worksheet(
                    columns,
                    sheetData);

            var header =
                new[]
                {
                    "Nombre",
                    "Plataforma",
                    "Estado",
                    "Cumplimiento",
                    "Serial",
                    "Fabricante",
                    "Modelo",
                    "Sistema Operativo",
                    "Versión",
                    "Usuario",
                    "Departamento",
                    "IP",
                    "Administrado",
                    "Último contacto UTC"
                };

            var headerRow =
                new Row();

            foreach (
                var value
                in header)
            {
                headerRow.Append(
                    CreateTextCell(
                        value,
                        styleIndex:
                            1));
            }

            sheetData.Append(
                headerRow);

            foreach (
                var device
                in devices)
            {
                var row =
                    new Row();

                row.Append(
                    CreateTextCell(
                        device.DeviceName),

                    CreateTextCell(
                        device.Platform
                            .ToString()),

                    CreateTextCell(
                        device.Status
                            .ToString()),

                    CreateTextCell(
                        device.ComplianceStatus
                            .ToString()),

                    CreateTextCell(
                        device.SerialNumber),

                    CreateTextCell(
                        device.Manufacturer
                        ??
                        string.Empty),

                    CreateTextCell(
                        device.Model
                        ??
                        string.Empty),

                    CreateTextCell(
                        device.OperatingSystem
                        ??
                        string.Empty),

                    CreateTextCell(
                        device.OperatingSystemVersion
                        ??
                        string.Empty),

                    CreateTextCell(
                        device.AssignedUser
                        ??
                        string.Empty),

                    CreateTextCell(
                        device.Department
                        ??
                        string.Empty),

                    CreateTextCell(
                        device.IpAddress
                        ??
                        string.Empty),

                    CreateTextCell(
                        device.IsManaged
                            ? "Sí"
                            : "No"),

                    CreateTextCell(
                        device.LastSeenAtUtc
                            ?.ToString(
                                "yyyy-MM-dd HH:mm:ss 'UTC'",
                                CultureInfo.InvariantCulture)
                        ??
                        string.Empty));

                sheetData.Append(
                    row);
            }

            var sheets =
                workbookPart
                    .Workbook
                    .AppendChild(
                        new Sheets());

            sheets.Append(
                new Sheet
                {
                    Id =
                        workbookPart
                            .GetIdOfPart(
                                worksheetPart),

                    SheetId =
                        1,

                    Name =
                        "Dispositivos"
                });

            worksheetPart
                .Worksheet
                .Save();

            workbookPart
                .Workbook
                .Save();
        }

        return stream
            .ToArray();
    }

    // ============================================================
    // PDF
    // ============================================================

    public async Task<byte[]>
        ExportDevicesPdfAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken = default)
    {
        var devices =
            await GetDevicesForExportAsync(
                organizationId,
                accessibleSiteIds,
                cancellationToken);

        var lines =
            new List<string>
            {
                "TitanMDM Enterprise - Reporte de dispositivos",

                $"Generado UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",

                $"Total de dispositivos: {devices.Count}",

                string.Empty,

                "Nombre | Plataforma | Estado | Cumplimiento | SO | Usuario | IP"
            };

        foreach (
            var device
            in devices)
        {
            lines.Add(
                JoinPdfColumns(
                    device.DeviceName,

                    device.Platform
                        .ToString(),

                    device.Status
                        .ToString(),

                    device.ComplianceStatus
                        .ToString(),

                    BuildOperatingSystem(
                        device),

                    device.AssignedUser
                    ??
                    string.Empty,

                    device.IpAddress
                    ??
                    string.Empty));
        }

        return BuildPdf(
            lines);
    }

    // ============================================================
    // VISIBLE DEVICES
    // ============================================================

    private IQueryable<Device>
        GetVisibleDevices(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds)
    {
        ValidateOrganization(
            organizationId);

        var query =
            _dbContext
                .Devices
                .AsNoTracking()
                .Where(
                    device =>
                        device.OrganizationId ==
                            organizationId
                        &&
                        !device.IsDeleted);

        /*
         * null significa Organization Scope.
         */

        if (
            accessibleSiteIds is null)
        {
            return query;
        }

        var siteIds =
            accessibleSiteIds
                .Where(
                    id =>
                        id != Guid.Empty)
                .Distinct()
                .ToArray();

        /*
         * El usuario posee permiso funcional,
         * pero no tiene Site asignado.
         *
         * Devolvemos consulta vacía.
         *
         * No devolvemos datos corporativos por accidente.
         */

        if (
            siteIds.Length ==
            0)
        {
            return query.Where(
                _ =>
                    false);
        }

        /*
         * Un usuario regional solamente obtiene
         * equipos con Site explícito dentro de su Scope.
         *
         * Dispositivos sin Site quedan reservados para
         * Organization Scope.
         */

        return query.Where(
            device =>
                device.SiteId.HasValue
                &&
                siteIds.Contains(
                    device.SiteId.Value));
    }

    // ============================================================
    // EXPORT DATA
    // ============================================================

    private async Task<List<Device>>
        GetDevicesForExportAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid>? accessibleSiteIds,
            CancellationToken cancellationToken)
    {
        return await GetVisibleDevices(
                organizationId,
                accessibleSiteIds)
            .OrderBy(
                device =>
                    device.DeviceName)
            .ThenBy(
                device =>
                    device.Id)
            .ToListAsync(
                cancellationToken);
    }

    // ============================================================
    // EXCEL HELPERS
    // ============================================================

    private static Stylesheet
        CreateStylesheet()
    {
        var fonts =
            new Fonts(
                new Font(),

                new Font(
                    new Bold()));

        var fills =
            new Fills(
                new Fill(
                    new PatternFill
                    {
                        PatternType =
                            PatternValues.None
                    }),

                new Fill(
                    new PatternFill
                    {
                        PatternType =
                            PatternValues.Gray125
                    }));

        var borders =
            new Borders(
                new Border());

        var cellStyleFormats =
            new CellStyleFormats(
                new CellFormat());

        var cellFormats =
            new CellFormats(
                new CellFormat(),

                new CellFormat
                {
                    FontId =
                        1,

                    ApplyFont =
                        true
                });

        return new Stylesheet(
            fonts,
            fills,
            borders,
            cellStyleFormats,
            cellFormats);
    }

    private static Column
        CreateColumn(
            uint min,
            uint max,
            double width)
    {
        return new Column
        {
            Min =
                min,

            Max =
                max,

            Width =
                width,

            CustomWidth =
                true
        };
    }

    private static Cell
        CreateTextCell(
            string? value,
            uint styleIndex = 0)
    {
        return new Cell
        {
            DataType =
                CellValues.InlineString,

            StyleIndex =
                styleIndex,

            InlineString =
                new InlineString(
                    new Text(
                        value
                        ??
                        string.Empty)
                    {
                        Space =
                            SpaceProcessingModeValues
                                .Preserve
                    })
        };
    }

    // ============================================================
    // PDF
    // ============================================================

    private static byte[]
        BuildPdf(
            IReadOnlyList<string> lines)
    {
        const int rowsPerPage =
            42;

        var pages =
            lines
                .Chunk(
                    rowsPerPage)
                .ToArray();

        if (
            pages.Length ==
            0)
        {
            pages =
            [
                []
            ];
        }

        /*
         * Objetos PDF:
         *
         * 1 = Catalog
         * 2 = Pages
         * 3 = Font
         *
         * Por cada página:
         * Page + Content
         */

        var objectBodies =
            new Dictionary<int, string>();

        var pageObjectNumbers =
            new List<int>();

        var nextObjectNumber =
            4;

        foreach (
            var pageLines
            in pages)
        {
            var pageObjectNumber =
                nextObjectNumber++;

            var contentObjectNumber =
                nextObjectNumber++;

            pageObjectNumbers.Add(
                pageObjectNumber);

            var content =
                BuildPdfPageContent(
                    pageLines);

            var contentBytes =
                Encoding
                    .Latin1
                    .GetBytes(
                        content);

            objectBodies[
                contentObjectNumber] =
                $"<< /Length {contentBytes.Length} >>\n"
                +
                "stream\n"
                +
                content
                +
                "\nendstream";

            objectBodies[
                pageObjectNumber] =
                "<< /Type /Page "
                +
                "/Parent 2 0 R "
                +
                "/MediaBox [0 0 842 595] "
                +
                "/Resources << "
                +
                "/Font << /F1 3 0 R >> "
                +
                ">> "
                +
                $"/Contents {contentObjectNumber} 0 R "
                +
                ">>";
        }

        objectBodies[1] =
            "<< /Type /Catalog /Pages 2 0 R >>";

        objectBodies[2] =
            "<< /Type /Pages "
            +
            $"/Count {pageObjectNumbers.Count} "
            +
            "/Kids ["
            +
            string.Join(
                " ",
                pageObjectNumbers
                    .Select(
                        objectNumber =>
                            $"{objectNumber} 0 R"))
            +
            "] >>";

        objectBodies[3] =
            "<< /Type /Font "
            +
            "/Subtype /Type1 "
            +
            "/BaseFont /Helvetica "
            +
            "/Encoding /WinAnsiEncoding >>";

        using var stream =
            new MemoryStream();

        WritePdf(
            stream,
            "%PDF-1.4\n");

        var offsets =
            new Dictionary<int, long>();

        var highestObject =
            objectBodies
                .Keys
                .Max();

        for (
            var objectNumber = 1;
            objectNumber <= highestObject;
            objectNumber++)
        {
            offsets[
                objectNumber] =
                stream.Position;

            WritePdf(
                stream,
                $"{objectNumber} 0 obj\n");

            WritePdf(
                stream,
                objectBodies[
                    objectNumber]);

            WritePdf(
                stream,
                "\nendobj\n");
        }

        var xrefOffset =
            stream.Position;

        WritePdf(
            stream,
            $"xref\n0 {highestObject + 1}\n");

        WritePdf(
            stream,
            "0000000000 65535 f \n");

        for (
            var objectNumber = 1;
            objectNumber <= highestObject;
            objectNumber++)
        {
            WritePdf(
                stream,
                $"{offsets[objectNumber]:D10} 00000 n \n");
        }

        WritePdf(
            stream,
            "trailer\n"
            +
            $"<< /Size {highestObject + 1} "
            +
            "/Root 1 0 R >>\n"
            +
            "startxref\n"
            +
            $"{xrefOffset}\n"
            +
            "%%EOF");

        return stream
            .ToArray();
    }

    private static string
        BuildPdfPageContent(
            IReadOnlyList<string> lines)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "BT");

        builder.AppendLine(
            "/F1 7 Tf");

        builder.AppendLine(
            "28 560 Td");

        var first =
            true;

        foreach (
            var rawLine
            in lines)
        {
            var line =
                EscapePdfText(
                    Truncate(
                        rawLine,
                        145));

            if (!first)
            {
                builder.AppendLine(
                    "0 -12 Td");
            }

            builder.Append(
                '(');

            builder.Append(
                line);

            builder.AppendLine(
                ") Tj");

            first =
                false;
        }

        builder.Append(
            "ET");

        return builder
            .ToString();
    }

    private static void
        WritePdf(
            Stream stream,
            string value)
    {
        var bytes =
            Encoding
                .Latin1
                .GetBytes(
                    value);

        stream.Write(
            bytes,
            0,
            bytes.Length);
    }

    private static string
        EscapePdfText(
            string value)
    {
        return value
            .Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
            .Replace(
                "(",
                "\\(",
                StringComparison.Ordinal)
            .Replace(
                ")",
                "\\)",
                StringComparison.Ordinal);
    }

    private static string
        JoinPdfColumns(
            params string[] values)
    {
        return string.Join(
            " | ",
            values.Select(
                value =>
                    NormalizePdfValue(
                        value)));
    }

    private static string
        NormalizePdfValue(
            string value)
    {
        return value
            .Replace(
                "\r",
                " ",
                StringComparison.Ordinal)
            .Replace(
                "\n",
                " ",
                StringComparison.Ordinal)
            .Trim();
    }

    private static string
        Truncate(
            string value,
            int maximumLength)
    {
        if (
            value.Length <=
            maximumLength)
        {
            return value;
        }

        if (
            maximumLength <=
            3)
        {
            return value[
                ..maximumLength];
        }

        return value[
            ..(maximumLength - 3)]
            +
            "...";
    }

    // ============================================================
    // COMMON HELPERS
    // ============================================================

    private static string
        BuildOperatingSystem(
            Device device)
    {
        var name =
            device.OperatingSystem
            ??
            string.Empty;

        var version =
            device.OperatingSystemVersion
            ??
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                version))
        {
            return name;
        }

        if (
            string.IsNullOrWhiteSpace(
                name))
        {
            return version;
        }

        return
            $"{name} {version}";
    }

    private static string
        Csv(
            string? value)
    {
        var safe =
            value
            ??
            string.Empty;

        return
            $"\"{safe.Replace(
                "\"",
                "\"\"",
                StringComparison.Ordinal)}\"";
    }

    private static void
        ValidateOrganization(
            Guid organizationId)
    {
        if (
            organizationId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "La organización no es válida.");
        }
    }
}