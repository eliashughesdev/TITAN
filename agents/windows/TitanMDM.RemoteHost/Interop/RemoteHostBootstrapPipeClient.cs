
using System.IO.Pipes;
using System.Text.Json;

using TitanMDM.RemoteHost.Models;

namespace TitanMDM.RemoteHost.Interop;

public static class RemoteHostBootstrapPipeClient
{
    private const int MaxPayloadBytes = 65536;

    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromSeconds(15);

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
                !(char.IsLetterOrDigit(c) || c == '-')))
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
            PipeDirection.In,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync(timeout.Token);

        var lengthBytes = new byte[sizeof(int)];

        await pipe.ReadExactlyAsync(
            lengthBytes.AsMemory(),
            timeout.Token);

        var length = BitConverter.ToInt32(lengthBytes);

        if (length <= 0 || length > MaxPayloadBytes)
        {
            throw new InvalidOperationException(
                "Longitud del bootstrap no válida.");
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
                string.IsNullOrWhiteSpace(session.ServerUrl) ||
                string.IsNullOrWhiteSpace(session.AccessToken) ||
                session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException(
                    "Bootstrap recibido inválido o expirado.");
            }

            return session;
        }
        finally
        {
            Array.Clear(bytes);
        }
    }
}
