
using System.Diagnostics;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

using TitanMDM.RemoteHost.Capture;
using TitanMDM.RemoteHost.Input;
using TitanMDM.RemoteHost.Interop;
using TitanMDM.RemoteHost.Models;

namespace TitanMDM.RemoteHost.Transport;

public sealed class RemoteTransportClient : IAsyncDisposable
{
    private const int TargetFrameIntervalMilliseconds = 60;
    private const int DisconnectedRetryMilliseconds = 250;
    private const int ProtectedDesktopRetryMilliseconds = 500;
    private const int CaptureFailureRetryMilliseconds = 1000;

    private static readonly TimeSpan FrameSendTimeout =
        TimeSpan.FromSeconds(5);

    private readonly RemoteHostSession _session;
    private readonly DesktopCaptureService _captureService;
    private readonly RemoteInputController _inputController;
    private readonly WindowsDesktopStateProbe _desktopProbe = new();

    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly object _streamSync = new();

    private HubConnection? _connection;
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;

    private long _framesPublished;
    private int _disposing;
    private int _connectionLostRaised;

    public event Action<string>? StatusChanged;
    public event Action? ConnectionLost;

    public RemoteTransportClient(RemoteHostSession session)
    {
        _session = session ??
            throw new ArgumentNullException(nameof(session));

        _captureService = new DesktopCaptureService();
        _inputController = new RemoteInputController();
    }

    public bool IsConnected =>
        Volatile.Read(ref _disposing) == 0 &&
        _connection?.State == HubConnectionState.Connected;

    private bool IsDisposing =>
        Volatile.Read(ref _disposing) != 0;

    private bool CanUseDefaultDesktop =>
        _desktopProbe.GetAvailability() ==
        DesktopAvailability.Default;

    private bool SessionExpired =>
        DateTime.UtcNow >= _session.ExpiresAtUtc;

    // ============================================================
    // CONNECT
    // ============================================================

