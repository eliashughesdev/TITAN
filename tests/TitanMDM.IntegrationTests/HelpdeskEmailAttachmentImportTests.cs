using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Helpdesk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

/*
 * ============================================================
 * TITANMDM HELPDESK — IMPORTACIÓN DE ADJUNTOS DE CORREO
 * ============================================================
 *
 * Cubre:
 *  - imágenes inline de firma (PNG / GIF) y su asociación con
 *    el mensaje de correo,
 *  - rechazo de archivos con firma inválida o formato no
 *    permitido, con trazabilidad,
 *  - idempotencia ante reintentos del worker.
 */

public sealed class HelpdeskEmailAttachmentImportTests
    : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "titan-hd-att-" +
            Guid.NewGuid()
                .ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(
                    _root,
                    recursive:
                        true);
            }
        }
        catch
        {
            // Limpieza best-effort en pruebas.
        }
    }

    [Fact]
    public async Task ImportsInlinePngAndAssociatesWithMessage()
    {
        await using var db = Open();
        var scenario = await SeedAsync(
            db,
            "<msg-png@test>");

        var service =
            CreateService(db);

        var imported =
            await service.ImportAsync(
                scenario.OrganizationId,
                scenario.TicketId,
                scenario.UserId,
                scenario.InternetMessageId,
                new[]
                {
                    new IncomingHelpdeskAttachment(
                        "att-1",
                        "logo.png",
                        "image/png",
                        PngBytes(),
                        IsInline:
                            true,
                        ContentId:
                            "logo@signature")
                });

        Assert.Equal(
            1,
            imported);

        var stored =
            await db.Set<HelpdeskTicketAttachment>()
                .SingleAsync();

        Assert.Equal(
            "image/png",
            stored.ContentType);

        Assert.True(
            stored.IsInline);

        Assert.Equal(
            "logo@signature",
            stored.ContentId);

        Assert.Equal(
            scenario.EmailMessageId,
            stored.EmailMessageId);
    }

    [Fact]
    public async Task AcceptsGifSignatureLogo()
    {
        await using var db = Open();
        var scenario = await SeedAsync(
            db,
            "<msg-gif@test>");

        var service =
            CreateService(db);

        var imported =
            await service.ImportAsync(
                scenario.OrganizationId,
                scenario.TicketId,
                scenario.UserId,
                scenario.InternetMessageId,
                new[]
                {
                    new IncomingHelpdeskAttachment(
                        "att-gif",
                        "firma.gif",
                        "image/gif",
                        GifBytes(),
                        IsInline:
                            true,
                        ContentId:
                            "firma@x")
                });

        Assert.Equal(
            1,
            imported);

        var stored =
            await db.Set<HelpdeskTicketAttachment>()
                .SingleAsync();

        Assert.Equal(
            "image/gif",
            stored.ContentType);
    }

    [Fact]
    public async Task RejectsInvalidSignatureAndRecordsEvent()
    {
        await using var db = Open();
        var scenario = await SeedAsync(
            db,
            "<msg-bad@test>");

        var service =
            CreateService(db);

        var imported =
            await service.ImportAsync(
                scenario.OrganizationId,
                scenario.TicketId,
                scenario.UserId,
                scenario.InternetMessageId,
                new[]
                {
                    new IncomingHelpdeskAttachment(
                        "att-bad",
                        "malicioso.png",
                        "image/png",
                        new byte[]
                        {
                            0x01, 0x02, 0x03, 0x04
                        })
                });

        Assert.Equal(
            0,
            imported);

        Assert.False(
            await db.Set<HelpdeskTicketAttachment>()
                .AnyAsync());

        Assert.True(
            await db.HelpdeskTicketEvents
                .AnyAsync(
                    x =>
                        x.TicketId ==
                            scenario.TicketId
                        &&
                        x.EventType ==
                            "email_attachments_rejected"));
    }

    [Fact]
    public async Task RejectsUnsupportedExtension()
    {
        await using var db = Open();
        var scenario = await SeedAsync(
            db,
            "<msg-exe@test>");

        var service =
            CreateService(db);

        var imported =
            await service.ImportAsync(
                scenario.OrganizationId,
                scenario.TicketId,
                scenario.UserId,
                scenario.InternetMessageId,
                new[]
                {
                    new IncomingHelpdeskAttachment(
                        "att-exe",
                        "payload.exe",
                        "application/octet-stream",
                        PngBytes())
                });

        Assert.Equal(
            0,
            imported);
    }

    [Fact]
    public async Task RetryDoesNotCreateDuplicates()
    {
        await using var db = Open();
        var scenario = await SeedAsync(
            db,
            "<msg-retry@test>");

        var service =
            CreateService(db);

        var attachment =
            new IncomingHelpdeskAttachment(
                "att-retry",
                "documento.pdf",
                "application/pdf",
                PdfBytes());

        var first =
            await service.ImportAsync(
                scenario.OrganizationId,
                scenario.TicketId,
                scenario.UserId,
                scenario.InternetMessageId,
                new[]
                {
                    attachment
                });

        var second =
            await service.ImportAsync(
                scenario.OrganizationId,
                scenario.TicketId,
                scenario.UserId,
                scenario.InternetMessageId,
                new[]
                {
                    attachment
                });

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        Assert.Equal(
            1,
            await db.Set<HelpdeskTicketAttachment>()
                .CountAsync());
    }

    private HelpdeskEmailAttachmentImportService
        CreateService(
            TitanMdmDbContext db)
    {
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<
                        string,
                        string?>
                    {
                        ["HelpdeskAttachments:StoragePath"] =
                            _root
                    })
                .Build();

        var environment =
            new FakeHostEnvironment
            {
                ContentRootPath =
                    _root
            };

        return new HelpdeskEmailAttachmentImportService(
            db,
            configuration,
            environment,
            NullLogger<
                HelpdeskEmailAttachmentImportService>
                .Instance);
    }

    private static async Task<Scenario> SeedAsync(
        TitanMdmDbContext db,
        string internetMessageId)
    {
        var organizationId =
            Guid.NewGuid();

        var user =
            new User(
                organizationId,
                "Mail",
                "Bot",
                "mailbot@example.test");

        var ticket =
            new HelpdeskTicket(
                organizationId,
                (
                    "HD-" +
                    Guid.NewGuid()
                        .ToString("N")
                )[..20],
                "Solicitud",
                "Descripción",
                "incident",
                "medium",
                "general",
                "email",
                user.Id,
                null,
                null);

        db.AddRange(
            user,
            ticket);

        await db.SaveChangesAsync();

        var message =
            new HelpdeskEmailMessage(
                organizationId,
                ticket.Id,
                "helpdesk@example.test",
                internetMessageId,
                null);

        db.Add(
            message);

        await db.SaveChangesAsync();

        return new Scenario(
            organizationId,
            ticket.Id,
            user.Id,
            internetMessageId,
            message.Id);
    }

    private static TitanMdmDbContext Open()
    {
        var options =
            new DbContextOptionsBuilder<
                    TitanMdmDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid()
                        .ToString("N"))
                .Options;

        return new TitanMdmDbContext(
            options);
    }

    private static byte[] PngBytes()
    {
        return new byte[]
        {
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82
        };
    }

    private static byte[] GifBytes()
    {
        var header =
            "GIF89a"u8.ToArray();

        var body =
            new byte[]
            {
                10, 0, 10, 0
            };

        var result =
            new byte[
                header.Length +
                body.Length];

        header.CopyTo(
            result,
            0);

        body.CopyTo(
            result,
            header.Length);

        return result;
    }

    private static byte[] PdfBytes()
    {
        var header =
            "%PDF-"u8.ToArray();

        var body =
            new byte[]
            {
                49, 46, 55, 10
            };

        var result =
            new byte[
                header.Length +
                body.Length];

        header.CopyTo(
            result,
            0);

        body.CopyTo(
            result,
            header.Length);

        return result;
    }

    private sealed record Scenario(
        Guid OrganizationId,
        Guid TicketId,
        Guid UserId,
        string InternetMessageId,
        Guid EmailMessageId);

    private sealed class FakeHostEnvironment
        : IHostEnvironment
    {
        public string EnvironmentName
        {
            get;
            set;
        } = "Test";

        public string ApplicationName
        {
            get;
            set;
        } = "TitanMDM.Tests";

        public string ContentRootPath
        {
            get;
            set;
        } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider
        {
            get;
            set;
        } = new NullFileProvider();
    }
}
