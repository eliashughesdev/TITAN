using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Commands;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Security;

public sealed class WindowsRemediationOptions
{
    public const string SectionName = "Remediation:Windows";

    /* Interruptor global del motor. */
    public bool Enabled { get; set; } = true;

    /*
     * Ventana anti-bucle: no se repite el mismo comando
     * correctivo para el mismo equipo dentro de este
     * periodo, aunque el fallo persista.
     */
    public int CooldownMinutes { get; set; } = 60;

    /* Expiracion de los comandos encolados. */
    public int CommandExpirationMinutes { get; set; } = 60;

    /* Encola COMPLIANCE_CHECK tras remediar. */
    public bool VerifyAfterRemediation { get; set; } = true;

    /*
     * Usuario que figura como autor de los comandos
     * automaticos. Si se omite se usa el primer
     * SuperAdmin activo de la organizacion.
     */
    public string? ActorUserEmail { get; set; }
}

public sealed class WindowsRemediationEngine
    : IWindowsRemediationEngine
{
    internal const string VerificationCommandType = "COMPLIANCE_CHECK";

    private const string SuperAdminRoleName = "SuperAdmin";

    private sealed record Playbook(
        string CommandType,
        object? Payload,
        string Description);

    /*
     * Catalogo de remediaciones automaticas. Solo
     * acciones idempotentes y no destructivas.
     */
    private static readonly IReadOnlyDictionary<string, Playbook>
        Automatic =
            new Dictionary<string, Playbook>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["DEFENDER_AVAILABLE"] =
                    new(
                        "SERVICE_START",
                        new { serviceName = "WinDefend" },
                        "Iniciar servicio Microsoft Defender Antivirus (WinDefend)."),

                ["FIREWALL_AVAILABLE"] =
                    new(
                        "SERVICE_START",
                        new { serviceName = "mpssvc" },
                        "Iniciar servicio Windows Defender Firewall (mpssvc)."),
            };

    /*
     * Fallos que requieren intervencion o ventana de
     * mantenimiento: se informan, no se ejecutan.
     */
    private static readonly IReadOnlyDictionary<string, string>
        ManualGuidance =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["UAC_ENABLED"] =
                    "Reactivar UAC por politica (APPLY_POLICY/GPO); requiere reinicio.",
                ["SECURE_BOOT"] =
                    "Habilitar Secure Boot en el firmware UEFI del equipo.",
                ["TPM_STATUS"] =
                    "Verificar/activar TPM en el firmware del equipo.",
                ["BITLOCKER_STATUS"] =
                    "Revisar BitLocker y custodia de la clave de recuperacion antes de cifrar.",
                ["NO_PENDING_REBOOT"] =
                    "Programar RESTART_DEVICE en ventana de mantenimiento.",
            };

    private readonly TitanMdmDbContext _db;
    private readonly IDeviceCommandService _commands;
    private readonly WindowsRemediationOptions _options;
    private readonly ILogger<WindowsRemediationEngine> _logger;

    public WindowsRemediationEngine(
        TitanMdmDbContext db,
        IDeviceCommandService commands,
        IOptions<WindowsRemediationOptions> options,
        ILogger<WindowsRemediationEngine> logger)
    {
        _db = db;
        _commands = commands;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WindowsRemediationResult> RemediateAsync(
        Guid organizationId,
        Guid deviceId,
        IReadOnlyCollection<string> failedCheckCodes,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return WindowsRemediationResult.Disabled(
                "Remediacion automatica deshabilitada (Remediation:Windows:Enabled).");
        }

        var codes =
            failedCheckCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (codes.Length == 0)
        {
            return WindowsRemediationResult.Disabled(
                "Sin controles fallidos.");
        }

        var manual =
            codes
                .Where(ManualGuidance.ContainsKey)
                .Select(code =>
                    new WindowsRemediationAction(
                        code,
                        "MANUAL",
                        ManualGuidance[code]))
                .ToList();

        var planned =
            codes
                .Where(Automatic.ContainsKey)
                .Select(code => (Code: code, Playbook: Automatic[code]))
                .ToList();

        if (planned.Count == 0)
        {
            return new WindowsRemediationResult(
                true,
                Array.Empty<WindowsRemediationAction>(),
                Array.Empty<WindowsRemediationAction>(),
                manual,
                "Ningun fallo admite remediacion automatica.");
        }

        var device =
            await _db.Devices
                .AsNoTracking()
                .Where(x =>
                    x.Id == deviceId &&
                    x.OrganizationId == organizationId &&
                    !x.IsDeleted)
                .Select(x => new { x.Platform })
                .SingleOrDefaultAsync(cancellationToken);

        if (device is null ||
            device.Platform != DevicePlatform.Windows)
        {
            return WindowsRemediationResult.Disabled(
                "El dispositivo no existe o no es Windows.");
        }

        var actorId =
            await ResolveActorAsync(
                organizationId,
                cancellationToken);

        if (actorId is null)
        {
            _logger.LogWarning(
                "Remediacion Windows omitida para {DeviceId}: no hay actor (SuperAdmin activo o Remediation:Windows:ActorUserEmail).",
                deviceId);

            return new WindowsRemediationResult(
                true,
                Array.Empty<WindowsRemediationAction>(),
                Array.Empty<WindowsRemediationAction>(),
                manual,
                "No existe un usuario actor valido para comandos automaticos.");
        }

        var since =
            DateTime.UtcNow.AddMinutes(
                -Math.Max(1, _options.CooldownMinutes));

        var recent =
            await _db.DeviceCommands
                .AsNoTracking()
                .Where(x =>
                    x.DeviceId == deviceId &&
                    x.OrganizationId == organizationId &&
                    x.CreatedAtUtc >= since)
                .Select(x => new { x.CommandType, x.PayloadJson })
                .ToListAsync(cancellationToken);

        var recentKeys =
            recent
                .Select(x => Key(x.CommandType, x.PayloadJson))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var queued = new List<WindowsRemediationAction>();
        var suppressed = new List<WindowsRemediationAction>();

        foreach (var (code, playbook) in planned)
        {
            var payloadJson =
                playbook.Payload is null
                    ? "{}"
                    : JsonSerializer.Serialize(playbook.Payload);

            var action =
                new WindowsRemediationAction(
                    code,
                    playbook.CommandType,
                    playbook.Description);

            if (!recentKeys.Add(Key(playbook.CommandType, payloadJson)))
            {
                suppressed.Add(action);
                continue;
            }

            var command =
                await QueueAsync(
                    organizationId,
                    actorId.Value,
                    deviceId,
                    playbook.CommandType,
                    payloadJson,
                    cancellationToken);

            if (command is null)
            {
                suppressed.Add(action);
                continue;
            }

            queued.Add(action with { CommandId = command });
        }

        if (queued.Count > 0 && _options.VerifyAfterRemediation)
        {
            var verifyPayload =
                JsonSerializer.Serialize(
                    new { reason = "remediation-verify" });

            if (recentKeys.Add(Key(VerificationCommandType, verifyPayload)))
            {
                var verifyId =
                    await QueueAsync(
                        organizationId,
                        actorId.Value,
                        deviceId,
                        VerificationCommandType,
                        verifyPayload,
                        cancellationToken);

                if (verifyId is not null)
                {
                    queued.Add(
                        new WindowsRemediationAction(
                            "VERIFY",
                            VerificationCommandType,
                            "Re-evaluar cumplimiento tras la remediacion.",
                            verifyId));
                }
            }
        }

        if (queued.Count > 0)
        {
            _logger.LogInformation(
                "Remediacion Windows {DeviceId}: {Count} comando(s) encolado(s): {Commands}.",
                deviceId,
                queued.Count,
                string.Join(", ", queued.Select(x => x.CommandType + ":" + x.CheckCode)));
        }

        return new WindowsRemediationResult(
            true,
            queued,
            suppressed,
            manual,
            queued.Count == 0
                ? "Remediacion ya ejecutada dentro de la ventana de enfriamiento."
                : null);
    }

    private async Task<Guid?> QueueAsync(
        Guid organizationId,
        Guid actorId,
        Guid deviceId,
        string commandType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto =
                await _commands.CreateAsync(
                    organizationId,
                    actorId,
                    new CreateDeviceCommandRequest(
                        deviceId,
                        commandType,
                        payloadJson,
                        Math.Clamp(_options.CommandExpirationMinutes, 1, 1440)),
                    cancellationToken);

            return dto.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "No se pudo encolar {CommandType} de remediacion para {DeviceId}.",
                commandType,
                deviceId);

            return null;
        }
    }

    private async Task<Guid?> ResolveActorAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ActorUserEmail))
        {
            var email = _options.ActorUserEmail.Trim();

            var configured =
                await _db.Users
                    .AsNoTracking()
                    .Where(x =>
                        x.OrganizationId == organizationId &&
                        x.IsActive &&
                        x.Email == email)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            if (configured is not null)
            {
                return configured;
            }
        }

        return await (
                from user in _db.Users.AsNoTracking()
                join userRole in _db.UserRoles.AsNoTracking()
                    on user.Id equals userRole.UserId
                join role in _db.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where
                    user.OrganizationId == organizationId &&
                    user.IsActive &&
                    role.IsActive &&
                    role.Name == SuperAdminRoleName
                orderby user.CreatedAtUtc
                select (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string Key(string commandType, string payloadJson)
    {
        return commandType.Trim() + "|" + NormalizeJson(payloadJson);
    }

    private static string NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException)
        {
            return json.Trim();
        }
    }
}
