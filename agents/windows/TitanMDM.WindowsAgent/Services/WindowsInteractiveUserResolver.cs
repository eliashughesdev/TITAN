
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
        // Priorizamos el usuario de la consola física.
        var consoleSession = WTSGetActiveConsoleSessionId();

        if (consoleSession != InvalidSessionId)
        {
            var consoleUser = ReadUser(consoleSession);

            if (consoleUser is not null)
            {
                return consoleUser;
            }
        }

        // Si el equipo se utiliza mediante RDP, buscar
        // una sesión interactiva activa alternativa.
        if (!WTSEnumerateSessions(
                IntPtr.Zero,
                0,
                1,
                out var sessionsPointer,
                out var count))
        {
            return null;
        }

        try
        {
            var size = Marshal.SizeOf<WtsSessionInfo>();

            for (var index = 0; index < count; index++)
            {
                var pointer = IntPtr.Add(
                    sessionsPointer,
                    index * size);

                var session = Marshal
                    .PtrToStructure<WtsSessionInfo>(pointer);

                if (session.State != WtsActive)
                {
                    continue;
                }

                if (session.SessionId < 0)
                {
                    continue;
                }

                var sessionId = (uint)session.SessionId;

                if (sessionId == consoleSession)
                {
                    continue;
                }

                var user = ReadUser(sessionId);

                if (user is not null)
                {
                    return user;
                }
            }
        }
        finally
        {
            WTSFreeMemory(sessionsPointer);
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
        {
            return null;
        }

        var domainName = QuerySessionString(
            sessionId,
            WtsDomainName);

        return new WindowsInteractiveUser(
            SessionId: sessionId,
            UserName: userName,
            DomainName: domainName,
            QualifiedName:
                string.IsNullOrWhiteSpace(domainName)
                    ? userName
                    : $"{domainName}\\{userName}",
            CollectedAtUtc: DateTime.UtcNow);
    }

    private static string? QuerySessionString(
        uint sessionId,
        int informationClass)
    {
        if (!WTSQuerySessionInformation(
                IntPtr.Zero,
                sessionId,
                informationClass,
                out var buffer,
                out var byteCount))
        {
            return null;
        }

        try
        {
            if (buffer == IntPtr.Zero || byteCount <= 1)
            {
                return null;
            }

            var value = Marshal.PtrToStringUni(buffer);

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                WTSFreeMemory(buffer);
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

   [DllImport(
    "wtsapi32.dll",
    EntryPoint = "WTSQuerySessionInformationW",
    CharSet = CharSet.Unicode,
    SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSEnumerateSessions(
        IntPtr server,
        int reserved,
        int version,
        out IntPtr sessions,
        out int count);

    [DllImport(
        "wtsapi32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformation(
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
