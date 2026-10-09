
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace TitanMDM.RemoteHost.Interop;

public sealed class WindowsDesktopStateProbe
{
    private const int UoiName = 2;

    private const uint DesktopReadObjects = 0x0001;

    private const int ErrorAccessDenied = 5;

    public DesktopAvailability GetAvailability()
    {
        return GetState().Availability;
    }

    public WindowsDesktopState GetState()
    {
        var windowsSessionId =
            ProcessIdToSessionIdSafe();

        var desktop = OpenInputDesktop(
            0,
            false,
            DesktopReadObjects);

        if (desktop == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();

            return new WindowsDesktopState(
                Availability: DesktopAvailability.Unavailable,
                Kind: error == ErrorAccessDenied
                    ? WindowsDesktopKind.AccessDenied
                    : WindowsDesktopKind.Unavailable,
                DesktopName: null,
                WindowsSessionId: windowsSessionId,
                Win32ErrorCode: error,
                ObservedAtUtc: DateTime.UtcNow);
        }

        try
        {
            var name = new StringBuilder(256);

            if (!GetUserObjectInformation(
                    desktop,
                    UoiName,
                    name,
                    checked((uint)(name.Capacity * sizeof(char))),
                    out _))
            {
                var error = Marshal.GetLastWin32Error();

                return new WindowsDesktopState(
                    Availability: DesktopAvailability.Unavailable,
                    Kind: error == ErrorAccessDenied
                        ? WindowsDesktopKind.AccessDenied
                        : WindowsDesktopKind.Unavailable,
                    DesktopName: null,
                    WindowsSessionId: windowsSessionId,
                    Win32ErrorCode: error,
                    ObservedAtUtc: DateTime.UtcNow);
            }

            var desktopName = name.ToString();

            var kind = ClassifyDesktop(desktopName);

            var availability =
                kind == WindowsDesktopKind.Default
                    ? DesktopAvailability.Default
                    : DesktopAvailability.Other;

            return new WindowsDesktopState(
                Availability: availability,
                Kind: kind,
                DesktopName: desktopName,
                WindowsSessionId: windowsSessionId,
                Win32ErrorCode: null,
                ObservedAtUtc: DateTime.UtcNow);
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }

    private static WindowsDesktopKind ClassifyDesktop(
        string desktopName)
    {
        if (string.Equals(
                desktopName,
                "Default",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowsDesktopKind.Default;
        }

        if (string.Equals(
                desktopName,
                "Winlogon",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowsDesktopKind.Winlogon;
        }

        if (string.Equals(
                desktopName,
                "Screen-saver",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowsDesktopKind.ScreenSaver;
        }

        return WindowsDesktopKind.Other;
    }

    private static int? ProcessIdToSessionIdSafe()
    {
        var processId =
            (uint)Environment.ProcessId;

        if (!ProcessIdToSessionId(
                processId,
                out var sessionId))
        {
            return null;
        }

        return checked((int)sessionId);
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(
        uint flags,
        [MarshalAs(UnmanagedType.Bool)] bool inherit,
        uint desiredAccess);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetUserObjectInformationW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(
        IntPtr handle,
        int index,
        StringBuilder information,
        uint length,
        out uint neededLength);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(
        IntPtr desktop);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(
        uint processId,
        out uint sessionId);
}

public enum DesktopAvailability
{
    Unavailable = 0,
    Default = 1,
    Other = 2
}

public enum WindowsDesktopKind
{
    Unknown = 0,
    Default = 1,
    Winlogon = 2,
    ScreenSaver = 3,
    Other = 4,
    AccessDenied = 5,
    Unavailable = 6
}

public sealed record WindowsDesktopState(
    DesktopAvailability Availability,
    WindowsDesktopKind Kind,
    string? DesktopName,
    int? WindowsSessionId,
    int? Win32ErrorCode,
    DateTime ObservedAtUtc)
{
    public bool CanUseDefaultDesktop =>
        Availability == DesktopAvailability.Default;

    public bool RequiresProtectedDesktopHandling =>
        Kind != WindowsDesktopKind.Default;

    public string DiagnosticCode =>
        Kind switch
        {
            WindowsDesktopKind.Default =>
                "DESKTOP_DEFAULT",

            WindowsDesktopKind.Winlogon =>
                "DESKTOP_WINLOGON",

            WindowsDesktopKind.ScreenSaver =>
                "DESKTOP_SCREEN_SAVER",

            WindowsDesktopKind.AccessDenied =>
                "DESKTOP_ACCESS_DENIED",

            WindowsDesktopKind.Unavailable =>
                "DESKTOP_UNAVAILABLE",

            WindowsDesktopKind.Other =>
                "DESKTOP_OTHER",

            _ =>
                "DESKTOP_UNKNOWN"
        };
}
