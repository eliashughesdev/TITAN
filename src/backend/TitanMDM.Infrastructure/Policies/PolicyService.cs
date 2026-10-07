using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Policies;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Policies;

public sealed class PolicyService : IPolicyService
{
    private readonly TitanMdmDbContext _dbContext;
    private readonly IDeviceCommandService _commandService;

    public PolicyService(
        TitanMdmDbContext dbContext,
        IDeviceCommandService commandService)
    {
        _dbContext = dbContext;
        _commandService = commandService;
    }

    public async Task<IReadOnlyCollection<PolicyDto>> GetAllAsync(
        Guid organizationId,
        string? platform,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query =
            _dbContext.Policies
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(platform))
        {
            if (!Enum.TryParse<PolicyPlatform>(
                    platform,
                    true,
                    out var parsedPlatform))
            {
                throw new PolicyException(
                    "INVALID_PLATFORM",
                    "La plataforma especificada no es válida.");
            }

            query = query.Where(
                x => x.Platform == parsedPlatform);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PolicyStatus>(
                    status,
                    true,
                    out var parsedStatus))
            {
                throw new PolicyException(
                    "INVALID_STATUS",
                    "El estado especificado no es válido.");
            }

            query = query.Where(
                x => x.Status == parsedStatus);
        }

        var policies =
            await query
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ToListAsync(cancellationToken);

        var policyIds =
            policies.Select(x => x.Id).ToArray();

        var assignmentCounts =
            await _dbContext.DevicePolicyAssignments
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    policyIds.Contains(x.PolicyId) &&
                    x.Status != PolicyAssignmentStatus.Removed)
                .GroupBy(x => x.PolicyId)
                .Select(x => new
                {
                    PolicyId = x.Key,
                    Count = x.Count()
                })
                .ToDictionaryAsync(
                    x => x.PolicyId,
                    x => x.Count,
                    cancellationToken);

        return policies
            .Select(x =>
                Map(
                    x,
                    assignmentCounts.GetValueOrDefault(x.Id)))
            .ToArray();
    }

    public async Task<PolicyDetailsDto?> GetByIdAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default)
    {
        var policy =
            await _dbContext.Policies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == policyId &&
                        x.OrganizationId == organizationId,
                    cancellationToken);

        if (policy is null)
            return null;

        var version =
            await _dbContext.PolicyVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.PolicyId == policy.Id &&
                        x.VersionNumber ==
                            policy.CurrentVersion,
                    cancellationToken);

        var assignments =
            await _dbContext.DevicePolicyAssignments
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.PolicyId == policy.Id &&
                    x.Status !=
                        PolicyAssignmentStatus.Removed)
                .ToListAsync(cancellationToken);

        return new PolicyDetailsDto(
            policy.Id,
            policy.OrganizationId,
            policy.Name,
            policy.Description,
            policy.Platform.ToString(),
            policy.Status.ToString(),
            policy.CurrentVersion,
            version?.ConfigurationJson ?? "{}",
            assignments.Count,
            assignments.Count(x =>
                x.Status ==
                PolicyAssignmentStatus.Applied),
            assignments.Count(x =>
                x.Status ==
                PolicyAssignmentStatus.Failed),
            policy.CreatedByUserId,
            policy.CreatedAtUtc,
            policy.UpdatedAtUtc,
            policy.ActivatedAtUtc,
            policy.ArchivedAtUtc);
    }

    public async Task<PolicyDetailsDto> CreateAsync(
        Guid organizationId,
        Guid userId,
        CreatePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(
            organizationId,
            userId);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new PolicyException(
                "INVALID_NAME",
                "El nombre de la política es obligatorio.");
        }

        if (!Enum.TryParse<PolicyPlatform>(
                request.Platform,
                true,
                out var platform))
        {
            throw new PolicyException(
                "INVALID_PLATFORM",
                "La plataforma debe ser Windows o Android.");
        }

        ValidateJson(request.ConfigurationJson);

        var duplicate =
            await _dbContext.Policies
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Name == request.Name.Trim(),
                    cancellationToken);

        if (duplicate)
        {
            throw new PolicyException(
                "POLICY_NAME_EXISTS",
                "Ya existe una política con ese nombre.");
        }

        var policy =
            new Policy(
                organizationId,
                request.Name,
                request.Description,
                platform,
                userId);

        var version =
            new PolicyVersion(
                organizationId,
                policy.Id,
                1,
                NormalizeJson(request.ConfigurationJson),
                userId);

        _dbContext.Policies.Add(policy);
        _dbContext.PolicyVersions.Add(version);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return (await GetByIdAsync(
            organizationId,
            policy.Id,
            cancellationToken))!;
    }

    public async Task<PolicyDetailsDto> UpdateAsync(
        Guid organizationId,
        Guid userId,
        Guid policyId,
        UpdatePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(
            organizationId,
            userId);

        ValidateJson(request.ConfigurationJson);

        var policy =
            await GetPolicyEntityAsync(
                organizationId,
                policyId,
                cancellationToken);

        if (policy.Status == PolicyStatus.Archived)
        {
            throw new PolicyException(
                "POLICY_ARCHIVED",
                "Una política archivada no puede modificarse.");
        }

        var duplicate =
            await _dbContext.Policies
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OrganizationId == organizationId &&
                        x.Name == request.Name.Trim() &&
                        x.Id != policyId,
                    cancellationToken);

        if (duplicate)
        {
            throw new PolicyException(
                "POLICY_NAME_EXISTS",
                "Ya existe una política con ese nombre.");
        }

        policy.Update(
            request.Name,
            request.Description);

        var nextVersion =
            policy.CurrentVersion + 1;

        var version =
            new PolicyVersion(
                organizationId,
                policy.Id,
                nextVersion,
                NormalizeJson(request.ConfigurationJson),
                userId);

        policy.SetCurrentVersion(nextVersion);

        _dbContext.PolicyVersions.Add(version);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return (await GetByIdAsync(
            organizationId,
            policy.Id,
            cancellationToken))!;
    }

    public async Task ActivateAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default)
    {
        var policy =
            await GetPolicyEntityAsync(
                organizationId,
                policyId,
                cancellationToken);

        policy.Activate();

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DisableAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default)
    {
        var policy =
            await GetPolicyEntityAsync(
                organizationId,
                policyId,
                cancellationToken);

        policy.Disable();

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task ArchiveAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken = default)
    {
        var policy =
            await GetPolicyEntityAsync(
                organizationId,
                policyId,
                cancellationToken);

        policy.Archive();

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<PolicyAssignmentDto>>
        AssignAsync(
            Guid organizationId,
            Guid userId,
            Guid policyId,
            AssignPolicyRequest request,
            CancellationToken cancellationToken = default)
    {
        if (request.DeviceIds is null ||
            request.DeviceIds.Count == 0)
        {
            throw new PolicyException(
                "NO_DEVICES",
                "Debe seleccionar al menos un dispositivo.");
        }

        var policy =
            await GetPolicyEntityAsync(
                organizationId,
                policyId,
                cancellationToken);

        if (policy.Status != PolicyStatus.Active)
        {
            throw new PolicyException(
                "POLICY_NOT_ACTIVE",
                "La política debe estar activa antes de asignarla.");
        }

        var version =
            await _dbContext.PolicyVersions
                .AsNoTracking()
                .SingleAsync(
                    x =>
                        x.PolicyId == policy.Id &&
                        x.VersionNumber ==
                            policy.CurrentVersion,
                    cancellationToken);

        var requestedIds =
            request.DeviceIds
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToArray();

        var devices =
            await _dbContext.Devices
                .Where(x =>
                    requestedIds.Contains(x.Id) &&
                    x.OrganizationId == organizationId &&
                    !x.IsDeleted)
                .ToListAsync(cancellationToken);

        if (devices.Count != requestedIds.Length)
        {
            throw new PolicyException(
                "DEVICE_NOT_FOUND",
                "Uno o más dispositivos no existen.");
        }

        foreach (var device in devices)
        {
            var platformMatches =
                policy.Platform switch
                {
                    PolicyPlatform.Windows =>
                        device.Platform ==
                        DevicePlatform.Windows,

                    PolicyPlatform.Android =>
                        device.Platform ==
                        DevicePlatform.Android,

                    _ => false
                };

            if (!platformMatches)
            {
                throw new PolicyException(
                    "PLATFORM_MISMATCH",
                    $"El dispositivo {device.DeviceName} no corresponde a la plataforma {policy.Platform}.");
            }
        }

        foreach (var device in devices)
        {
            var existing =
                await _dbContext.DevicePolicyAssignments
                    .FirstOrDefaultAsync(
                        x =>
                            x.OrganizationId ==
                                organizationId &&
                            x.PolicyId == policy.Id &&
                            x.DeviceId == device.Id,
                        cancellationToken);

            if (existing is not null &&
                existing.Status !=
                    PolicyAssignmentStatus.Removed)
            {
                continue;
            }

            var assignment =
                existing ??
                new DevicePolicyAssignment(
                    organizationId,
                    policy.Id,
                    device.Id,
                    policy.CurrentVersion);

            if (existing is null)
            {
                _dbContext.DevicePolicyAssignments.Add(
                    assignment);
            }

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            var payload =
                JsonSerializer.Serialize(new
                {
                    policyId = policy.Id,
                    policyVersion =
                        policy.CurrentVersion,
                    platform =
                        policy.Platform.ToString(),
                    configuration =
                        JsonSerializer.Deserialize<object>(
                            version.ConfigurationJson)
                });

            var command =
                await _commandService.CreateAsync(
                    organizationId,
                    userId,
                    new CreateDeviceCommandRequest(
                        device.Id,
                        "APPLY_POLICY",
                        payload,
                        60),
                    cancellationToken);

            assignment.Queue(command.Id);

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        return await GetAssignmentsAsync(
            organizationId,
            policy.Id,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<PolicyAssignmentDto>>
        GetAssignmentsAsync(
            Guid organizationId,
            Guid policyId,
            CancellationToken cancellationToken = default)
    {
        var result =
            await (
                from assignment
                    in _dbContext.DevicePolicyAssignments
                        .AsNoTracking()
                join device
                    in _dbContext.Devices.AsNoTracking()
                    on assignment.DeviceId equals device.Id
                where
                    assignment.OrganizationId ==
                        organizationId &&
                    assignment.PolicyId == policyId &&
                    assignment.Status !=
                        PolicyAssignmentStatus.Removed
                orderby assignment.AssignedAtUtc descending
                select new PolicyAssignmentDto(
                    assignment.Id,
                    assignment.PolicyId,
                    assignment.DeviceId,
                    device.DeviceName,
                    assignment.PolicyVersion,
                    assignment.Status.ToString(),
                    assignment.CommandId,
                    assignment.AssignedAtUtc,
                    assignment.UpdatedAtUtc,
                    assignment.AppliedAtUtc,
                    assignment.ErrorMessage))
                .ToListAsync(cancellationToken);

        return result;
    }

    private async Task<Policy> GetPolicyEntityAsync(
        Guid organizationId,
        Guid policyId,
        CancellationToken cancellationToken)
    {
        var policy =
            await _dbContext.Policies
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == policyId &&
                        x.OrganizationId == organizationId,
                    cancellationToken);

        if (policy is null)
        {
            throw new PolicyException(
                "POLICY_NOT_FOUND",
                "La política no existe.");
        }

        return policy;
    }

    private static PolicyDto Map(
        Policy policy,
        int assignedDevices)
    {
        return new PolicyDto(
            policy.Id,
            policy.OrganizationId,
            policy.Name,
            policy.Description,
            policy.Platform.ToString(),
            policy.Status.ToString(),
            policy.CurrentVersion,
            assignedDevices,
            policy.CreatedByUserId,
            policy.CreatedAtUtc,
            policy.UpdatedAtUtc,
            policy.ActivatedAtUtc,
            policy.ArchivedAtUtc);
    }

    private static void ValidateIdentity(
        Guid organizationId,
        Guid userId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new PolicyException(
                "INVALID_ORGANIZATION",
                "La organización no es válida.");
        }

        if (userId == Guid.Empty)
        {
            throw new PolicyException(
                "INVALID_USER",
                "El usuario no es válido.");
        }
    }

    private static void ValidateJson(
        string configurationJson)
    {
        try
        {
            using var _ =
                JsonDocument.Parse(
                    NormalizeJson(configurationJson));
        }
        catch (JsonException)
        {
            throw new PolicyException(
                "INVALID_CONFIGURATION",
                "La configuración de la política no contiene JSON válido.");
        }
    }

    private static string NormalizeJson(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "{}"
            : value.Trim();
    }
}