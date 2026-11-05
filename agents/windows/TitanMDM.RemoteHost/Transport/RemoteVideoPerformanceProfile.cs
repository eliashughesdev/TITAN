using System;

namespace TitanMDM.RemoteHost.Transport;

/// <summary>
/// Control adaptativo de carga del transporte remoto JPEG.
///
/// La adaptacion automatica (congestion / recuperacion) opera siempre
/// dentro de los limites del perfil elegido por el tecnico
/// (SetStreamProfile): auto, speed, balanced o quality, con resolucion
/// y FPS maximos opcionales.
/// </summary>
public sealed class RemoteVideoPerformanceProfile
{
    private const int MaximumIntervalMs = 180;

    private readonly object _sync = new();

    // Limites vigentes (dependen del perfil del tecnico).
    private int _minQuality = 38;
    private int _maxQuality = 78;
    private int _initialQuality = 58;
    private int _minWidth = 960;
    private int _maxWidth = 1920;
    private int _initialWidth = 1600;
    private int _targetIntervalMs = 33;

    private int _frameIntervalMs = 33;
    private int _jpegQuality = 58;
    private int _maximumWidth = 1600;
    private int _stableFrames;
    private int _consecutiveCongestion;
    private int _recoveryCooldown;
    private double _smoothedFrameTimeMs;

    public int FrameIntervalMilliseconds { get { lock (_sync) return _frameIntervalMs; } }

    public int JpegQuality { get { lock (_sync) return _jpegQuality; } }

    public int MaximumWidth { get { lock (_sync) return _maximumWidth; } }

    public int TargetFps { get { lock (_sync) return Math.Max(1, 1000 / _frameIntervalMs); } }

    public string Preset { get; private set; } = "auto";

    /// <summary>
    /// Aplica la preferencia del tecnico. maxWidth/maxFps = 0 significa automatico.
    /// </summary>
    public void ApplyViewerPreference(string preset, int maxWidth, int maxFps)
    {
        lock (_sync)
        {
            Preset = preset;

            (_minQuality, _maxQuality, _initialQuality, _minWidth, _maxWidth, _initialWidth, _targetIntervalMs) =
                preset switch
                {
                    // Menor latencia: JPEG ligero, menor resolucion, maxima fluidez.
                    "speed" => (30, 55, 45, 800, 1366, 1280, 33),
                    // Texto nitido para soporte administrativo.
                    "quality" => (60, 90, 80, 1280, 3840, 1920, 50),
                    "balanced" => (45, 72, 60, 960, 1920, 1600, 40),
                    _ => (38, 78, 58, 960, 1920, 1600, 33),
                };

            if (maxWidth > 0)
            {
                _maxWidth = Math.Clamp(maxWidth, 640, 3840);
                _minWidth = Math.Min(_minWidth, _maxWidth);
                _initialWidth = Math.Min(_initialWidth, _maxWidth);
            }

            if (maxFps > 0)
            {
                _targetIntervalMs = Math.Max(_targetIntervalMs, 1000 / Math.Clamp(maxFps, 5, 60));
            }

            ResetUnsafe();
        }
    }

    public void ReportFrameCompleted(TimeSpan captureAndSendTime)
    {
        var elapsedMs = captureAndSendTime.TotalMilliseconds;

        if (!double.IsFinite(elapsedMs) || elapsedMs <= 0)
            return;

        lock (_sync)
        {
            _smoothedFrameTimeMs =
                _smoothedFrameTimeMs <= 0
                    ? elapsedMs
                    : (_smoothedFrameTimeMs * 0.85) + (elapsedMs * 0.15);

            if (_recoveryCooldown > 0)
                _recoveryCooldown--;

            // Un frame lento aislado no reduce la calidad; el promedio debe confirmarlo.
            if (elapsedMs > _frameIntervalMs * 2.5 ||
                _smoothedFrameTimeMs > _frameIntervalMs * 1.25)
            {
                _stableFrames = 0;
                _consecutiveCongestion++;

                if (_consecutiveCongestion >= 2)
                {
                    _consecutiveCongestion = 0;
                    ReduceWorkload();
                }

                return;
            }

            _consecutiveCongestion = 0;

            if (_smoothedFrameTimeMs <= _frameIntervalMs * 0.60)
            {
                _stableFrames++;

                if (_stableFrames >= 120 && _recoveryCooldown == 0)
                {
                    _stableFrames = 0;
                    IncreaseQuality();
                    _recoveryCooldown = 90;
                }

                return;
            }

            _stableFrames = 0;
        }
    }

    public void ReportCongestion()
    {
        lock (_sync)
        {
            _stableFrames = 0;
            _consecutiveCongestion = 0;
            _frameIntervalMs = Math.Min(MaximumIntervalMs, _frameIntervalMs + 20);
            ReduceWorkload();
            _recoveryCooldown = 120;
        }
    }

    public void ReportCaptureFailure()
    {
        lock (_sync)
        {
            _stableFrames = 0;
            _consecutiveCongestion = 0;
            _frameIntervalMs = Math.Min(MaximumIntervalMs, _frameIntervalMs + 15);
            _recoveryCooldown = 120;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            ResetUnsafe();
        }
    }

    private void ResetUnsafe()
    {
        _frameIntervalMs = _targetIntervalMs;
        _jpegQuality = _initialQuality;
        _maximumWidth = _initialWidth;
        _stableFrames = 0;
        _consecutiveCongestion = 0;
        _recoveryCooldown = 0;
        _smoothedFrameTimeMs = 0;
    }

    private void ReduceWorkload()
    {
        // Primero se reduce el volumen de cada frame.
        if (_jpegQuality > _minQuality)
        {
            _jpegQuality = Math.Max(_minQuality, _jpegQuality - 5);
        }
        else if (_maximumWidth > _minWidth)
        {
            _maximumWidth = Math.Max(_minWidth, _maximumWidth - 160);
        }

        // Si la presion continua, tambien la frecuencia.
        if (_smoothedFrameTimeMs > _frameIntervalMs * 1.25)
        {
            _frameIntervalMs = Math.Min(
                MaximumIntervalMs,
                Math.Max(_frameIntervalMs + 5, (int)Math.Ceiling(_smoothedFrameTimeMs)));
        }

        _recoveryCooldown = Math.Max(_recoveryCooldown, 60);
    }

    private void IncreaseQuality()
    {
        // Recuperacion gradual, evitando oscilaciones.
        if (_frameIntervalMs > _targetIntervalMs)
        {
            _frameIntervalMs = Math.Max(_targetIntervalMs, _frameIntervalMs - 5);
            return;
        }

        if (_maximumWidth < _maxWidth)
        {
            _maximumWidth = Math.Min(_maxWidth, _maximumWidth + 160);
            return;
        }

        if (_jpegQuality < _maxQuality)
        {
            _jpegQuality = Math.Min(_maxQuality, _jpegQuality + 3);
        }
    }
}
