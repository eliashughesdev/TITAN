using System.Runtime.InteropServices;
using System.Text;

namespace TitanMDM.RemoteHost.Interop;

public sealed class WindowsDesktopStateProbe
{
    private const int UoiName = 2;
    private const uint DesktopReadObjects = 0x0001;

    public DesktopAvailability GetAvailability()
    {
        // OpenInputDesktop consulta el escritorio que recibe entrada
        // en la sesión Windows de este RemoteHost.
        var desktop = OpenInputDesktop(
            0,
            false,
            DesktopReadObjects);

        if (desktop == IntPtr.Zero)
        {
            return DesktopAvailability.Unavailable;
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
                return DesktopAvailability.Unavailable;
            }

            return string.Equals(
                name.ToString(),
                "Default",
                StringComparison.OrdinalIgnoreCase)
                    ? DesktopAvailability.Default
                    : DesktopAvailability.Other;
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr desktop);
}

public enum DesktopAvailability
{
    Unavailable = 0,
    Default = 1,
    Other = 2
}