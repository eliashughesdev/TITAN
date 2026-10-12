
using System;

namespace TitanMDM.RemoteHost.Transport;

/// <summary>
/// Control adaptativo de carga del transporte remoto JPEG.
///
/// Mantiene compatibilidad con RemoteTransportClient.
/// No modifica permisos, captura protegida ni protocolo SignalR.
/// </summary>
public sealed class RemoteVideoPerformanceProfile
{
    private const int TargetIntervalMs = 33;
    private const int MaximumIntervalMs = 180;

    private const int MinimumQuality = 38;
    private const int MaximumQuality = 78;

    private const int MinimumCaptureWidth = 960;
    private const int MaximumCaptureWidth = 1920;

    private const int InitialQuality = 58;
    private const int InitialWidth = 1600;

    private int _frameIntervalMs = TargetIntervalMs;
    private int _jpegQuality = InitialQuality;
    private int _maximumWidth = InitialWidth;

    private int _stableFrames;
    private int _consecutiveCongestion;
    private int _recoveryCooldown;

    private double _smoothedFrameTimeMs;

    public int FrameIntervalMilliseconds => _frameIntervalMs;
    public int JpegQuality => _jpegQuality;
    public int MaximumWidth => _maximumWidth;

    public int TargetFps =>
        Math.Max(1, 1000 / _frameIntervalMs);

    public void ReportFrameCompleted(TimeSpan captureAndSendTime)
    {
        var elapsedMs = captureAndSendTime.TotalMilliseconds;

        if (!double.IsFinite(elapsedMs) || elapsedMs <= 0)
            return;

        _smoothedFrameTimeMs =
            _smoothedFrameTimeMs <= 0
                ? elapsedMs
                : (_smoothedFrameTimeMs * 0.85) + (elapsedMs * 0.15);

        if (_recoveryCooldown > 0)
            _recoveryCooldown--;

        // Un frame excesivamente lento puede ser un pico aislado.
        // Reducir carga cuando el promedio también confirma presión.
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

    public void ReportCongestion()
    {
        _stableFrames = 0;
        _consecutiveCongestion = 0;

        _frameIntervalMs = Math.Min(
            MaximumIntervalMs,
            _frameIntervalMs + 20);

        ReduceWorkload();
        _recoveryCooldown = 120;
    }

    public void ReportCaptureFailure()
    {
        _stableFrames = 0;
        _consecutiveCongestion = 0;

        _frameIntervalMs = Math.Min(
            MaximumIntervalMs,
            _frameIntervalMs + 15);

        _recoveryCooldown = 120;
    }

    public void Reset()
    {
        _frameIntervalMs = TargetIntervalMs;
        _jpegQuality = InitialQuality;
        _maximumWidth = InitialWidth;

        _stableFrames = 0;
        _consecutiveCongestion = 0;
        _recoveryCooldown = 0;
        _smoothedFrameTimeMs = 0;
    }

    private void ReduceWorkload()
    {
        // Reducir primero el volumen de datos de cada frame.
        if (_jpegQuality > MinimumQuality)
        {
            _jpegQuality = Math.Max(
                MinimumQuality,
                _jpegQuality - 5);
        }
        else if (_maximumWidth > MinimumCaptureWidth)
        {
            _maximumWidth = Math.Max(
                MinimumCaptureWidth,
                _maximumWidth - 160);
        }

        // También limitar la frecuencia si continúa la presión.
        if (_smoothedFrameTimeMs > _frameIntervalMs * 1.25)
        {
            _frameIntervalMs = Math.Min(
                MaximumIntervalMs,
                Math.Max(
                    _frameIntervalMs + 5,
                    (int)Math.Ceiling(_smoothedFrameTimeMs)));
        }

        _recoveryCooldown = Math.Max(
            _recoveryCooldown,
            60);
    }

    private void IncreaseQuality()
    {
        // Recuperación gradual, evitando oscilaciones.
        if (_frameIntervalMs > TargetIntervalMs)
        {
            _frameIntervalMs = Math.Max(
                TargetIntervalMs,
                _frameIntervalMs - 5);
            return;
        }

        if (_maximumWidth < MaximumCaptureWidth)
        {
            _maximumWidth = Math.Min(
                MaximumCaptureWidth,
                _maximumWidth + 160);
            return;
        }

        if (_jpegQuality < MaximumQuality)
        {
            _jpegQuality = Math.Min(
                MaximumQuality,
                _jpegQuality + 3);
        }
    }
}
