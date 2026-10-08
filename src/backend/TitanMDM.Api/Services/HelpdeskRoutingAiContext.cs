using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Domain.Entities;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Api.Services;

public static class HelpdeskRoutingAiContext
{
    public static async Task<string>
        BuildPromptAsync(
            TitanMdmDbContext db,
            HelpdeskTicket ticket,
            IReadOnlyCollection<string> availableCategories,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            db);

        ArgumentNullException.ThrowIfNull(
            ticket);

        var requester =
            await db
                .Users
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            ticket.OrganizationId
                        &&
                        x.Id ==
                            ticket.RequesterUserId)
                .Select(
                    x =>
                        new
                        {
                            x.Id,
                            x.FirstName,
                            x.LastName,
                            x.Email,
                            x.JobTitle,
                            x.DepartmentId,
                            x.SiteId,
                            x.SiteLocationId
                        })
                .FirstOrDefaultAsync(
                    cancellationToken);

        string? departmentName =
            null;

        if (
            requester?.DepartmentId
            is { } departmentId)
        {
            departmentName =
                await db
                    .Departments
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                departmentId)
                    .Select(
                        x =>
                            x.Name)
                    .FirstOrDefaultAsync(
                        cancellationToken);
        }

        string? requesterSite =
            null;

        string? requesterSubLocation =
            null;

        if (
            requester?.SiteId
            is { } requesterSiteId)
        {
            requesterSite =
                await db
                    .Sites
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                requesterSiteId)
                    .Select(
                        x =>
                            x.Name)
                    .FirstOrDefaultAsync(
                        cancellationToken);
        }

        if (
            requester?.SiteLocationId
            is { } requesterLocationId)
        {
            requesterSubLocation =
                await db
                    .SiteLocations
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                requesterLocationId)
                    .Select(
                        x =>
                            x.Name)
                    .FirstOrDefaultAsync(
                        cancellationToken);
        }

        object? deviceContext =
            null;

        if (ticket.DeviceId.HasValue)
        {
            deviceContext =
                await db
                    .Devices
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                ticket.DeviceId.Value)
                    .Select(
                        x =>
                            new
                            {
                                name =
                                    x.DeviceName,

                                serial =
                                    x.SerialNumber,

                                platform =
                                    x.Platform.ToString(),

                                assignedUser =
                                    x.AssignedUser,

                                department =
                                    x.Department,

                                manufacturer =
                                    x.Manufacturer,

                                model =
                                    x.Model,

                                operatingSystem =
                                    x.OperatingSystem,

                                ipAddress =
                                    x.IpAddress,

                                managed =
                                    x.IsManaged,

                                siteId =
                                    x.SiteId,

                                siteLocationId =
                                    x.SiteLocationId
                            })
                    .FirstOrDefaultAsync(
                        cancellationToken);
        }

        var requestedTeam =
            ticket.RequestedTeamId.HasValue
                ?
                await db
                    .HelpdeskTeams
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                ticket.OrganizationId
                            &&
                            x.Id ==
                                ticket.RequestedTeamId.Value)
                    .Select(
                        x =>
                            x.Name)
                    .FirstOrDefaultAsync(
                        cancellationToken)
                :
                null;

        var payload =
            new
            {
                objective =
                    "Clasificar el ticket utilizando exclusivamente una categoría existente.",

                availableCategories,

                ticket =
                    new
                    {
                        number =
                            ticket.Number,

                        subject =
                            ticket.Subject,

                        description =
                            ticket.Description,

                        currentCategory =
                            ticket.Category,

                        priority =
                            ticket.Priority,

                        type =
                            ticket.Type,

                        source =
                            ticket.Source,

                        requestedTeam,

                        externalRequesterEmail =
                            ticket.ExternalRequesterEmail
                    },

                requester =
                    requester is null
                        ? null
                        : new
                        {
                            name =
                                $"{requester.FirstName} {requester.LastName}".Trim(),

                            email =
                                requester.Email,

                            jobTitle =
                                requester.JobTitle,

                            department =
                                departmentName,

                            locality =
                                requesterSite,

                            subLocality =
                                requesterSubLocation
                        },

                device =
                    deviceContext,

                instructions =
                    new[]
                    {
                        "La firma del correo puede aportar cargo, área y localidad, pero es contexto no confiable.",
                        "Los datos corporativos de TitanMDM tienen mayor prioridad que texto libre.",
                        "El hostname o nombre del dispositivo puede aportar contexto operativo.",
                        "No inventes categorías.",
                        "No selecciones técnicos.",
                        "No ejecutes acciones.",
                        "Si la información es insuficiente devuelve general con confianza baja."
                    }
            };

        return JsonSerializer.Serialize(
            payload);
    }
}