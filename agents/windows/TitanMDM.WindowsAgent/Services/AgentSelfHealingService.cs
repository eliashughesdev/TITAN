using TitanMDM.WindowsAgent.Storage;

namespace TitanMDM.WindowsAgent.Services;

public sealed class
    AgentSelfHealingService
{
    private readonly
        DeviceIdentityStore
        _identityStore;

    private readonly
        AgentRuntimeSettingsStore
        _settingsStore;

    private readonly ILogger<
        AgentSelfHealingService>
        _logger;

    public AgentSelfHealingService(
        DeviceIdentityStore identityStore,
        AgentRuntimeSettingsStore settingsStore,
        ILogger<
            AgentSelfHealingService>
            logger)
    {
        _identityStore =
            identityStore;

        _settingsStore =
            settingsStore;

        _logger =
            logger;
    }

    public async Task<
        AgentRepairResult>
        RepairAsync(
            CancellationToken cancellationToken =
                default)
    {
        var repairs =
            new List<string>();

        var warnings =
            new List<string>();

        var programData =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .CommonApplicationData),
                "TitanMDM");

        var logs =
            Path.Combine(
                programData,
                "logs");

        var diagnostics =
            Path.Combine(
                programData,
                "diagnostics");

        EnsureDirectory(
            programData,
            repairs);

        EnsureDirectory(
            logs,
            repairs);

        EnsureDirectory(
            diagnostics,
            repairs);

        if (
            !_settingsStore
                .Exists())
        {
            warnings.Add(
                "agentsettings.json no existe.");
        }

        if (
            _identityStore
                .Exists())
        {
            var identity =
                await _identityStore
                    .LoadAsync(
                        cancellationToken);

            if (identity is null)
            {
                warnings.Add(
                    "device.json existe pero no puede descifrarse o está corrupto.");

                /*
                 * NO eliminamos la identidad automáticamente.
                 *
                 * Un borrado silencioso podría provocar una
                 * inscripción duplicada.
                 *
                 * La recuperación de identidad debe pasar por
                 * el lifecycle/enrollment controlado.
                 */
            }
        }
        else
        {
            warnings.Add(
                "device.json todavía no existe.");
        }

        var remoteHost =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .ProgramFiles),
                "TitanMDM",
                "RemoteHost",
                "TitanMDM.RemoteHost.exe");

        if (
            !File.Exists(
                remoteHost))
        {
            warnings.Add(
                "TitanMDM.RemoteHost.exe no existe.");
        }

        var agentExe =
            Environment.ProcessPath;

        if (
            string.IsNullOrWhiteSpace(
                agentExe)
            ||
            !File.Exists(
                agentExe))
        {
            warnings.Add(
                "No fue posible validar el ejecutable principal del agente.");
        }

        var result =
            new AgentRepairResult(
                DateTime.UtcNow,
                repairs.ToArray(),
                warnings.ToArray());

        if (
            repairs.Count >
            0)
        {
            _logger.LogInformation(
                "TitanMDM Self-Healing aplicó {Count} reparaciones.",
                repairs.Count);
        }

        foreach (
            var warning
            in warnings)
        {
            _logger.LogWarning(
                "TitanMDM Self-Healing: {Warning}",
                warning);
        }

        return result;
    }

    private static void EnsureDirectory(
        string path,
        ICollection<string> repairs)
    {
        if (
            Directory.Exists(
                path))
        {
            return;
        }

        Directory.CreateDirectory(
            path);

        repairs.Add(
            $"Directorio creado: {path}");
    }
}

public sealed record
    AgentRepairResult(
        DateTime ExecutedAtUtc,
        IReadOnlyCollection<string>
            Repairs,
        IReadOnlyCollection<string>
            Warnings);