using Microsoft.AspNetCore.SignalR.Client;

using TitanMDM.RemoteHost.Capture;
using TitanMDM.RemoteHost.Input;
using TitanMDM.RemoteHost.Interop;
using TitanMDM.RemoteHost.Models;

namespace TitanMDM.RemoteHost.Transport;

public sealed class RemoteTransportClient : IAsyncDisposable
{
    private readonly RemoteHostSession _session;
    private readonly DesktopCaptureService _captureService;
    private readonly RemoteInputController _inputController;
    private readonly WindowsDesktopStateProbe _desktopProbe = new();

    private HubConnection? _connection;
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;
    private long _framesPublished;
    private bool _disposing;
    private int _connectionLostRaised;

    public event Action<string>? StatusChanged;
    public event Action? ConnectionLost;

    public RemoteTransportClient(RemoteHostSession session)
    {
        _session = session;
        _captureService = new DesktopCaptureService();
        _inputController = new RemoteInputController();
    }

    public bool IsConnected =>
        _connection?.State == HubConnectionState.Connected;

    private bool CanUseDefaultDesktop =>
        _desktopProbe.GetAvailability() ==
        DesktopAvailability.Default;

    public async Task ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return;
        }

        var hubUrl =
            $"{_session.ServerUrl.TrimEnd('/')}/hubs/remote-support";

        var connection = new HubConnectionBuilder()
            .WithUrl(
                hubUrl,
                options =>
                {
                    options.Headers["X-Titan-Remote-Session"] =
                        _session.SessionId.ToString();

                    options.Headers["X-Titan-Remote-Token"] =
                        _session.AccessToken;
                })
            .WithAutomaticReconnect(
                new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
            .Build();

        _connection = connection;
        RegisterHandlers(connection);

        connection.Reconnecting += error =>
        {
            var suffix = error is null
                ? string.Empty
                : $" - {error.Message}";

            StatusChanged?.Invoke($"Reconectando{suffix}");
            return Task.CompletedTask;
        };

        connection.Reconnected += async _ =>
        {
            try
            {
                StatusChanged?.Invoke("Re-registrando");

                // La conexión nueva debe volver a ingresar al HostGroup.
                // El token original de ConnectAsync ya puede estar cancelado.
                await RegisterRemoteHostAsync(
                    CancellationToken.None);

                await PublishMonitorStateAsync(
                    CancellationToken.None);

                StatusChanged?.Invoke("Conectado");
                StartStreaming();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(
                    $"Error re-registro: {ex.Message}");

                RaiseConnectionLost();
            }
        };

        connection.Closed += error =>
        {
            if (!_disposing)
            {
                var suffix = error is null
                    ? string.Empty
                    : $" - {error.Message}";

                StatusChanged?.Invoke(
                    $"Desconectado{suffix}");

                // SignalR agotó la reconexión. La UI cerrará el
                // proceso y WindowsAgent podrá volver a iniciarlo.
                RaiseConnectionLost();
            }

            return Task.CompletedTask;
        };

        try
        {
            StatusChanged?.Invoke("Conectando");

            await connection.StartAsync(
                cancellationToken);

            StatusChanged?.Invoke(
                "Registrando RemoteHost");

            await RegisterRemoteHostAsync(
                cancellationToken);

            await PublishMonitorStateAsync(
                cancellationToken);

            StatusChanged?.Invoke("Conectado");
            StartStreaming();
        }
        catch
        {
            _disposing = true;
            _connection = null;
            await connection.DisposeAsync();
            throw;
        }
    }

    private void RaiseConnectionLost()
    {
        if (_disposing ||
            Interlocked.Exchange(
                ref _connectionLostRaised,
                1) != 0)
        {
            return;
        }

        _streamCancellation?.Cancel();
        ConnectionLost?.Invoke();
    }

    private async Task RegisterRemoteHostAsync(
        CancellationToken cancellationToken)
    {
        var connection = _connection;

        if (connection is null ||
            connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException(
                "SignalR no está conectado para registrar RemoteHost.");
        }

        await connection.InvokeAsync(
            "RegisterRemoteHost",
            _session.SessionId,
            cancellationToken);
    }

    private void RegisterHandlers(
        HubConnection connection)
    {
        connection.On<double, double>(
            "PointerMove",
            (x, y) =>
            {
                if (!_session.AllowMouse ||
                    !CanUseDefaultDesktop)
                {
                    return;
                }

                var bounds =
                    _captureService.GetSelectedBounds();

                _inputController.MovePointer(
                    x,
                    y,
                    bounds);
            });

        connection.On<int>(
            "SelectMonitor",
            async monitorIndex =>
            {
                var selected =
                    _captureService.SelectDisplay(
                        monitorIndex);

                StatusChanged?.Invoke(
                    $"Transmitiendo · {selected.Label}");

                await PublishMonitorStateAsync(
                    CancellationToken.None);
            });

        connection.On(
            "NextMonitor",
            async () =>
            {
                var selected =
                    _captureService.SelectNextDisplay();

                StatusChanged?.Invoke(
                    $"Transmitiendo · {selected.Label}");

                await PublishMonitorStateAsync(
                    CancellationToken.None);
            });

        connection.On(
            "PreviousMonitor",
            async () =>
            {
                var selected =
                    _captureService.SelectPreviousDisplay();

                StatusChanged?.Invoke(
                    $"Transmitiendo · {selected.Label}");

                await PublishMonitorStateAsync(
                    CancellationToken.None);
            });

        connection.On(
            "RequestMonitorState",
            async () =>
            {
                await PublishMonitorStateAsync(
                    CancellationToken.None);
            });

        connection.On<string>(
            "PointerButton",
            action =>
            {
                if (!_session.AllowMouse ||
                    !CanUseDefaultDesktop)
                {
                    return;
                }

                switch (action)
                {
                    case "left-down":
                        _inputController.LeftDown();
                        break;

                    case "left-up":
                        _inputController.LeftUp();
                        break;

                    case "right-down":
                        _inputController.RightDown();
                        break;

                    case "right-up":
                        _inputController.RightUp();
                        break;
                }
            });

        connection.On<int>(
            "PointerWheel",
            delta =>
            {
                if (_session.AllowMouse &&
                    CanUseDefaultDesktop)
                {
                    _inputController.Wheel(delta);
                }
            });

        connection.On<int, bool>(
            "Keyboard",
            (virtualKey, keyDown) =>
            {
                if (!_session.AllowKeyboard ||
                    !CanUseDefaultDesktop)
                {
                    return;
                }

                if (virtualKey < ushort.MinValue ||
                    virtualKey > ushort.MaxValue)
                {
                    return;
                }

                var key = (ushort)virtualKey;

                if (keyDown)
                {
                    _inputController.KeyDown(key);
                }
                else
                {
                    _inputController.KeyUp(key);
                }
            });

        connection.On(
            "TerminateRemoteSession",
            () =>
            {
                _streamCancellation?.Cancel();
                Application.Exit();
            });
    }

    private async Task PublishMonitorStateAsync(
        CancellationToken cancellationToken)
    {
        var connection = _connection;

        if (connection is null ||
            connection.State != HubConnectionState.Connected)
        {
            return;
        }

        var displays =
            _captureService.GetDisplays();

        var selected =
            _captureService.GetSelectedDisplay();

        var payload = displays
            .Select(
                display =>
                    new RemoteMonitorInfo(
                        Index: display.Index,
                        DeviceName: display.DeviceName,
                        Width: display.Width,
                        Height: display.Height,
                        IsPrimary: display.IsPrimary,
                        Label: display.Label))
            .ToArray();

        await connection.InvokeAsync(
            "PublishMonitorState",
            _session.SessionId,
            selected.Index,
            payload,
            cancellationToken);
    }

    private void StartStreaming()
    {
        if (_streamTask is not null)
        {
            return;
        }

        _streamCancellation =
            new CancellationTokenSource();

        var token = _streamCancellation.Token;

        _streamTask = Task.Run(
            () => StreamLoopAsync(token));
    }

    private async Task StreamLoopAsync(
        CancellationToken cancellationToken)
    {
        StatusChanged?.Invoke("Iniciando captura");

        var desktopPaused = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var connection = _connection;

            if (connection?.State !=
                HubConnectionState.Connected)
            {
                await DelaySafeAsync(
                    250,
                    cancellationToken);

                continue;
            }

            if (!CanUseDefaultDesktop)
            {
                if (!desktopPaused)
                {
                    StatusChanged?.Invoke(
                        "Escritorio bloqueado o protegido; captura y entrada en pausa");

                    desktopPaused = true;
                }

                await DelaySafeAsync(
                    1000,
                    cancellationToken);

                continue;
            }

            if (desktopPaused)
            {
                desktopPaused = false;
                StatusChanged?.Invoke(
                    "Escritorio disponible; reanudando captura");
            }

            try
            {
                var frame = _captureService.Capture(
                    jpegQuality: 40,
                    maxWidth: 1440);

                if (frame.Data.Length == 0)
                {
                    throw new InvalidOperationException(
                        "DesktopCapture devolvió un frame vacío.");
                }

                var base64 =
                    Convert.ToBase64String(frame.Data);

                await connection.InvokeAsync(
                    "PublishFrame",
                    _session.SessionId,
                    frame.Sequence,
                    frame.Width,
                    frame.Height,
                    frame.MimeType,
                    base64,
                    frame.CapturedAtUtc,
                    frame.DisplayIndex,
                    frame.DisplayCount,
                    frame.DisplayLabel,
                    cancellationToken);

                _framesPublished++;

                if (_framesPublished == 1 ||
                    _framesPublished % 30 == 0)
                {
                    StatusChanged?.Invoke(
                        $"Transmitiendo · {frame.DisplayLabel} · {_framesPublished} frames");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(
                    $"Error video: {ex.Message}");

                await DelaySafeAsync(
                    1000,
                    cancellationToken);

                continue;
            }

            await DelaySafeAsync(
                110,
                cancellationToken);
        }
    }

    private static async Task DelaySafeAsync(
        int milliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                milliseconds,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposing = true;

        if (_streamCancellation is not null)
        {
            await _streamCancellation.CancelAsync();
        }

        if (_streamTask is not null)
        {
            try
            {
                await _streamTask;
            }
            catch (OperationCanceledException)
            {
            }

            _streamTask = null;
        }

        _streamCancellation?.Dispose();
        _streamCancellation = null;

        if (_connection is not null)
        {
            try
            {
                if (_connection.State !=
                    HubConnectionState.Disconnected)
                {
                    await _connection.StopAsync();
                }
            }
            catch
            {
                // El cierre local continúa aunque SignalR ya haya caído.
            }

            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public sealed record RemoteMonitorInfo(
        int Index,
        string DeviceName,
        int Width,
        int Height,
        bool IsPrimary,
        string Label);
}