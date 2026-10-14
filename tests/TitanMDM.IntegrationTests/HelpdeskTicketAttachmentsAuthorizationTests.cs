using System.Collections;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using TitanMDM.Api.Controllers;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;
using TitanMDM.Infrastructure.Security;

namespace TitanMDM.IntegrationTests;

/*
 * ============================================================
 * TITANMDM HELPDESK — AUTORIZACIÓN DE ADJUNTOS (HD-F11)
 * ============================================================
 *
 * Verifica el contrato de seguridad del controlador
 * consolidado HelpdeskTicketAttachmentsController:
 *
 *  1. El usuario autorizado puede listar, previsualizar y
 *     descargar los adjuntos de su ticket.
 *  2. El usuario sin acceso recibe 403 (existe, sin permiso)
 *     o 404 (no visible en su organización).
 *  3. Un usuario de otra organización no puede obtener
 *     adjuntos por identificador directo.
 *  4. Un usuario regional sin cobertura de la localidad no
 *     puede acceder.
 *  5. Los adjuntos inline (firmas / imágenes CID) respetan
 *     exactamente los mismos permisos.
 *  6. No se filtran rutas físicas, datos de otra organización
 *     ni información sensible.
 *  7. Las rutas antiguas compatibles no permiten eludir la
 *     autorización.
 */

