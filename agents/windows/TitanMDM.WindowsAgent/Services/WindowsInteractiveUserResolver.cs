
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TitanMDM.WindowsAgent.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsInteractiveUserResolver
{
    private const int WtsActive = 0;
    private const int WtsUserName = 5;
    private const int WtsDomainName = 7;
    private const uint InvalidSessionId = 0xFFFFFFFF;

    public WindowsInteractiveUser? GetActiveUser()
    {
        // Preferir la consola interactiva del equipo.
        var consoleId = WTSGetActiveConsoleSessionId();

        if (consoleId != InvalidSessionId)
        {
            var consoleUser = ReadUser(consoleId);

            if (consoleUser is not null)
                return consoleUser;
        }

        // Fallback: una sesion RDP activa.
        if (!WTSEnumerateSessionsW(
                IntPtr.Zero,
                0,
                1,
                out var sessions,
                out var count))
        {
            return null;
        }

        try
        {
            var structureSize =
                Marshal.SizeOf<WtsSessionInfo>();

            for (var i = 0; i < count; i++)
            {
                var entryPointer = IntPtr.Add(
                    sessions,
                    checked(i * structureSize));

                var entry =
                    Marshal.PtrToStructure<WtsSessionInfo>(
                        entryPointer);

                if (entry.State != WtsActive ||
                    entry.SessionId < 0 ||
                    (uint)entry.SessionId == consoleId)
                {
                    continue;
                }

                var user = ReadUser(
                    (uint)entry.SessionId);

                if (user is not null)
                    return user;
            }
        }
        finally
        {
            if (sessions != IntPtr.Zero)
                WTSFreeMemory(sessions);
        }

        return null;
    }

    private static WindowsInteractiveUser? ReadUser(
        uint sessionId)
    {
        var userName = QuerySessionString(
            sessionId,
            WtsUserName);

        if (string.IsNullOrWhiteSpace(userName))
            return null;

        var domain = QuerySessionString(
            sessionId,
            WtsDomainName);

        if (IsServiceAccount(userName, domain))
            return null;

        var qualifiedName =
            string.IsNullOrWhiteSpace(domain)
                ? userName
                : $"{domain}\\{userName}";

        return new WindowsInteractiveUser(
            SessionId: sessionId,
            UserName: userName,
            DomainName: domain,
            QualifiedName: qualifiedName,
            CollectedAtUtc: DateTime.UtcNow);
    }

    private static bool IsServiceAccount(
        string userName,
        string? domainName)
    {
        if (domainName?.Equals(
                "NT AUTHORITY",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return userName.Equals(
                   "SYSTEM",
                   StringComparison.OrdinalIgnoreCase)
               || userName.Equals(
                   "LOCAL SERVICE",
                   StringComparison.OrdinalIgnoreCase)
               || userName.Equals(
                   "NETWORK SERVICE",
                   StringComparison.OrdinalIgnoreCase)
               || userName.StartsWith(
                   "DWM-",
                   StringComparison.OrdinalIgnoreCase)
               || userName.StartsWith(
                   "UMFD-",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? QuerySessionString(
        uint sessionId,
        int infoClass)
    {
        // IMPORTANTE:
        // Invocar explicitamente la API Unicode (W).
        // La variante ANSI interpretada como UTF-16
        // produce nombres corruptos.
        if (!WTSQuerySessionInformationW(
                IntPtr.Zero,
                sessionId,
                infoClass,
                out var buffer,
                out var byteCount))
        {
            return null;
        }

        try
        {
            if (buffer == IntPtr.Zero ||
                byteCount < sizeof(char) ||
                byteCount % sizeof(char) != 0)
            {
                return null;
            }

            // WTS devuelve el tamano en bytes.
            // Limitar la lectura al buffer real.
            var charCount = byteCount / sizeof(char);

            if (charCount > 32768)
                return null;

            var value = Marshal.PtrToStringUni(
                buffer,
                charCount);

            if (value is null)
                return null;

            var terminator = value.IndexOf('\0');

            if (terminator >= 0)
                value = value[..terminator];

            value = value.Trim();

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                WTSFreeMemory(buffer);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint
        WTSGetActiveConsoleSessionId();

    [DllImport(
        "wtsapi32.dll",
        EntryPoint = "WTSEnumerateSessionsW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSEnumerateSessionsW(
        IntPtr server,
        int reserved,
        int version,
        out IntPtr sessions,
        out int count);

    [DllImport(
        "wtsapi32.dll",
        EntryPoint = "WTSQuerySessionInformationW",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool
        WTSQuerySessionInformationW(
            IntPtr server,
            uint sessionId,
            int informationClass,
            out IntPtr buffer,
            out int bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(
        IntPtr memory);

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WtsSessionInfo
    {
        public int SessionId;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? StationName;

        public int State;
    }
}

public sealed record WindowsInteractiveUser(
    uint SessionId,
    string UserName,
    string? DomainName,
    string QualifiedName,
    DateTime CollectedAtUtc);
