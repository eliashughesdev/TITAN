using System.Security.Claims;

using Microsoft.AspNetCore.Mvc.Filters;

using TitanMDM.Application.Audit;

namespace TitanMDM.Api.Security;

public sealed class RbacAuditFilter
    : IAsyncActionFilter
{
    private readonly IAdministrativeAuditWriter
        _auditWriter;

    private readonly ILogger<RbacAuditFilter>
        _logger;

    public RbacAuditFilter(
        IAdministrativeAuditWriter auditWriter,
        ILogger<RbacAuditFilter> logger)
    {
        _auditWriter =
            auditWriter;

        _logger =
            logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        /*
         * Las operaciones GET no modifican RBAC.
         * Por tanto no generan eventos administrativos de cambio.
         */
        if (
            HttpMethods.IsGet(
                context.HttpContext
                    .Request
                    .Method))
        {
            await next();
            return;
        }

        var organizationId =
            ReadGuidClaim(
                context,
                "organization_id");

        var actorUserId =
            ReadGuidClaim(
                context,
                ClaimTypes.NameIdentifier)
            ??
            ReadGuidClaim(
                context,
                "sub");

        var controller =
            GetRouteValue(
                context,
                "controller")
            ??
            "Unknown";

        var action =
            GetRouteValue(
                context,
                "action")
            ??
            "Unknown";

        var targetId =
            ResolveTargetId(
                context);

        var correlationId =
            context.HttpContext
                .TraceIdentifier;

        var ipAddress =
            context.HttpContext
                .Connection
                .RemoteIpAddress
                ?.ToString();

        try
        {
            var executed =
                await next();

            var result =
                ResolveResult(
                    executed);

            await WriteAuditAsync(
                organizationId,
                actorUserId,
                $"RBAC.{controller}.{action}",
                controller,
                targetId,
                result,
                correlationId,
                ipAddress,
                context.HttpContext
                    .RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "RBAC operation failed. Controller={Controller}, Action={Action}, TargetId={TargetId}",
                controller,
                action,
                targetId);

            await WriteAuditAsync(
                organizationId,
                actorUserId,
                $"RBAC.{controller}.{action}",
                controller,
                targetId,
                "Failure",
                correlationId,
                ipAddress,
                context.HttpContext
                    .RequestAborted);

            throw;
        }
    }

    // ============================================================
    // AUDIT WRITE
    // ============================================================

    private async Task WriteAuditAsync(
        Guid? organizationId,
        Guid? actorUserId,
        string action,
        string targetType,
        string? targetId,
        string result,
        string correlationId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (!organizationId.HasValue)
        {
            /*
             * Si no existe organization_id no escribimos
             * un evento potencialmente ambiguo.
             */
            return;
        }

        try
        {
            await _auditWriter
                .WriteAsync(
                    new AdministrativeAuditEntry(
                        organizationId.Value,
                        actorUserId,
                        action,
                        targetType,
                        targetId,
                        result,
                        correlationId,
                        ipAddress),
                    cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            /*
             * La auditoría no debe ocultar el resultado real
             * de la operación administrativa.
             *
             * Registramos el fallo para observabilidad.
             */
            _logger.LogError(
                ex,
                "No fue posible persistir el evento RBAC. Action={Action}, Target={TargetType}, TargetId={TargetId}",
                action,
                targetType,
                targetId);
        }
    }

    // ============================================================
    // RESULT
    // ============================================================

    private static string ResolveResult(
        ActionExecutedContext executed)
    {
        if (executed.Exception is not null)
        {
            return "Failure";
        }

        var statusCode =
            executed.HttpContext
                .Response
                .StatusCode;

        if (
            statusCode >= 200
            &&
            statusCode <= 399)
        {
            return "Success";
        }

        if (
            statusCode ==
                StatusCodes.Status401Unauthorized
            ||
            statusCode ==
                StatusCodes.Status403Forbidden)
        {
            return "Denied";
        }

        return "Failure";
    }

    // ============================================================
    // CLAIMS
    // ============================================================

    private static Guid? ReadGuidClaim(
        ActionExecutingContext context,
        string claimType)
    {
        var value =
            context.HttpContext
                .User
                .FindFirstValue(
                    claimType);

        return Guid.TryParse(
            value,
            out var result)
            ? result
            : null;
    }

    // ============================================================
    // ROUTE VALUES
    // ============================================================

    private static string? GetRouteValue(
        ActionExecutingContext context,
        string key)
    {
        if (
            context.ActionDescriptor
                .RouteValues
                .TryGetValue(
                    key,
                    out var value)
            &&
            !string.IsNullOrWhiteSpace(
                value))
        {
            return value;
        }

        return null;
    }

    private static string? ResolveTargetId(
        ActionExecutingContext context)
    {
        /*
         * Primero buscamos parámetros comunes de acciones
         * administrativas.
         */
        foreach (
            var key
            in new[]
            {
                "userId",
                "roleId",
                "grantId",
                "directoryUserId",
                "organizationId",
                "departmentId",
                "groupId",
                "siteId"
            })
        {
            if (
                context.RouteData
                    .Values
                    .TryGetValue(
                        key,
                        out var value)
                &&
                value is not null)
            {
                return value.ToString();
            }
        }

        /*
         * Como fallback revisamos ActionArguments.
         */
        foreach (
            var key
            in new[]
            {
                "userId",
                "roleId",
                "grantId",
                "directoryUserId",
                "organizationId",
                "departmentId",
                "groupId",
                "siteId"
            })
        {
            if (
                context.ActionArguments
                    .TryGetValue(
                        key,
                        out var value)
                &&
                value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }
}