public sealed class HelpdeskTicketAttachmentsAuthorizationTests
    : IDisposable
{
    private const string PortalAccess =
        "helpdesk.portal.access";

    private const string AgentAccess =
        "helpdesk.agent.access";

    private const string InternalNote =
        "helpdesk.ticket.internal-note";

    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "titan-hd-authz-" +
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

    // ============================================================
    // 1. USUARIO AUTORIZADO
    // ============================================================

    [Fact]
    public async Task OwnerWithPortalPermission_CanListPreviewAndDownload()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "contrato.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment);

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            attachment.StorageName,
            PdfBytes());

        var controller =
            CreateController(
                db,
                organizationId,
                ownerId,
                PortalAccess);

        Assert.IsType<OkObjectResult>(
            await controller.List(
                ticket.Id,
                CancellationToken.None));

        AssertFile(
            await controller.Preview(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        AssertFile(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task AgentWithOrganizationScope_CanListPreviewAndDownload()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var agentId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "informe.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment,
            new UserScopeGrant(
                organizationId,
                agentId,
                AuthorizationScopeType.Organization,
                organizationId));

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            attachment.StorageName,
            PdfBytes());

        var controller =
            CreateController(
                db,
                organizationId,
                agentId,
                AgentAccess);

        Assert.IsType<OkObjectResult>(
            await controller.List(
                ticket.Id,
                CancellationToken.None));

        AssertFile(
            await controller.Preview(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        AssertFile(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    // ============================================================
    // 2. USUARIO SIN ACCESO — 403 / 404 SEGÚN CONTRATO
    // ============================================================

    [Fact]
    public async Task UserWithoutAnyHelpdeskPermission_IsForbidden()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                Guid.NewGuid());

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ticket.RequesterUserId,
                "privado.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment);

        var intruderId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                intruderId);

        Assert.IsType<ForbidResult>(
            await controller.List(
                ticket.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.Preview(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.DownloadLegacy(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task PortalUserWhoIsNotRequester_IsForbidden()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                Guid.NewGuid());

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ticket.RequesterUserId,
                "ajeno.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment);

        var otherPortalUserId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                otherPortalUserId,
                PortalAccess);

        Assert.IsType<ForbidResult>(
            await controller.Preview(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task UnknownTicket_ReturnsNotFound()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var userId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                userId,
                AgentAccess);

        var ghostTicketId =
            Guid.NewGuid();

        var ghostAttachmentId =
            Guid.NewGuid();

        Assert.IsType<NotFoundObjectResult>(
            await controller.List(
                ghostTicketId,
                CancellationToken.None));

        Assert.IsType<NotFoundResult>(
            await controller.Preview(
                ghostTicketId,
                ghostAttachmentId,
                CancellationToken.None));

        Assert.IsType<NotFoundResult>(
            await controller.Download(
                ghostTicketId,
                ghostAttachmentId,
                CancellationToken.None));
    }

    // ============================================================
    // 3. AISLAMIENTO ENTRE ORGANIZACIONES
    // ============================================================

    [Fact]
    public async Task OtherOrganizationUser_ListIsNotFound()
    {
        await using var db = Open();

        var organizationA =
            Guid.NewGuid();

        var organizationB =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationA,
                Guid.NewGuid());

        var attachment =
            NewAttachment(
                organizationA,
                ticket.Id,
                ticket.RequesterUserId,
                "confidencial.pdf",
                "application/pdf");

        var agentB =
            Guid.NewGuid();

        await SeedAsync(
            db,
            organizationA,
            ticket,
            attachment,
            new UserScopeGrant(
                organizationB,
                agentB,
                AuthorizationScopeType.Organization,
                organizationB));

        var controller =
            CreateController(
                db,
                organizationB,
                agentB,
                AgentAccess);

        Assert.IsType<NotFoundObjectResult>(
            await controller.List(
                ticket.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task OtherOrganizationUser_DirectAttachmentId_IsNotFound()
    {
        await using var db = Open();

        var organizationA =
            Guid.NewGuid();

        var organizationB =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationA,
                Guid.NewGuid());

        var attachment =
            NewAttachment(
                organizationA,
                ticket.Id,
                ticket.RequesterUserId,
                "secreto.pdf",
                "application/pdf");

        var agentB =
            Guid.NewGuid();

        await SeedAsync(
            db,
            organizationA,
            ticket,
            attachment,
            new UserScopeGrant(
                organizationB,
                agentB,
                AuthorizationScopeType.Organization,
                organizationB));

        await WriteFileAsync(
            organizationA,
            ticket.Id,
            attachment.StorageName,
            PdfBytes());

        var controller =
            CreateController(
                db,
                organizationB,
                agentB,
                AgentAccess);

        Assert.IsType<NotFoundResult>(
            await controller.Preview(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        Assert.IsType<NotFoundResult>(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        Assert.IsType<NotFoundResult>(
            await controller.DownloadLegacy(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    // ============================================================
    // 4. COBERTURA REGIONAL (LOCALIDAD)
    // ============================================================

    [Fact]
    public async Task AgentWithoutAnyScopeGrant_IsForbidden()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                Guid.NewGuid());

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ticket.RequesterUserId,
                "sin-scope.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment);

        var agentId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                agentId,
                AgentAccess);

        Assert.IsType<ForbidResult>(
            await controller.Preview(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task RegionalAgentWithoutCoverage_IsForbidden()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var coveringSite =
            new Site(
                organizationId,
                "SITE-1",
                "Sede Uno");

        var otherSite =
            new Site(
                organizationId,
                "SITE-2",
                "Sede Dos");

        var ticket =
            NewTicket(
                organizationId,
                Guid.NewGuid());

        ticket.AssignSite(
            coveringSite.Id);

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ticket.RequesterUserId,
                "regional.pdf",
                "application/pdf");

        var regionalAgentId =
            Guid.NewGuid();

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment,
            new UserScopeGrant(
                organizationId,
                regionalAgentId,
                AuthorizationScopeType.Site,
                otherSite.Id),
            coveringSite,
            otherSite);

        var controller =
            CreateController(
                db,
                organizationId,
                regionalAgentId,
                AgentAccess);

        Assert.IsType<ForbidResult>(
            await controller.List(
                ticket.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.Preview(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task RegionalAgentWithCoverage_CanAccess()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var coveringSite =
            new Site(
                organizationId,
                "SITE-1",
                "Sede Uno");

        var ticket =
            NewTicket(
                organizationId,
                Guid.NewGuid());

        ticket.AssignSite(
            coveringSite.Id);

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ticket.RequesterUserId,
                "cubierto.pdf",
                "application/pdf");

        var regionalAgentId =
            Guid.NewGuid();

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment,
            new UserScopeGrant(
                organizationId,
                regionalAgentId,
                AuthorizationScopeType.Site,
                coveringSite.Id),
            coveringSite);

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            attachment.StorageName,
            PdfBytes());

        var controller =
            CreateController(
                db,
                organizationId,
                regionalAgentId,
                AgentAccess);

        Assert.IsType<OkObjectResult>(
            await controller.List(
                ticket.Id,
                CancellationToken.None));

        AssertFile(
            await controller.Download(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    // ============================================================
    // 5. ADJUNTOS INLINE / FIRMAS CID
    // ============================================================

    [Fact]
    public async Task InlineAttachment_OwnerCanPreviewAndDownload()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var inline =
            NewInlineAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "firma.png",
                "image/png",
                "image001.png@signature");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            inline);

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            inline.StorageName,
            PngBytes());

        var controller =
            CreateController(
                db,
                organizationId,
                ownerId,
                PortalAccess);

        AssertFile(
            await controller.Preview(
                ticket.Id,
                inline.Id,
                CancellationToken.None));

        AssertFile(
            await controller.Download(
                ticket.Id,
                inline.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task InlineAttachment_NonAuthorizedUserIsForbidden()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                Guid.NewGuid());

        var inline =
            NewInlineAttachment(
                organizationId,
                ticket.Id,
                ticket.RequesterUserId,
                "firma.png",
                "image/png",
                "image002.png@signature");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            inline);

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            inline.StorageName,
            PngBytes());

        var intruderId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                intruderId);

        Assert.IsType<ForbidResult>(
            await controller.Preview(
                ticket.Id,
                inline.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.Download(
                ticket.Id,
                inline.Id,
                CancellationToken.None));

        Assert.IsType<ForbidResult>(
            await controller.DownloadLegacy(
                ticket.Id,
                inline.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task InternalInlineAttachment_OwnerForbidden_AgentWithInternalPermissionAllowed()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var internalInline =
            new HelpdeskTicketAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "nota-interna.png",
                "internal-inline-0001.bin",
                "image/png",
                PngBytes().Length,
                isInternal:
                    true,
                isInline:
                    true,
                contentId:
                    "internal003.png@signature");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            internalInline);

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            internalInline.StorageName,
            PngBytes());

        var ownerController =
            CreateController(
                db,
                organizationId,
                ownerId,
                PortalAccess);

        Assert.IsType<ForbidResult>(
            await ownerController.Preview(
                ticket.Id,
                internalInline.Id,
                CancellationToken.None));

        var agentId =
            Guid.NewGuid();

        var agentController =
            CreateController(
                db,
                organizationId,
                agentId,
                AgentAccess,
                InternalNote,
                new UserScopeGrant(
                    organizationId,
                    agentId,
                    AuthorizationScopeType.Organization,
                    organizationId));

        AssertFile(
            await agentController.Download(
                ticket.Id,
                internalInline.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task List_HidesInternalAttachmentsFromOwner()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var visible =
            NewAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "publico.pdf",
                "application/pdf");

        var hidden =
            new HelpdeskTicketAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "privado.pdf",
                "internal-hidden-0001.bin",
                "application/pdf",
                PdfBytes().Length,
                isInternal:
                    true);

        await SeedAsync(
            db,
            organizationId,
            ticket,
            visible,
            hidden);

        var controller =
            CreateController(
                db,
                organizationId,
                ownerId,
                PortalAccess);

        var ok =
            Assert.IsType<OkObjectResult>(
                await controller.List(
                    ticket.Id,
                    CancellationToken.None));

        Assert.Equal(
            1,
            Count(ok.Value));

        Assert.DoesNotContain(
            "internal-hidden-0001.bin",
            JsonSerializer.Serialize(
                ok.Value));
    }

    // ============================================================
    // 6. NO FILTRACIÓN DE DATOS
    // ============================================================

    [Fact]
    public async Task List_DoesNotLeakStorageNameOrPhysicalPath()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "factura.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment);

        var agentId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                agentId,
                AgentAccess,
                new UserScopeGrant(
                    organizationId,
                    agentId,
                    AuthorizationScopeType.Organization,
                    organizationId));

        var ok =
            Assert.IsType<OkObjectResult>(
                await controller.List(
                    ticket.Id,
                    CancellationToken.None));

        var json =
            JsonSerializer.Serialize(
                ok.Value);

        Assert.DoesNotContain(
            attachment.StorageName,
            json);

        Assert.DoesNotContain(
            _root,
            json);

        Assert.DoesNotContain(
            "App_Data",
            json);
    }

    [Fact]
    public async Task List_OnlyReturnsAttachmentsOfRequestedOrganization()
    {
        await using var db = Open();

        var organizationA =
            Guid.NewGuid();

        var organizationB =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationA,
                ownerId);

        var ownAttachment =
            NewAttachment(
                organizationA,
                ticket.Id,
                ownerId,
                "propio.pdf",
                "application/pdf");

        /*
         * Fila con el mismo TicketId pero de otra organización.
         * El filtro por OrganizationId debe excluirla.
         */
        var foreignAttachment =
            NewAttachment(
                organizationB,
                ticket.Id,
                ownerId,
                "ajeno.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationA,
            ticket,
            ownAttachment,
            foreignAttachment);

        var controller =
            CreateController(
                db,
                organizationA,
                ownerId,
                PortalAccess);

        var ok =
            Assert.IsType<OkObjectResult>(
                await controller.List(
                    ticket.Id,
                    CancellationToken.None));

        Assert.Equal(
            1,
            Count(ok.Value));

        Assert.DoesNotContain(
            "ajeno.pdf",
            JsonSerializer.Serialize(
                ok.Value));
    }

    [Fact]
    public async Task RejectsStorageNamePathTraversal()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var traversing =
            NewAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "escape.pdf",
                "application/pdf",
                storageName:
                    string.Join(
                        Path.DirectorySeparatorChar,
                        "..",
                        "..",
                        "escape.bin"));

        await SeedAsync(
            db,
            organizationId,
            ticket,
            traversing);

        var agentId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                agentId,
                AgentAccess,
                new UserScopeGrant(
                    organizationId,
                    agentId,
                    AuthorizationScopeType.Organization,
                    organizationId));

        Assert.IsType<BadRequestObjectResult>(
            await controller.Download(
                ticket.Id,
                traversing.Id,
                CancellationToken.None));
    }

    // ============================================================
    // 7. RUTAS ANTIGUAS COMPATIBLES
    // ============================================================

    [Fact]
    public async Task LegacyRoute_NonAuthorizedUserIsForbidden()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                Guid.NewGuid());

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ticket.RequesterUserId,
                "legacy.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment);

        var intruderId =
            Guid.NewGuid();

        var controller =
            CreateController(
                db,
                organizationId,
                intruderId);

        Assert.IsType<ForbidResult>(
            await controller.DownloadLegacy(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task LegacyRoute_OtherOrganizationIsNotFound()
    {
        await using var db = Open();

        var organizationA =
            Guid.NewGuid();

        var organizationB =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationA,
                Guid.NewGuid());

        var attachment =
            NewAttachment(
                organizationA,
                ticket.Id,
                ticket.RequesterUserId,
                "legacy-secreto.pdf",
                "application/pdf");

        var agentB =
            Guid.NewGuid();

        await SeedAsync(
            db,
            organizationA,
            ticket,
            attachment,
            new UserScopeGrant(
                organizationB,
                agentB,
                AuthorizationScopeType.Organization,
                organizationB));

        var controller =
            CreateController(
                db,
                organizationB,
                agentB,
                AgentAccess);

        Assert.IsType<NotFoundResult>(
            await controller.DownloadLegacy(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task LegacyRoute_AuthorizedOwnerServesFile()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var attachment =
            NewAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "legacy-ok.pdf",
                "application/pdf");

        await SeedAsync(
            db,
            organizationId,
            ticket,
            attachment);

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            attachment.StorageName,
            PdfBytes());

        var controller =
            CreateController(
                db,
                organizationId,
                ownerId,
                PortalAccess);

        AssertFile(
            await controller.DownloadLegacy(
                ticket.Id,
                attachment.Id,
                CancellationToken.None));
    }

    [Fact]
    public async Task LegacyRoute_InternalAttachment_RequiresInternalPermission()
    {
        await using var db = Open();

        var organizationId =
            Guid.NewGuid();

        var ownerId =
            Guid.NewGuid();

        var ticket =
            NewTicket(
                organizationId,
                ownerId);

        var internalAttachment =
            new HelpdeskTicketAttachment(
                organizationId,
                ticket.Id,
                ownerId,
                "legacy-interno.pdf",
                "legacy-internal-0001.bin",
                "application/pdf",
                PdfBytes().Length,
                isInternal:
                    true);

        await SeedAsync(
            db,
            organizationId,
            ticket,
            internalAttachment);

        await WriteFileAsync(
            organizationId,
            ticket.Id,
            internalAttachment.StorageName,
            PdfBytes());

        var ownerController =
            CreateController(
                db,
                organizationId,
                ownerId,
                PortalAccess);

        Assert.IsType<ForbidResult>(
            await ownerController.DownloadLegacy(
                ticket.Id,
                internalAttachment.Id,
                CancellationToken.None));

        var agentId =
            Guid.NewGuid();

        var agentController =
            CreateController(
                db,
                organizationId,
                agentId,
                AgentAccess,
                InternalNote,
                new UserScopeGrant(
                    organizationId,
                    agentId,
                    AuthorizationScopeType.Organization,
                    organizationId));

        AssertFile(
            await agentController.DownloadLegacy(
                ticket.Id,
                internalAttachment.Id,
                CancellationToken.None));
    }

    // ============================================================
    // INFRAESTRUCTURA
    // ============================================================

    private static HelpdeskTicket NewTicket(
        Guid organizationId,
        Guid requesterUserId)
    {
        return new HelpdeskTicket(
            organizationId,
            "HD-" +
            Guid.NewGuid()
                .ToString("N")[..16],
            "Solicitud de prueba",
            "Descripción de prueba",
            "incident",
            "medium",
            "general",
            "console",
            requesterUserId,
            null,
            null);
    }

    private static HelpdeskTicketAttachment NewAttachment(
        Guid organizationId,
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        string? storageName = null)
    {
        return new HelpdeskTicketAttachment(
            organizationId,
            ticketId,
            uploadedByUserId,
            fileName,
            storageName ??
                Guid.NewGuid()
                    .ToString("N") +
                ".bin",
            contentType,
            PdfBytes().Length,
            isInternal:
                false);
    }

    private static HelpdeskTicketAttachment NewInlineAttachment(
        Guid organizationId,
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        string contentId)
    {
        return new HelpdeskTicketAttachment(
            organizationId,
            ticketId,
            uploadedByUserId,
            fileName,
            Guid.NewGuid()
                .ToString("N") +
            ".bin",
            contentType,
            PngBytes().Length,
            isInternal:
                false,
            isInline:
                true,
            contentId:
                contentId);
    }

    private static async Task SeedAsync(
        TitanMdmDbContext db,
        Guid organizationId,
        HelpdeskTicket ticket,
        params object[] attachmentsAndGrants)
    {
        db.HelpdeskTickets.Add(
            ticket);

        foreach (var item in attachmentsAndGrants)
        {
            switch (item)
            {
                case HelpdeskTicketAttachment attachment:
                    db.Add(
                        attachment);
                    break;

                case UserScopeGrant grant:
                    db.Add(
                        grant);
                    break;

                case Site site:
                    db.Add(
                        site);
                    break;
            }
        }

        await db.SaveChangesAsync();
    }

    private TitanMdmDbContext Open()
    {
        return new TitanMdmDbContext(
            new DbContextOptionsBuilder<
                    TitanMdmDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid()
                        .ToString("N"))
                .Options);
    }

    private HelpdeskTicketAttachmentsController
        CreateController(
            TitanMdmDbContext db,
            Guid organizationId,
            Guid userId,
            params object[] permissionsOrGrants)
    {
        var claims =
            new List<Claim>
            {
                new(
                    "organization_id",
                    organizationId.ToString()),
                new(
                    ClaimTypes.NameIdentifier,
                    userId.ToString())
            };

        var grants =
            new List<UserScopeGrant>();

        foreach (var item in permissionsOrGrants)
        {
            switch (item)
            {
                case string permission:
                    claims.Add(
                        new Claim(
                            "permission",
                            permission));
                    break;

                case UserScopeGrant grant:
                    grants.Add(
                        grant);
                    break;
            }
        }

        if (grants.Count > 0)
        {
            db.AddRange(
                grants);

            db.SaveChanges();
        }

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

        return new HelpdeskTicketAttachmentsController(
            db,
            new ScopeAccessService(
                db),
            configuration,
            environment)
        {
            ControllerContext =
                new ControllerContext
                {
                    HttpContext =
                        new DefaultHttpContext
                        {
                            User =
                                new ClaimsPrincipal(
                                    new ClaimsIdentity(
                                        claims,
                                        "test"))
                        }
                }
        };
    }

    private async Task WriteFileAsync(
        Guid organizationId,
        Guid ticketId,
        string storageName,
        byte[] bytes)
    {
        var directory =
            Path.Combine(
                _root,
                organizationId
                    .ToString("N"),
                ticketId
                    .ToString("N"));

        Directory.CreateDirectory(
            directory);

        await File.WriteAllBytesAsync(
            Path.Combine(
                directory,
                storageName),
            bytes);
    }

    /*
     * Confirma que la acción sirvió un archivo y libera el
     * stream para no dejar archivos bloqueados en Windows.
     */
    private static void AssertFile(
        IActionResult result)
    {
        var file =
            Assert.IsType<FileStreamResult>(
                result);

        file.FileStream.Dispose();
    }

    private static int Count(
        object? value)
    {
        return value is IEnumerable enumerable
            ? enumerable
                .Cast<object>()
                .Count()
            : 0;
    }

    private static byte[] PngBytes()
    {
        return new byte[]
        {
            137, 80, 78, 71, 13, 10, 26, 10,
            0, 0, 0, 13, 73, 72, 68, 82
        };
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
