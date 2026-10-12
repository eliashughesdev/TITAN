
using System.Runtime.InteropServices;

namespace TitanMDM.RemoteHost.Capture;

public sealed class DxgiDesktopCaptureService : IDisposable
{
    private const int CaptureOk = 0;
    private const int CaptureIdle = 1;
    private const int CaptureReset = 2;

    private readonly object _sync = new();
    private IntPtr _handle;
    private int _width;
    private int _height;
    private bool _disposed;

    public bool IsAvailable
    {
        get
        {
            lock (_sync)
                return !_disposed && _handle != IntPtr.Zero;
        }
    }

    public int Width
    {
        get
        {
            lock (_sync)
                return _width;
        }
    }

    public int Height
    {
        get
        {
            lock (_sync)
                return _height;
        }
    }

    public bool TryInitialize()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            CloseNative();

            try
            {
                _handle = TitanDxgiOpen();

                if (_handle == IntPtr.Zero)
                    return false;

                if (TitanDxgiGetSize(
                    _handle,
                    out _width,
                    out _height) != CaptureOk)
                {
                    CloseNative();
                    return false;
                }

                if (_width <= 0 || _height <= 0)
                {
                    CloseNative();
                    return false;
                }

                return true;
            }
            catch (DllNotFoundException)
            {
                CloseNative();
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                CloseNative();
                return false;
            }
            catch (BadImageFormatException)
            {
                CloseNative();
                return false;
            }
        }
    }

    /// <summary>
    /// Obtiene BGRA8 del monitor principal.
    /// Devuelve null si no hubo cambios o DXGI necesita reiniciar.
    /// </summary>
    public DxgiFrame? TryCapture(
        int timeoutMilliseconds = 16)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_handle == IntPtr.Zero)
                return null;

            var length = checked(_width * _height * 4);
            var buffer = new byte[length];

            var pinned = GCHandle.Alloc(
                buffer,
                GCHandleType.Pinned);

            int result;

            try
            {
                result = TitanDxgiGrab(
                    _handle,
                    pinned.AddrOfPinnedObject(),
                    buffer.Length,
                    (uint)Math.Clamp(
                        timeoutMilliseconds, 0, 1000));
            }
            finally
            {
                pinned.Free();
            }

            if (result == CaptureIdle)
                return null;

            if (result == CaptureReset)
            {
                CloseNative();
                return null;
            }

            if (result != CaptureOk)
            {
                throw new InvalidOperationException(
                    $"DXGI capture error: {result}");
            }

            return new DxgiFrame(
                _width,
                _height,
                buffer,
                DateTime.UtcNow);
        }
    }

    private void CloseNative()
    {
        if (_handle != IntPtr.Zero)
        {
            TitanDxgiClose(_handle);
            _handle = IntPtr.Zero;
        }

        _width = 0;
        _height = 0;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            CloseNative();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    [DllImport(
        "TitanDxgiCapture.dll",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr TitanDxgiOpen();

    [DllImport(
        "TitanDxgiCapture.dll",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int TitanDxgiGetSize(
        IntPtr handle,
        out int width,
        out int height);

    [DllImport(
        "TitanDxgiCapture.dll",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int TitanDxgiGrab(
        IntPtr handle,
        IntPtr destination,
        int capacity,
        uint timeoutMs);

    [DllImport(
        "TitanDxgiCapture.dll",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern void TitanDxgiClose(IntPtr handle);
}

public sealed record DxgiFrame(
    int Width,
    int Height,
    byte[] BgraPixels,
    DateTime CapturedAtUtc);
