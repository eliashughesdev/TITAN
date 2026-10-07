using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TitanMDM.RemoteHost.Capture;

public sealed class DesktopCaptureService
{
    private readonly object
        _syncRoot =
            new();

    private long
        _sequence;

    private int
        _selectedDisplayIndex;

    public DesktopCaptureService()
    {
        var screens =
            Screen.AllScreens;

        var primaryIndex =
            Array.FindIndex(
                screens,
                x =>
                    x.Primary);

        _selectedDisplayIndex =
            primaryIndex >= 0
                ? primaryIndex
                : 0;
    }

    /*
     * ============================================================
     * MONITORS
     * ============================================================
     */

    public IReadOnlyList<DisplayDescriptor>
        GetDisplays()
    {
        lock (_syncRoot)
        {
            var screens =
                Screen.AllScreens;

            return screens
                .Select(
                    (
                        screen,
                        index) =>
                            CreateDescriptor(
                                screen,
                                index))
                .ToArray();
        }
    }

    public DisplayDescriptor
        GetSelectedDisplay()
    {
        lock (_syncRoot)
        {
            var screens =
                Screen.AllScreens;

            EnsureSelectedDisplay(
                screens);

            return CreateDescriptor(
                screens[
                    _selectedDisplayIndex],
                _selectedDisplayIndex);
        }
    }

    public Rectangle
        GetSelectedBounds()
    {
        lock (_syncRoot)
        {
            var screens =
                Screen.AllScreens;

            EnsureSelectedDisplay(
                screens);

            return screens[
                    _selectedDisplayIndex]
                .Bounds;
        }
    }

