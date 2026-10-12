
using System;

namespace TitanMDM.RemoteHost.Transport;

/// <summary>
/// Perfil adaptativo de rendimiento del transporte JPEG.
///
/// Ajusta calidad, ancho máximo y frecuencia de captura
/// según el rendimiento observado.
///
/// Es compatible con el transporte heredado y no modifica
/// las restricciones de seguridad del escritorio Windows.
/// </summary>
public sealed class RemoteVideoPerformanceProfile
{
    private const int TargetIntervalMs = 33;
    private const int MaximumIntervalMs = 150;

    private const int MinimumQuality = 35;
    private const int MaximumQuality = 75;

    private const int MinimumCaptureWidth = 1024;
    private const int MaximumCaptureWidth = 1920;

    private int _frameIntervalMs = TargetIntervalMs;
    private int _jpegQuality = 58;
    private int _maximumWidth = 1600;

    private int _stableFrames;
    private int _consecutiveCongestion;

    public int FrameIntervalMilliseconds => _frameIntervalMs;

    public int JpegQuality => _jpegQuality;

    public int MaximumWidth => _maximumWidth;

    public int TargetFps =>
        Math.Max(1, 1000 / _frameIntervalMs);

    public void ReportFrameCompleted(TimeSpan captureAndSendTime)
    {
        _consecutiveCongestion = 0;

        var elapsedMs = captureAndSendTime.TotalMilliseconds;

        if (elapsedMs > _frameIntervalMs * 1.5)
        {
            _stableFrames = 0;
            ReduceWorkload();
            return;
        }

        if (elapsedMs <= _frameIntervalMs * 0.65)
        {
            _stableFrames++;

            if (_stableFrames >= 90)
            {
                _stableFrames = 0;
                IncreaseQuality();
            }

            return;
        }

        _stableFrames = 0;
    }

    public void ReportCongestion()
    {
        _stableFrames = 0;
        _consecutiveCongestion++;

        ReduceWorkload();

        if (_consecutiveCongestion >= 3)
        {
            _frameIntervalMs = Math.Min(
                MaximumIntervalMs,
                _frameIntervalMs + 15);
        }
    }

    public void ReportCaptureFailure()
    {
        _stableFrames = 0;

        _frameIntervalMs = Math.Min(
            MaximumIntervalMs,
            _frameIntervalMs + 10);
    }

    public void Reset()
    {
        _frameIntervalMs = TargetIntervalMs;
        _jpegQuality = 58;
        _maximumWidth = 1600;

        _stableFrames = 0;
        _consecutiveCongestion = 0;
    }

    private void ReduceWorkload()
    {
        if (_jpegQuality > MinimumQuality)
        {
            _jpegQuality = Math.Max(
                MinimumQuality,
                _jpegQuality - 5);

            return;
        }

        if (_maximumWidth > MinimumCaptureWidth)
        {
            _maximumWidth = Math.Max(
                MinimumCaptureWidth,
                _maximumWidth - 160);
        }
    }

    private void IncreaseQuality()
    {
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
