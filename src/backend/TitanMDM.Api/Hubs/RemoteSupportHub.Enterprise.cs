using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR;

using TitanMDM.Application.Commands;
using TitanMDM.Domain.Entities;

namespace TitanMDM.Api.Hubs;

/*
 * Escritorio remoto Windows - capacidades de nivel comercial:
 *
 *  1. SetStreamProfile   : el tecnico elige calidad / resolucion / FPS.
 *  2. SendSecureAttention: Ctrl+Alt+Del real (SendSAS) ejecutado por el
 *                          servicio SYSTEM del agente; Windows ignora la
 *                          combinacion inyectada por SendInput.
 *  3. Transferencia de archivos tecnico -> equipo, por bloques con
 *     acuse del RemoteHost (contrapresion), hash SHA-256 y limite de
 *     tamano. Requiere AllowFileTransfer + control activo.
 *
 * Todas exigen remote.manage, alcance RBAC (GetHumanSessionAsync) y
 * ser el titular del control de la sesion.
 */
public sealed partial class RemoteSupportHub
{
    public const long MaxFileTransferBytes = 200L * 1024 * 1024;
    public const int FileChunkBytes = 256 * 1024;

    private static readonly HashSet<string> StreamPresets =
        new(StringComparer.OrdinalIgnoreCase) { "auto", "speed", "balanced", "quality" };

    private static readonly TimeSpan HostAckTimeout = TimeSpan.FromSeconds(30);

    private sealed record FileTransferState(
        Guid OrganizationId,
        Guid SessionId,
        Guid UserId,
        string HostConnectionId,
        string FileName,
        long SizeBytes,
        DateTime StartedAtUtc)
    {
        public long Received;
    }

    private static readonly ConcurrentDictionary<Guid, FileTransferState> FileTransfers = new();

    // ============================================================
    // 0. LATENCIA (RTT tecnico <-> servidor)
    // ============================================================

    /*
     * Eco del timestamp del cliente: el visor mide el RTT con su propio
     * reloj, sin depender de la hora del equipo remoto (sin desfase).
     */
    public long Ping(long clientTimestamp)
    {
        if (!IsHumanConnection())
        {
            throw new HubException("Solo para técnicos.");
        }

        return clientTimestamp;
    }

    // ============================================================
    // 1. PERFIL DE STREAMING
    // ============================================================

    public async Task SetStreamProfile(
        Guid sessionId,
        string preset,
        int maxWidth,
        int maxFps)
    {
        var session = await GetHumanSessionAsync(sessionId, "remote.manage");
        await RequireControlLeaseAsync(session);

        preset = (preset ?? "auto").Trim().ToLowerInvariant();

        if (!StreamPresets.Contains(preset))
        {
            throw new HubException("Perfil de calidad no válido.");
        }

        // 0 = automatico. Limites razonables para no saturar el canal.
        if (maxWidth != 0 && maxWidth is < 640 or > 3840)
        {
            throw new HubException("Resolución máxima no válida.");
        }

        if (maxFps != 0 && maxFps is < 5 or > 60)
        {
            throw new HubException("FPS máximos no válidos.");
        }

        await Clients
            .Group(HostGroup(session.OrganizationId, session.Id))
            .SendAsync("SetStreamProfile", preset, maxWidth, maxFps);

        await Clients
            .Group(TechnicianGroup(session.OrganizationId, session.Id))
            .SendAsync("RemoteStreamProfile", new { sessionId = session.Id, preset, maxWidth, maxFps });
    }

    // ============================================================
    // 2. CTRL + ALT + SUPR (SAS)
    // ============================================================

    public async Task<object> SendSecureAttention(Guid sessionId)
    {
        var session = await GetHumanSessionAsync(sessionId, "remote.manage");

        if (!session.AllowKeyboard)
        {
            throw new HubException("La sesión no permite teclado.");
        }

        await RequireControlLeaseAsync(session);

        var command =
            await _deviceCommands.CreateAsync(
                session.OrganizationId,
                GetHumanUserId(),
                new CreateDeviceCommandRequest(
                    session.DeviceId,
                    "REMOTE_SEND_SAS",
                    JsonSerializer.Serialize(new { remoteSessionId = session.Id }),
                    2),
                Context.ConnectionAborted);

        _logger.LogInformation(
            "Ctrl+Alt+Supr solicitado. Session={SessionId}, Device={DeviceId}, User={UserId}, Command={CommandId}.",
            session.Id,
            session.DeviceId,
            GetHumanUserId(),
            command.Id);

        return new { commandId = command.Id, status = command.Status.ToString() };
    }

    // ============================================================
    // 3. TRANSFERENCIA DE ARCHIVOS
    // ============================================================