    public async Task ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);

        try
        {
            if (IsDisposing)
            {
                throw new ObjectDisposedException(
                    nameof(RemoteTransportClient));
            }

            if (_connection is not null)
            {
                return;
            }

            if (SessionExpired)
            {
                throw new InvalidOperationException(
                    "La sesión remota ya expiró.");
            }

            var hubUrl =
                $"{_session.ServerUrl.TrimEnd('/')}/hubs/remote-support";

            var connection = new HubConnectionBuilder()
                .AddMessagePackProtocol()
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
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(20),
                        TimeSpan.FromSeconds(30)
                    })
                .Build();

            _connection = connection;

            RegisterHandlers(connection);
            RegisterConnectionEvents(connection);

            try
            {
                StatusChanged?.Invoke("Conectando");

                await connection.StartAsync(cancellationToken);

                await RegisterRemoteHostAsync(cancellationToken);

                await PublishMonitorStateAsync(cancellationToken);

                StatusChanged?.Invoke("Conectado");

                StartStreaming();
            }
            catch
            {
                _connection = null;
                await connection.DisposeAsync();
                throw;
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    // ============================================================
    // CONNECTION EVENTS
    // ============================================================

    private void RegisterConnectionEvents(HubConnection connection)
    {
        connection.Reconnecting += error =>
        {
            if (!IsDisposing)
            {
                StatusChanged?.Invoke(
                    error is null
                        ? "Reconectando SignalR"
                        : $"Reconectando SignalR: {error.Message}");
            }

            return Task.CompletedTask;
        };

        connection.Reconnected += async _ =>
        {
            if (IsDisposing)
            {
                return;
            }

            if (SessionExpired)
            {
                StatusChanged?.Invoke("Sesión remota expirada");
                RaiseConnectionLost();
                return;
            }

            try
            {
                StatusChanged?.Invoke(
                    "Restaurando registro del RemoteHost");

                // SignalR asigna un ConnectionId nuevo.
                // El RemoteHost debe reincorporarse al grupo.
                await RegisterRemoteHostAsync(CancellationToken.None);

                await PublishMonitorStateAsync(CancellationToken.None);

                if (IsDisposing)
                {
                    return;
                }

                StatusChanged?.Invoke("Conectado");

                // No se reinicia un stream que continúa activo.
                StartStreaming();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(
                    $"No fue posible recuperar la sesión: {ex.Message}");

                RaiseConnectionLost();
            }
        };

        connection.Closed += error =>
        {
            if (!IsDisposing)
            {
                StatusChanged?.Invoke(
                    error is null
                        ? "Canal SignalR cerrado"
                        : $"Canal SignalR cerrado: {error.Message}");

                RaiseConnectionLost();
            }

            return Task.CompletedTask;
        };
    }

    // ============================================================
    // HOST REGISTRATION
    // ============================================================

    private async Task RegisterRemoteHostAsync(
        CancellationToken cancellationToken)
    {
        var connection = _connection;

        if (connection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException(
                "SignalR no está conectado para registrar RemoteHost.");
        }

        await connection.InvokeAsync(
            "RegisterRemoteHost",
            _session.SessionId,
            cancellationToken);
    }

    // ============================================================
    // HOST EVENTS
    // ============================================================

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<double, double>(
            "PointerMove",
            (x, y) =>
            {
                if (!CanProcessMouse())
                {
                    return;
                }

                var bounds = _captureService.GetSelectedBounds();

                _inputController.MovePointer(x, y, bounds);
            });

        connection.On<string>(
            "PointerButton",
            action =>
            {
                if (!CanProcessMouse())
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
                if (CanProcessMouse())
                {
                    _inputController.Wheel(delta);
                }
            });

        connection.On<int, bool>(
            "Keyboard",
            (virtualKey, keyDown) =>
            {
                if (!CanProcessKeyboard())
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

        connection.On<int>(
            "SelectMonitor",
            async monitorIndex =>
            {
                var selected =
                    _captureService.SelectDisplay(monitorIndex);

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

        connection.On(
            "TerminateRemoteSession",
            () =>
            {
                StatusChanged?.Invoke(
                    "La sesión remota fue finalizada.");

                RaiseConnectionLost();
            });
    }

    private bool CanProcessMouse()
    {
        return !IsDisposing &&
               !SessionExpired &&
               _session.AllowMouse &&
               IsConnected &&
               CanUseDefaultDesktop;
    }

    private bool CanProcessKeyboard()
    {
        return !IsDisposing &&
               !SessionExpired &&
               _session.AllowKeyboard &&
               IsConnected &&
               CanUseDefaultDesktop;
    }

    // ============================================================
    // MONITOR STATE
    // ============================================================

    private async Task PublishMonitorStateAsync(
        CancellationToken cancellationToken)
    {
        var connection = _connection;

        if (IsDisposing ||
            connection?.State != HubConnectionState.Connected)
        {
            return;
        }

        var displays = _captureService.GetDisplays();
        var selected = _captureService.GetSelectedDisplay();

        var payload = displays
            .Select(display =>
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

    // ============================================================
    // SINGLE STREAM LIFECYCLE
    // ============================================================

    private void StartStreaming()
    {
        lock (_streamSync)
        {
            if (IsDisposing ||
                Volatile.Read(ref _connectionLostRaised) != 0)
            {
                return;
            }

            if (_streamTask is { IsCompleted: false })
            {
                // Ya existe un único loop activo.
                // Nunca lanzar otro en paralelo.
                return;
            }

            if (_streamTask is not null)
            {
                // El loop anterior terminó: limpiar sus recursos.
                _streamCancellation?.Dispose();
                _streamCancellation = null;
                _streamTask = null;
            }

            _streamCancellation = new CancellationTokenSource();

            var token = _streamCancellation.Token;

            _streamTask = Task.Run(
                () => StreamLoopAsync(token),
                token);
        }
    }

    // ============================================================
    // STREAM LOOP
    // ============================================================

    private async Task StreamLoopAsync(
        CancellationToken cancellationToken)
    {
        StatusChanged?.Invoke("Iniciando captura");

        var desktopPaused = false;
        var channelPaused = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (SessionExpired)
            {
                StatusChanged?.Invoke("Sesión remota expirada");
                RaiseConnectionLost();
                break;
            }

            var connection = _connection;

            if (connection?.State != HubConnectionState.Connected)
            {
                if (!channelPaused)
                {
                    StatusChanged?.Invoke(
                        "Canal temporalmente desconectado");

                    channelPaused = true;
                }

                await DelaySafeAsync(
                    DisconnectedRetryMilliseconds,
                    cancellationToken);

                continue;
            }

            if (channelPaused)
            {
                channelPaused = false;

                StatusChanged?.Invoke(
                    "Canal recuperado; reanudando transmisión");
            }

            if (!CanUseDefaultDesktop)
            {
                if (!desktopPaused)
                {
                    StatusChanged?.Invoke(
                        "Escritorio protegido; captura y entrada en pausa");

                    desktopPaused = true;
                }

                await DelaySafeAsync(
                    ProtectedDesktopRetryMilliseconds,
                    cancellationToken);

                continue;
            }

            if (desktopPaused)
            {
                desktopPaused = false;

                StatusChanged?.Invoke(
                    "Escritorio disponible; reanudando captura");
            }

            var cycleStarted = Stopwatch.GetTimestamp();

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

                using var sendCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                sendCancellation.CancelAfter(FrameSendTimeout);

                await connection.InvokeAsync(
                    "PublishFrame",
                    _session.SessionId,
                    frame.Sequence,
                    frame.Width,
                    frame.Height,
                    frame.MimeType,
                    frame.Data,
                    frame.CapturedAtUtc,
                    frame.DisplayIndex,
                    frame.DisplayCount,
                    frame.DisplayLabel,
                    sendCancellation.Token);

                var published =
                    Interlocked.Increment(ref _framesPublished);

                if (published == 1 || published % 30 == 0)
                {
                    StatusChanged?.Invoke(
                        $"Transmitiendo · {frame.DisplayLabel} · " +
                        $"{published} frames");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(
                    "Frame descartado por congestión del canal");

                await DelaySafeAsync(
                    DisconnectedRetryMilliseconds,
                    cancellationToken);

                continue;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(
                    $"Error de captura o transmisión: {ex.Message}");

                await DelaySafeAsync(
                    CaptureFailureRetryMilliseconds,
                    cancellationToken);

                continue;
            }

            var elapsed = Stopwatch.GetElapsedTime(cycleStarted);

            var delay = Math.Max(
                0,
                TargetFrameIntervalMilliseconds -
                (int)elapsed.TotalMilliseconds);

            if (delay > 0)
            {
                await DelaySafeAsync(delay, cancellationToken);
            }
        }

        StatusChanged?.Invoke("Streaming detenido");
    }

    private static async Task DelaySafeAsync(
        int milliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(milliseconds, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Cancelación normal.
        }
    }

    // ============================================================
    // CONNECTION FAILURE
    // ============================================================

    private void RaiseConnectionLost()
    {
        if (IsDisposing ||
            Interlocked.Exchange(ref _connectionLostRaised, 1) != 0)
        {
            return;
        }

        lock (_streamSync)
        {
            _streamCancellation?.Cancel();
        }

        ConnectionLost?.Invoke();
    }

    // ============================================================
    // DISPOSE
    // ============================================================

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0)
        {
            return;
        }

        Task? streamingTask;

        lock (_streamSync)
        {
            _streamCancellation?.Cancel();
            streamingTask = _streamTask;
        }

        if (streamingTask is not null)
        {
            try
            {
                await streamingTask;
            }
            catch (OperationCanceledException)
            {
                // Terminación esperada.
            }
        }

        lock (_streamSync)
        {
            _streamCancellation?.Dispose();
            _streamCancellation = null;
            _streamTask = null;
        }

        await _connectionGate.WaitAsync();

        try
        {
            var connection = _connection;
            _connection = null;

            if (connection is not null)
            {
                try
                {
                    if (connection.State !=
                        HubConnectionState.Disconnected)
                    {
                        await connection.StopAsync();
                    }
                }
                catch
                {
                    // Se continúa con DisposeAsync aunque
                    // SignalR ya esté desconectado.
                }

                await connection.DisposeAsync();
            }
        }
        finally
        {
            _connectionGate.Release();
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