    public DisplayDescriptor
        SelectDisplay(
            int displayIndex)
    {
        lock (_syncRoot)
        {
            var screens =
                Screen.AllScreens;

            if (
                screens.Length ==
                0)
            {
                throw new InvalidOperationException(
                    "Windows no detectó monitores disponibles.");
            }

            if (
                displayIndex < 0
                ||
                displayIndex >=
                    screens.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(
                        displayIndex),
                    $"El monitor {displayIndex + 1} no existe.");
            }

            _selectedDisplayIndex =
                displayIndex;

            return CreateDescriptor(
                screens[
                    displayIndex],
                displayIndex);
        }
    }

    public DisplayDescriptor
        SelectNextDisplay()
    {
        lock (_syncRoot)
        {
            var screens =
                Screen.AllScreens;

            EnsureSelectedDisplay(
                screens);

            _selectedDisplayIndex =
                (
                    _selectedDisplayIndex +
                    1
                )
                %
                screens.Length;

            return CreateDescriptor(
                screens[
                    _selectedDisplayIndex],
                _selectedDisplayIndex);
        }
    }

    public DisplayDescriptor
        SelectPreviousDisplay()
    {
        lock (_syncRoot)
        {
            var screens =
                Screen.AllScreens;

            EnsureSelectedDisplay(
                screens);

            _selectedDisplayIndex =
                (
                    _selectedDisplayIndex -
                    1 +
                    screens.Length
                )
                %
                screens.Length;

            return CreateDescriptor(
                screens[
                    _selectedDisplayIndex],
                _selectedDisplayIndex);
        }
    }

    /*
     * ============================================================
     * CAPTURE
     * ============================================================
     */

    public DesktopCaptureResult Capture(
        int jpegQuality = 40,
        int maxWidth = 1440)
    {
        Screen screen;
        int displayIndex;
        int displayCount;

        lock (_syncRoot)
        {
            var screens =
                Screen.AllScreens;

            EnsureSelectedDisplay(
                screens);

            screen =
                screens[
                    _selectedDisplayIndex];

            displayIndex =
                _selectedDisplayIndex;

            displayCount =
                screens.Length;
        }

        var bounds =
            screen.Bounds;

        if (
            bounds.Width <= 0
            ||
            bounds.Height <= 0)
        {
            throw new InvalidOperationException(
                "El monitor posee dimensiones no válidas.");
        }

        /*
         * Captura a resolución nativa.
         */
        using var source =
            new Bitmap(
                bounds.Width,
                bounds.Height,
                PixelFormat.Format24bppRgb);

        using (
            var graphics =
                Graphics.FromImage(
                    source))
        {
            graphics.CopyFromScreen(
                bounds.Left,
                bounds.Top,
                0,
                0,
                bounds.Size,
                CopyPixelOperation.SourceCopy);

            /*
             * CopyFromScreen NO captura el cursor.
             * Lo dibujamos manualmente.
             */
            DrawCursor(
                graphics,
                bounds);
        }

        Bitmap?
            resized =
                null;

        Bitmap finalBitmap =
            source;

        try
        {
            /*
             * Reducimos resolución para mejorar FPS y ancho
             * de banda.
             */
            if (
                maxWidth > 0
                &&
                source.Width >
                    maxWidth)
            {
                var scale =
                    (double)maxWidth /
                    source.Width;

                var targetWidth =
                    maxWidth;

                var targetHeight =
                    Math.Max(
                        1,
                        (int)Math.Round(
                            source.Height *
                            scale));

                resized =
                    new Bitmap(
                        targetWidth,
                        targetHeight,
                        PixelFormat.Format24bppRgb);

                using var graphics =
                    Graphics.FromImage(
                        resized);

                graphics.CompositingMode =
                    CompositingMode.SourceCopy;

                graphics.CompositingQuality =
                    CompositingQuality.HighSpeed;

                graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;

                graphics.SmoothingMode =
                    SmoothingMode.HighSpeed;

                graphics.PixelOffsetMode =
                    PixelOffsetMode.HighSpeed;

                graphics.DrawImage(
                    source,
                    new Rectangle(
                        0,
                        0,
                        targetWidth,
                        targetHeight),
                    new Rectangle(
                        0,
                        0,
                        source.Width,
                        source.Height),
                    GraphicsUnit.Pixel);

                finalBitmap =
                    resized;
            }

            var bytes =
                EncodeJpeg(
                    finalBitmap,
                    jpegQuality);

            return new DesktopCaptureResult(
                Sequence:
                    Interlocked.Increment(
                        ref _sequence),

                Width:
                    finalBitmap.Width,

                Height:
                    finalBitmap.Height,

                MimeType:
                    "image/jpeg",

                Data:
                    bytes,

                CapturedAtUtc:
                    DateTime.UtcNow,

                DisplayIndex:
                    displayIndex,

                DisplayCount:
                    displayCount,

                DisplayLabel:
                    BuildDisplayLabel(
                        displayIndex,
                        screen));
        }
        finally
        {
            resized?.Dispose();
        }
    }

    /*
     * ============================================================
     * CURSOR
     * ============================================================
     */

    private static void DrawCursor(
        Graphics graphics,
        Rectangle screenBounds)
    {
        var cursorInfo =
            new CursorInfo
            {
                CbSize =
                    Marshal.SizeOf<
                        CursorInfo>()
            };

        if (
            !GetCursorInfo(
                ref cursorInfo))
        {
            return;
        }

        const int cursorShowing =
            0x00000001;

        if (
            (
                cursorInfo.Flags &
                cursorShowing
            )
            !=
            cursorShowing)
        {
            return;
        }

        if (
            cursorInfo.CursorHandle ==
            IntPtr.Zero)
        {
            return;
        }

        var cursorX =
            cursorInfo.ScreenPosition.X;

        var cursorY =
            cursorInfo.ScreenPosition.Y;

        /*
         * El cursor puede estar en otro monitor.
         * Solo lo dibujamos si pertenece al monitor que estamos
         * transmitiendo.
         */
        if (
            !screenBounds.Contains(
                cursorX,
                cursorY))
        {
            return;
        }

        var iconHandle =
            CopyIcon(
                cursorInfo.CursorHandle);

        if (
            iconHandle ==
            IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (
                !GetIconInfo(
                    iconHandle,
                    out var iconInfo))
            {
                return;
            }

            try
            {
                var localX =
                    cursorX -
                    screenBounds.Left -
                    (int)iconInfo.HotspotX;

                var localY =
                    cursorY -
                    screenBounds.Top -
                    (int)iconInfo.HotspotY;

                var hdc =
                    graphics.GetHdc();

                try
                {
                    DrawIconEx(
                        hdc,
                        localX,
                        localY,
                        iconHandle,
                        0,
                        0,
                        0,
                        IntPtr.Zero,
                        DrawIconNormal);
                }
                finally
                {
                    graphics.ReleaseHdc(
                        hdc);
                }
            }
            finally
            {
                if (
                    iconInfo.ColorBitmap !=
                    IntPtr.Zero)
                {
                    DeleteObject(
                        iconInfo.ColorBitmap);
                }

                if (
                    iconInfo.MaskBitmap !=
                    IntPtr.Zero)
                {
                    DeleteObject(
                        iconInfo.MaskBitmap);
                }
            }
        }
        finally
        {
            DestroyIcon(
                iconHandle);
        }
    }

    /*
     * ============================================================
     * JPEG
     * ============================================================
     */

    private static byte[] EncodeJpeg(
        Bitmap bitmap,
        int jpegQuality)
    {
        using var stream =
            new MemoryStream();

        var codec =
            ImageCodecInfo
                .GetImageEncoders()
                .FirstOrDefault(
                    x =>
                        x.FormatID ==
                        ImageFormat.Jpeg.Guid)
            ??
            throw new InvalidOperationException(
                "Windows no dispone del codificador JPEG requerido.");

        using var parameters =
            new EncoderParameters(
                1);

        parameters.Param[0] =
            new EncoderParameter(
                Encoder.Quality,
                (long)Math.Clamp(
                    jpegQuality,
                    20,
                    90));

        bitmap.Save(
            stream,
            codec,
            parameters);

        return stream.ToArray();
    }

    /*
     * ============================================================
     * DISPLAY HELPERS
     * ============================================================
     */

    private void EnsureSelectedDisplay(
        Screen[] screens)
    {
        if (
            screens.Length ==
            0)
        {
            throw new InvalidOperationException(
                "No existe una pantalla interactiva disponible.");
        }

        if (
            _selectedDisplayIndex < 0
            ||
            _selectedDisplayIndex >=
                screens.Length)
        {
            var primary =
                Array.FindIndex(
                    screens,
                    x =>
                        x.Primary);

            _selectedDisplayIndex =
                primary >= 0
                    ? primary
                    : 0;
        }
    }

    private static DisplayDescriptor
        CreateDescriptor(
            Screen screen,
            int index)
    {
        return new DisplayDescriptor(
            Index:
                index,

            DeviceName:
                screen.DeviceName,

            BoundsX:
                screen.Bounds.X,

            BoundsY:
                screen.Bounds.Y,

            Width:
                screen.Bounds.Width,

            Height:
                screen.Bounds.Height,

            IsPrimary:
                screen.Primary,

            Label:
                BuildDisplayLabel(
                    index,
                    screen));
    }

    private static string
        BuildDisplayLabel(
            int index,
            Screen screen)
    {
        return screen.Primary
            ? $"Monitor {index + 1} · Principal"
            : $"Monitor {index + 1}";
    }

    /*
     * ============================================================
     * WIN32
     * ============================================================
     */

    private const int
        DrawIconNormal =
            0x0003;

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool
        GetCursorInfo(
            ref CursorInfo cursorInfo);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern IntPtr
        CopyIcon(
            IntPtr iconHandle);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool
        GetIconInfo(
            IntPtr iconHandle,
            out IconInfo iconInfo);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool
        DrawIconEx(
            IntPtr deviceContext,
            int x,
            int y,
            IntPtr iconHandle,
            int width,
            int height,
            uint step,
            IntPtr brush,
            uint flags);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool
        DestroyIcon(
            IntPtr iconHandle);

    [DllImport(
        "gdi32.dll",
        SetLastError = true)]
    private static extern bool
        DeleteObject(
            IntPtr objectHandle);

    [StructLayout(
        LayoutKind.Sequential)]
    private struct Point
    {
        public int X;

        public int Y;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int CbSize;

        public int Flags;

        public IntPtr CursorHandle;

        public Point ScreenPosition;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(
            UnmanagedType.Bool)]
        public bool IsIcon;

        public uint HotspotX;

        public uint HotspotY;

        public IntPtr MaskBitmap;

        public IntPtr ColorBitmap;
    }
}

public sealed record DisplayDescriptor(
    int Index,
    string DeviceName,
    int BoundsX,
    int BoundsY,
    int Width,
    int Height,
    bool IsPrimary,
    string Label);

public sealed record DesktopCaptureResult(
    long Sequence,
    int Width,
    int Height,
    string MimeType,
    byte[] Data,
    DateTime CapturedAtUtc,
    int DisplayIndex,
    int DisplayCount,
    string DisplayLabel);