    public async Task<object> BeginFileTransfer(
        Guid sessionId,
        string fileName,
        long sizeBytes)
    {
        var session = await GetHumanSessionAsync(sessionId, "remote.manage");
        RequireFileTransfer(session);
        await RequireControlLeaseAsync(session);

        var safeName = SanitizeFileName(fileName);

        if (sizeBytes is <= 0 or > MaxFileTransferBytes)
        {
            throw new HubException($"El archivo debe pesar entre 1 byte y {MaxFileTransferBytes / (1024 * 1024)} MB.");
        }

        var hostConnectionId = RequireHostConnection(session);
        var transferId = Guid.NewGuid();

        var accepted =
            await InvokeHostAsync<bool>(
                hostConnectionId,
                "BeginFileTransfer",
                transferId,
                safeName,
                sizeBytes);

        if (!accepted)
        {
            throw new HubException("El equipo remoto rechazó la transferencia.");
        }

        FileTransfers[transferId] =
            new FileTransferState(
                session.OrganizationId,
                session.Id,
                GetHumanUserId(),
                hostConnectionId,
                safeName,
                sizeBytes,
                DateTime.UtcNow);

        _logger.LogInformation(
            "Transferencia iniciada. Session={SessionId}, Transfer={TransferId}, File={FileName}, Bytes={Bytes}.",
            session.Id,
            transferId,
            safeName,
            sizeBytes);

        return new { transferId, chunkSize = FileChunkBytes, fileName = safeName };
    }

    public async Task<long> SendFileChunk(
        Guid sessionId,
        Guid transferId,
        long offset,
        byte[] data)
    {
        var transfer = RequireTransfer(sessionId, transferId);

        if (data is null || data.Length is 0 or > FileChunkBytes)
        {
            throw new HubException("Bloque de archivo no válido.");
        }

        if (offset != transfer.Received || offset + data.Length > transfer.SizeBytes)
        {
            throw new HubException("Bloque fuera de secuencia.");
        }

        var received =
            await InvokeHostAsync<long>(
                transfer.HostConnectionId,
                "ReceiveFileChunk",
                transferId,
                offset,
                data);

        transfer.Received = received;
        return received;
    }

    public async Task<object> CompleteFileTransfer(
        Guid sessionId,
        Guid transferId,
        string sha256)
    {
        var transfer = RequireTransfer(sessionId, transferId);

        if (transfer.Received != transfer.SizeBytes)
        {
            throw new HubException("La transferencia está incompleta.");
        }

        try
        {
            var savedAs =
                await InvokeHostAsync<string>(
                    transfer.HostConnectionId,
                    "CompleteFileTransfer",
                    transferId,
                    (sha256 ?? string.Empty).Trim().ToLowerInvariant());

            _logger.LogInformation(
                "Transferencia completada. Session={SessionId}, Transfer={TransferId}, SavedAs={SavedAs}, Elapsed={Elapsed}.",
                transfer.SessionId,
                transferId,
                savedAs,
                DateTime.UtcNow - transfer.StartedAtUtc);

            return new { fileName = transfer.FileName, savedAs, bytes = transfer.SizeBytes };
        }
        finally
        {
            FileTransfers.TryRemove(transferId, out _);
        }
    }

    public async Task CancelFileTransfer(
        Guid sessionId,
        Guid transferId)
    {
        if (!FileTransfers.TryGetValue(transferId, out var transfer) ||
            transfer.SessionId != sessionId ||
            transfer.UserId != GetHumanUserId())
        {
            return;
        }

        FileTransfers.TryRemove(transferId, out _);

        await Clients
            .Client(transfer.HostConnectionId)
            .SendAsync("CancelFileTransfer", transferId);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static void RequireFileTransfer(RemoteSession session)
    {
        if (!session.AllowFileTransfer)
        {
            throw new HubException("La sesión no permite transferencia de archivos.");
        }
    }

    private string RequireHostConnection(RemoteSession session)
    {
        if (!_connections.TryGetRemoteHostConnectionId(
                session.OrganizationId,
                session.Id,
                out var connectionId))
        {
            throw new HubException("El equipo remoto no está conectado.");
        }

        return connectionId;
    }

    private FileTransferState RequireTransfer(Guid sessionId, Guid transferId)
    {
        if (!FileTransfers.TryGetValue(transferId, out var transfer) ||
            transfer.SessionId != sessionId ||
            transfer.OrganizationId != GetHumanOrganizationId() ||
            transfer.UserId != GetHumanUserId())
        {
            throw new HubException("Transferencia no encontrada.");
        }

        // El control pudo perderse a mitad de la transferencia.
        if (!_connections.TryGetControlLease(transfer.OrganizationId, transfer.SessionId, out var lease) ||
            lease.UserId != transfer.UserId)
        {
            FileTransfers.TryRemove(transferId, out _);
            throw new HubException("Se perdió el control de la sesión; transferencia cancelada.");
        }

        return transfer;
    }

    private async Task<T> InvokeHostAsync<T>(string connectionId, string method, params object?[] args)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Context.ConnectionAborted);
        timeout.CancelAfter(HostAckTimeout);

        try
        {
            return args.Length switch
            {
                2 => await Clients.Client(connectionId).InvokeAsync<T>(method, args[0], args[1], timeout.Token),
                3 => await Clients.Client(connectionId).InvokeAsync<T>(method, args[0], args[1], args[2], timeout.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(args)),
            };
        }
        catch (OperationCanceledException) when (!Context.ConnectionAborted.IsCancellationRequested)
        {
            throw new HubException("El equipo remoto no respondió a tiempo.");
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoteHost rechazó {Method}.", method);
            throw new HubException($"El equipo remoto rechazó la operación: {ex.Message}");
        }
    }

    internal static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        name = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && !char.IsControl(c)).ToArray()).Trim(' ', '.');

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new HubException("Nombre de archivo no válido.");
        }

        return name.Length > 180 ? name[..180] : name;
    }
}
