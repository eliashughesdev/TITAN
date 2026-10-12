
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TitanMDM.RemoteHost.Capture;

/// <summary>
/// Mantiene el contrato actual de DesktopCaptureService.
/// DXGI es opt-in y GDI permanece como respaldo.
/// </summary>
public sealed class HybridDesktopCaptureService : IDisposable
{
    private readonly DesktopCaptureService _gdi = new();
    private readonly DxgiDesktopCaptureService _dxgi = new();
    private readonly bool _preferDxgi;
    private readonly object _sync = new();

    private DateTime _nextDxgiAttemptUtc = DateTime.MinValue;
    private long _sequence;
    private bool _disposed;

    public HybridDesktopCaptureService()
    {
        _preferDxgi = string.Equals(
            Environment.GetEnvironmentVariable(
                "TITANMDM_REMOTE_CAPTURE"),
            "DXGI",
            StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<DisplayDescriptor> GetDisplays()
        => _gdi.GetDisplays();

    public DisplayDescriptor GetSelectedDisplay()
        => _gdi.GetSelectedDisplay();

    public Rectangle GetSelectedBounds()
        => _gdi.GetSelectedBounds();

    public DisplayDescriptor SelectDisplay(int index)
        => _gdi.SelectDisplay(index);

    public DisplayDescriptor SelectNextDisplay()
        => _gdi.SelectNextDisplay();

    public DisplayDescriptor SelectPreviousDisplay()
        => _gdi.SelectPreviousDisplay();

    public DesktopCaptureResult Capture(
        int jpegQuality = 40,
        int maxWidth = 1440)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_preferDxgi)
                return _gdi.Capture(jpegQuality, maxWidth);

            var display = _gdi.GetSelectedDisplay();

            // El módulo nativo solamente duplica el principal.
            if (!display.IsPrimary)
                return _gdi.Capture(jpegQuality, maxWidth);

            try
            {
                if (!_dxgi.IsAvailable)
                {
                    if (DateTime.UtcNow < _nextDxgiAttemptUtc)
                        return _gdi.Capture(jpegQuality, maxWidth);

                    if (!_dxgi.TryInitialize())
                    {
                        DelayDxgiRetry();
                        return _gdi.Capture(jpegQuality, maxWidth);
                    }
                }

                var frame = _dxgi.TryCapture(16);

                // Hasta incorporar envío diferencial, un timeout
                // se resuelve mediante el capturador de respaldo.
                if (frame is null)
                {
                    if (!_dxgi.IsAvailable)
                        DelayDxgiRetry();

                    return _gdi.Capture(jpegQuality, maxWidth);
                }

                if (frame.Width != display.Width ||
                    frame.Height != display.Height)
                {
                    // Cambio de resolución o escritorio:
                    // no presentar coordenadas incorrectas.
                    DelayDxgiRetry();
                    return _gdi.Capture(jpegQuality, maxWidth);
                }

                return EncodeFrame(
                    frame,
                    display,
                    jpegQuality,
                    maxWidth);
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is DllNotFoundException ||
                ex is EntryPointNotFoundException ||
                ex is BadImageFormatException ||
                ex is ExternalException)
            {
                DelayDxgiRetry();
                return _gdi.Capture(jpegQuality, maxWidth);
            }
        }
    }

    private void DelayDxgiRetry()
    {
        _nextDxgiAttemptUtc =
            DateTime.UtcNow.AddSeconds(10);
    }

    private DesktopCaptureResult EncodeFrame(
        DxgiFrame frame,
        DisplayDescriptor display,
        int jpegQuality,
        int maxWidth)
    {
        var required = checked(
            frame.Width * frame.Height * 4);

        if (frame.BgraPixels.Length != required)
            throw new InvalidOperationException(
                "DXGI devolvió dimensiones inconsistentes.");

        using var bitmap = new Bitmap(
            frame.Width,
            frame.Height,
            PixelFormat.Format32bppArgb);

        var rectangle = new Rectangle(
            0, 0, frame.Width, frame.Height);

        var data = bitmap.LockBits(
            rectangle,
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            int rowLength = checked(frame.Width * 4);

            for (int y = 0; y < frame.Height; y++)
            {
                // Respetar Stride, incluida su orientación.
                var row = IntPtr.Add(
                    data.Scan0,
                    checked(y * data.Stride));

                Marshal.Copy(
                    frame.BgraPixels,
                    checked(y * rowLength),
                    row,
                    rowLength);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        // Desktop Duplication entrega el cursor por separado
        // en determinados adaptadores. Lo dibujamos para que
        // el técnico mantenga visibilidad del puntero.
        using (var graphics = Graphics.FromImage(bitmap))
        {
            DrawCursor(
                graphics,
                new Rectangle(
                    display.BoundsX,
                    display.BoundsY,
                    display.Width,
                    display.Height));
        }

        Bitmap? resized = null;

        try
        {
            var output = bitmap;

            if (maxWidth > 0 && bitmap.Width > maxWidth)
            {
                var targetHeight = Math.Max(
                    1,
                    (int)Math.Round(
                        bitmap.Height *
                        (double)maxWidth / bitmap.Width));

                resized = new Bitmap(
                    maxWidth,
                    targetHeight,
                    PixelFormat.Format24bppRgb);

                using var graphics =
                    Graphics.FromImage(resized);

                graphics.InterpolationMode =
                    System.Drawing.Drawing2D
                        .InterpolationMode.HighQualityBilinear;

                graphics.DrawImage(
                    bitmap,
                    new Rectangle(
                        0, 0, maxWidth, targetHeight));

                output = resized;
            }

            using var stream = new MemoryStream();

            var codec = ImageCodecInfo
                .GetImageEncoders()
                .FirstOrDefault(x =>
                    x.FormatID == ImageFormat.Jpeg.Guid)
                ?? throw new InvalidOperationException(
                    "No se encontró codificador JPEG.");

            using var parameters =
                new EncoderParameters(1);

            parameters.Param[0] =
                new EncoderParameter(
                    Encoder.Quality,
                    (long)Math.Clamp(jpegQuality, 20, 90));

            output.Save(stream, codec, parameters);

            return new DesktopCaptureResult(
                Sequence: Interlocked.Increment(
                    ref _sequence),
                Width: output.Width,
                Height: output.Height,
                MimeType: "image/jpeg",
                Data: stream.ToArray(),
                CapturedAtUtc: frame.CapturedAtUtc,
                DisplayIndex: display.Index,
                DisplayCount: _gdi.GetDisplays().Count,
                DisplayLabel: display.Label);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static void DrawCursor(
        Graphics graphics,
        Rectangle bounds)
    {
        var info = new CursorInfo
        {
            CbSize = Marshal.SizeOf<CursorInfo>()
        };

        if (!GetCursorInfo(ref info) ||
            (info.Flags & 1) == 0 ||
            info.CursorHandle == IntPtr.Zero)
            return;

        var position = new Point(
            info.ScreenPosition.X,
            info.ScreenPosition.Y);

        if (!bounds.Contains(position))
            return;

        var copy = CopyIcon(info.CursorHandle);

        if (copy == IntPtr.Zero)
            return;

        try
        {
            if (!GetIconInfo(copy, out var iconInfo))
                return;

            try
            {
                var x = position.X - bounds.X -
                    (int)iconInfo.HotspotX;

                var y = position.Y - bounds.Y -
                    (int)iconInfo.HotspotY;

                var hdc = graphics.GetHdc();

                try
                {
                    DrawIconEx(
                        hdc, x, y, copy,
                        0, 0, 0, IntPtr.Zero, 3);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }
            finally
            {
                if (iconInfo.ColorBitmap != IntPtr.Zero)
                    DeleteObject(iconInfo.ColorBitmap);

                if (iconInfo.MaskBitmap != IntPtr.Zero)
                    DeleteObject(iconInfo.MaskBitmap);
            }
        }
        finally
        {
            DestroyIcon(copy);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _dxgi.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int CbSize;
        public int Flags;
        public IntPtr CursorHandle;
        public NativePoint ScreenPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public uint HotspotX;
        public uint HotspotY;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(
        ref CursorInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(
        IntPtr icon,
        out IconInfo info);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(
        IntPtr hdc,
        int x,
        int y,
        IntPtr icon,
        int width,
        int height,
        uint step,
        IntPtr brush,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
