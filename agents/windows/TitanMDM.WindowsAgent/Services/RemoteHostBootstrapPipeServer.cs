
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

using TitanMDM.WindowsAgent.Contracts;

namespace TitanMDM.WindowsAgent.Services;

/// <summary>
/// Entrega de configuración al RemoteHost mediante IPC local.
/// Una única conexión de un PID autorizado puede recibir el payload.
/// El AccessToken nunca aparece en los argumentos del proceso.
/// </summary>
public sealed class RemoteHostBootstrapPipeServer
    : IAsyncDisposable
{
    private const int MaxPayloadBytes = 65536;

    private static readonly TimeSpan DeliveryTimeout =
        TimeSpan.FromSeconds(20);

    private readonly NamedPipeServerStream _pipe;
    private readonly byte[] _payload;
    private readonly string _pipeName;

    private int _delivered;
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

        _payload = JsonSerializer.SerializeToUtf8Bytes(request);

        if (_payload.Length > MaxPayloadBytes)
        {
            throw new InvalidOperationException(
                "El bootstrap excede el tamaño permitido.");
        }

        var security = new PipeSecurity();

        // LocalSystem: servicio supervisor.
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.LocalSystemSid,
                    null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        // SYSTEM y administradores locales no dependen de
        // que la cuenta interactiva sea administradora.
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.BuiltinAdministratorsSid,
                    null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        // Permite conectarse a usuarios interactivos.
        // La autorización real para recibir el token
        // se realiza adicionalmente por PID.
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.InteractiveSid,
                    null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

        _pipe = NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.Out,
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
                ref _delivered, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "El bootstrap ya fue consumido.");
        }

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(DeliveryTimeout);

        await _pipe.WaitForConnectionAsync(timeout.Token);

        if (!GetNamedPipeClientProcessId(
                _pipe.SafePipeHandle,
                out var clientPid))
        {
            throw new InvalidOperationException(
                "No fue posible identificar el proceso IPC.");
        }

        if (clientPid != (uint)expectedProcessId)
        {
            throw new UnauthorizedAccessException(
                "El proceso que solicitó el bootstrap no está autorizado.");
        }

        var size = BitConverter.GetBytes(_payload.Length);

        await _pipe.WriteAsync(size, timeout.Token);

        await _pipe.WriteAsync(_payload, timeout.Token);

        await _pipe.FlushAsync(timeout.Token);

        // El servidor cierra el canal tras una única entrega.
        _pipe.Disconnect();
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
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
