
using System.IO.Pipes;
using System.Text.Json;
using TitanMDM.RemoteHost.Models;

namespace TitanMDM.RemoteHost.Interop;

/// <summary>
/// Cliente del bootstrap seguro entregado por WindowsAgent.
///
/// El canal se utiliza una sola vez durante el inicio
/// de una sesión remota autorizada.
/// </summary>
public static class RemoteHostBootstrapPipeClient
{
    private const int MaxPayloadBytes = 65536;

    private const byte Acknowledgement = 0xAC;

    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromSeconds(20);

    public static async Task<RemoteHostSession> ReceiveAsync(
        string pipeName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pipeName) ||
            !pipeName.StartsWith(
                "titanmdm-rh-",
                StringComparison.Ordinal) ||
            pipeName.Length > 100 ||
            pipeName.Any(c =>
                !(char.IsLetterOrDigit(c) ||
                  c == '-')))
        {
            throw new ArgumentException(
                "Nombre del canal bootstrap inválido.",
                nameof(pipeName));
        }

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(ConnectionTimeout);

        using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(timeout.Token);

            var lengthBytes =
                new byte[sizeof(int)];

            await pipe.ReadExactlyAsync(
                lengthBytes.AsMemory(),
                timeout.Token);

            var length =
                BitConverter.ToInt32(lengthBytes);

            if (length <= 0 ||
                length > MaxPayloadBytes)
            {
                throw new InvalidDataException(
                    "Longitud del bootstrap inválida.");
            }

            var bytes = new byte[length];

            try
            {
                await pipe.ReadExactlyAsync(
                    bytes.AsMemory(),
                    timeout.Token);

                var session =
                    JsonSerializer.Deserialize<RemoteHostSession>(
                        bytes,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (session is null ||
                    session.SessionId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(
                        session.ServerUrl) ||
                    string.IsNullOrWhiteSpace(
                        session.AccessToken) ||
                    session.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    throw new InvalidDataException(
                        "Bootstrap inválido o expirado.");
                }

                // Confirmación explícita al servicio.
                // No incluye credenciales ni información
                // sensible del dispositivo.
                await pipe.WriteAsync(
                    new byte[] { Acknowledgement },
                    timeout.Token);

                await pipe.FlushAsync(timeout.Token);

                return session;
            }
            finally
            {
                Array.Clear(bytes);
            }
        }
        catch (EndOfStreamException ex)
        {
            throw new IOException(
                "El canal IPC terminó antes de recibir " +
                "el bootstrap completo. Comprueba que " +
                "WindowsAgent y RemoteHost estén " +
                "actualizados con el mismo protocolo.",
                ex);
        }
        catch (OperationCanceledException ex)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Tiempo de espera agotado durante " +
                "la entrega del bootstrap RemoteHost.",
                ex);
        }
    }
}
