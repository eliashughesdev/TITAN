
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using TitanMDM.WindowsAgent.Contracts;

namespace TitanMDM.WindowsAgent.Services;

/// <summary>
/// Entrega segura de la configuración de RemoteHost.
///
/// Protocolo:
/// 1. El agente abre el canal IPC.
/// 2. RemoteHost se conecta.
/// 3. El agente verifica el PID.
/// 4. El agente envía longitud y payload.
/// 5. RemoteHost verifica y confirma la recepción.
/// 6. El agente cierra el canal.
///
/// El token nunca se transmite mediante argumentos
/// del proceso ni se escribe en logs.
/// </summary>
public sealed class RemoteHostBootstrapPipeServer
    : IAsyncDisposable
{
    private const int MaxPayloadBytes = 65536;

    private const byte Acknowledgement = 0xAC;

    private static readonly TimeSpan DeliveryTimeout =
        TimeSpan.FromSeconds(25);

    private readonly NamedPipeServerStream _pipe;
    private readonly byte[] _payload;
    private readonly string _pipeName;

    private int _deliveryStarted;
    private int _disposed;

    public string PipeName => _pipeName;

    public RemoteHostBootstrapPipeServer(
        RemoteDesktopStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SessionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.AccessToken) ||
            string.IsNullOrWhiteSpace(request.ServerUrl) ||
            request.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                "Bootstrap remoto inválido.",
                nameof(request));
        }

        _pipeName =
            $"titanmdm-rh-{Guid.NewGuid():N}";

        _payload =
            JsonSerializer.SerializeToUtf8Bytes(request);

        if (_payload.Length == 0 ||
            _payload.Length > MaxPayloadBytes)
        {
            throw new InvalidOperationException(
                "Tamaño del bootstrap inválido.");
        }

        var security = new PipeSecurity();

        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.LocalSystemSid,
                    null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.BuiltinAdministratorsSid,
                    null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        // El usuario interactivo puede conectar al canal.
        // El PID autorizado se verifica antes de entregar
        // cualquier byte del payload.
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.InteractiveSid,
                    null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

        _pipe = NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            4096,
            4096,
            security);
    }

    public async Task DeliverAsync(
        int expectedProcessId,
        CancellationToken cancellationToken = default)
    {
        if (expectedProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedProcessId));
        }

        if (Interlocked.CompareExchange(
                ref _deliveryStarted,
                1,
                0) != 0)
        {
            throw new InvalidOperationException(
                "El bootstrap ya inició su entrega.");
        }

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(DeliveryTimeout);

        await _pipe.WaitForConnectionAsync(
            timeout.Token);

        if (!GetNamedPipeClientProcessId(
                _pipe.SafePipeHandle,
                out var clientProcessId))
        {
            throw new InvalidOperationException(
                "No se pudo verificar la identidad IPC.");
        }

        if (clientProcessId !=
            (uint)expectedProcessId)
        {
            throw new UnauthorizedAccessException(
                "El cliente IPC no corresponde al proceso autorizado.");
        }

        var lengthBytes =
            BitConverter.GetBytes(_payload.Length);

        await _pipe.WriteAsync(
            lengthBytes.AsMemory(),
            timeout.Token);

        await _pipe.WriteAsync(
            _payload.AsMemory(),
            timeout.Token);

        await _pipe.FlushAsync(timeout.Token);

        // Esperar confirmación de que RemoteHost leyó
        // y validó el bootstrap antes de desconectar.
        var acknowledgement = new byte[1];

        await _pipe.ReadExactlyAsync(
            acknowledgement.AsMemory(),
            timeout.Token);

        if (acknowledgement[0] != Acknowledgement)
        {
            throw new InvalidOperationException(
                "RemoteHost no confirmó el bootstrap.");
        }

        _pipe.Disconnect();
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) == 0)
        {
            _pipe.Dispose();
            Array.Clear(_payload);
        }

        return ValueTask.CompletedTask;
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);
}
