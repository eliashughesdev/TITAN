using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TitanMDM.WindowsAgent.Interop;

public sealed class ActiveSessionProcessLauncher
{
    /*
     * ============================================================
     * TOKEN ACCESS
     * ============================================================
     */

    private const uint TokenAssignPrimary =
        0x0001;

    private const uint TokenDuplicate =
        0x0002;

    private const uint TokenQuery =
        0x0008;

    private const uint TokenAdjustDefault =
        0x0080;

    private const uint TokenAdjustSessionId =
        0x0100;

    /*
     * ============================================================
     * PROCESS FLAGS
     * ============================================================
     */

    private const uint CreateUnicodeEnvironment =
        0x00000400;

    /*
     * ============================================================
     * TOKEN TYPES
     * ============================================================
     */

    private const int SecurityImpersonation =
        2;

    private const int TokenPrimary =
        1;

    /*
     * ============================================================
     * WIN32 ERRORS
     * ============================================================
     */

    private const int ErrorPrivilegeNotHeld =
        1314;

    private const int ErrorNoToken =
        1008;

    /*
     * ============================================================
     * PUBLIC LAUNCH
     * ============================================================
     */

    public ProcessLaunchResult Launch(
        string executablePath,
        string arguments,
        string? workingDirectory = null)
    {
        if (
            string.IsNullOrWhiteSpace(
                executablePath))
        {
            throw new ArgumentException(
                "ExecutablePath es obligatorio.",
                nameof(executablePath));
        }

        if (
            !File.Exists(
                executablePath))
        {
            throw new FileNotFoundException(
                "TitanMDM no encontró TitanMDM.RemoteHost.exe.",
                executablePath);
        }

        /*
         * Detectamos la sesión Windows activa.
         */
        var sessionId =
            WTSGetActiveConsoleSessionId();

        if (
            sessionId ==
            uint.MaxValue)
        {
            throw new InvalidOperationException(
                "Windows no tiene una sesión interactiva activa.");
        }

        /*
         * Primero intentamos el mecanismo correcto para el
         * Windows Service de producción.
         */
        try
        {
            return LaunchUsingActiveSessionToken(
                executablePath,
                arguments,
                workingDirectory,
                sessionId);
        }
        catch (
            Win32Exception ex)
            when (
                CanUseDevelopmentFallback(
                    ex))
        {
            /*
             * ====================================================
             * DEVELOPMENT FALLBACK
             * ====================================================
             *
             * Cuando TitanMDM.WindowsAgent se ejecuta mediante:
             *
             *     dotnet run
             *
             * no posee normalmente SeTcbPrivilege y
             * WTSQueryUserToken devuelve ERROR_PRIVILEGE_NOT_HELD
             * (1314).
             *
             * Como el proceso YA está dentro de la sesión
             * interactiva del desarrollador, podemos arrancar
             * RemoteHost directamente.
             *
             * Este fallback NO sustituye la ruta de producción.
             * Cuando el agente se ejecute como Windows Service
             * LocalSystem se seguirá usando CreateProcessAsUser.
             */

                      return LaunchInteractiveFallback(
                executablePath,
                arguments,
                workingDirectory,
                checked((int)sessionId));
        }
    }

    public int? GetActiveConsoleSessionId()
    {
        var activeSessionId = WTSGetActiveConsoleSessionId();

        return activeSessionId == uint.MaxValue
            ? null
            : checked((int)activeSessionId);
    }

    /*
     * ============================================================
     * PRODUCTION PATH
     * ============================================================
     */

    private static ProcessLaunchResult
        LaunchUsingActiveSessionToken(
            string executablePath,
            string arguments,
            string? workingDirectory,
            uint sessionId)
    {
        IntPtr userToken =
            IntPtr.Zero;

        IntPtr primaryToken =
            IntPtr.Zero;

        IntPtr environment =
            IntPtr.Zero;

        try
        {
            /*
             * Obtiene el token del usuario conectado a la
             * sesión física/interactiva.
             */
            if (
                !WTSQueryUserToken(
                    sessionId,
                    out userToken))
            {
                ThrowWin32(
                    "No fue posible obtener el token de la sesión Windows activa.");
            }

            var desiredAccess =
                TokenAssignPrimary
                |
                TokenDuplicate
                |
                TokenQuery
                |
                TokenAdjustDefault
                |
                TokenAdjustSessionId;

            /*
             * Convertimos el token en Primary Token para poder
             * utilizar CreateProcessAsUser.
             */
            if (
                !DuplicateTokenEx(
                    userToken,
                    desiredAccess,
                    IntPtr.Zero,
                    SecurityImpersonation,
                    TokenPrimary,
                    out primaryToken))
            {
                ThrowWin32(
                    "No fue posible duplicar el token del usuario.");
            }

            /*
             * Crear variables de entorno del usuario interactivo.
             */
            if (
                !CreateEnvironmentBlock(
                    out environment,
                    primaryToken,
                    false))
            {
                ThrowWin32(
                    "No fue posible crear el entorno del usuario.");
            }

            var startupInfo =
                new StartupInfo
                {
                    Cb =
                        Marshal.SizeOf<
                            StartupInfo>(),

                    Desktop =
                        @"winsta0\default"
                };

            var commandLine =
                BuildCommandLine(
                    executablePath,
                    arguments);

            var currentDirectory =
                ResolveWorkingDirectory(
                    executablePath,
                    workingDirectory);

            /*
             * Iniciar RemoteHost dentro de la sesión del usuario,
             * no dentro de Session 0.
             */
            if (
                !CreateProcessAsUser(
                    primaryToken,
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CreateUnicodeEnvironment,
                    environment,
                    currentDirectory,
                    ref startupInfo,
                    out var processInformation))
            {
                ThrowWin32(
                    "Windows no pudo iniciar TitanMDM RemoteHost en la sesión interactiva.");
            }

            try
            {
                return new ProcessLaunchResult(
                    ProcessId:
                        checked(
                            (int)
                            processInformation
                                .ProcessId),

                    WindowsSessionId:
                        checked(
                            (int)
                            sessionId),

                    LaunchMode:
                        "CreateProcessAsUser");
            }
            finally
            {
                CloseHandleSafe(
                    processInformation.Thread);

                CloseHandleSafe(
                    processInformation.Process);
            }
        }
        finally
        {
            if (
                environment !=
                IntPtr.Zero)
            {
                DestroyEnvironmentBlock(
                    environment);
            }

            CloseHandleSafe(
                primaryToken);

            CloseHandleSafe(
                userToken);
        }
    }

    /*
     * ============================================================
     * DEVELOPMENT FALLBACK
     * ============================================================
     */

    private static ProcessLaunchResult
        LaunchInteractiveFallback(
            string executablePath,
            string arguments,
            string? workingDirectory,
            int windowsSessionId)
    {
        /*
         * Este fallback solo se usa cuando el agente está siendo
         * ejecutado interactivamente durante desarrollo.
         *
         * Como dotnet run ya pertenece al desktop del usuario,
         * Process.Start hereda correctamente la sesión interactiva.
         */

        var currentDirectory =
            ResolveWorkingDirectory(
                executablePath,
                workingDirectory);

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    executablePath,

                Arguments =
                    arguments
                    ??
                    string.Empty,

                WorkingDirectory =
                    currentDirectory,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    false
            };

        Process? process =
            null;

        try
        {
            process =
                Process.Start(
                    startInfo);

            if (
                process is null)
            {
                throw new InvalidOperationException(
                    "Process.Start no devolvió una instancia para TitanMDM.RemoteHost.");
            }

            return new ProcessLaunchResult(
                ProcessId:
                    process.Id,

                WindowsSessionId:
                    windowsSessionId,

                LaunchMode:
                    "InteractiveDevelopmentFallback");
        }
        finally
        {
            process?.Dispose();
        }
    }

    /*
     * ============================================================
     * FALLBACK DECISION
     * ============================================================
     */

    private static bool
        CanUseDevelopmentFallback(
            Win32Exception exception)
    {
        /*
         * 1314:
         * A required privilege is not held by the client.
         *
         * 1008:
         * An attempt was made to reference a token that does not
         * exist.
         *
         * Solo usamos fallback cuando estamos realmente en una
         * sesión interactiva.
         */

        if (
            !Environment
                .UserInteractive)
        {
            return false;
        }

        return
            exception.NativeErrorCode ==
                ErrorPrivilegeNotHeld
            ||
            exception.NativeErrorCode ==
                ErrorNoToken;
    }

    /*
     * ============================================================
     * COMMAND LINE
     * ============================================================
     */

    private static StringBuilder
        BuildCommandLine(
            string executablePath,
            string arguments)
    {
        var commandLine =
            new StringBuilder();

        commandLine.Append(
            '"');

        commandLine.Append(
            executablePath);

        commandLine.Append(
            '"');

        if (
            !string.IsNullOrWhiteSpace(
                arguments))
        {
            commandLine.Append(
                ' ');

            commandLine.Append(
                arguments);
        }

        return commandLine;
    }

    /*
     * ============================================================
     * WORKING DIRECTORY
     * ============================================================
     */

    private static string
        ResolveWorkingDirectory(
            string executablePath,
            string? workingDirectory)
    {
        if (
            !string.IsNullOrWhiteSpace(
                workingDirectory))
        {
            return workingDirectory;
        }

        var directory =
            Path.GetDirectoryName(
                executablePath);

        if (
            string.IsNullOrWhiteSpace(
                directory))
        {
            return AppContext
                .BaseDirectory;
        }

        return directory;
    }

    /*
     * ============================================================
     * WIN32 ERROR
     * ============================================================
     */

    private static void ThrowWin32(
        string message)
    {
        var error =
            Marshal.GetLastWin32Error();

        throw new Win32Exception(
            error,
            $"{message} Código Win32: {error}.");
    }

    /*
     * ============================================================
     * HANDLE CLEANUP
     * ============================================================
     */

    private static void CloseHandleSafe(
        IntPtr handle)
    {
        if (
            handle !=
            IntPtr.Zero)
        {
            CloseHandle(
                handle);
        }
    }

    /*
     * ============================================================
     * WIN32 IMPORTS
     * ============================================================
     */

    [DllImport(
        "kernel32.dll")]
    private static extern uint
        WTSGetActiveConsoleSessionId();

    [DllImport(
        "wtsapi32.dll",
        SetLastError = true)]
    private static extern bool
        WTSQueryUserToken(
            uint sessionId,
            out IntPtr token);

    [DllImport(
        "advapi32.dll",
        SetLastError = true)]
    private static extern bool
        DuplicateTokenEx(
            IntPtr existingToken,
            uint desiredAccess,
            IntPtr tokenAttributes,
            int impersonationLevel,
            int tokenType,
            out IntPtr newToken);

    [DllImport(
        "userenv.dll",
        SetLastError = true)]
    private static extern bool
        CreateEnvironmentBlock(
            out IntPtr environment,
            IntPtr token,
            bool inherit);

    [DllImport(
        "userenv.dll",
        SetLastError = true)]
    private static extern bool
        DestroyEnvironmentBlock(
            IntPtr environment);

    [DllImport(
        "advapi32.dll",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern bool
        CreateProcessAsUser(
            IntPtr token,
            string? applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string? currentDirectory,
            ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern bool
        CloseHandle(
            IntPtr handle);

    /*
     * ============================================================
     * WIN32 STRUCTURES
     * ============================================================
     */

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;

        public string? Reserved;

        public string? Desktop;

        public string? Title;

        public uint X;

        public uint Y;

        public uint XSize;

        public uint YSize;

        public uint XCountChars;

        public uint YCountChars;

        public uint FillAttribute;

        public uint Flags;

        public ushort ShowWindow;

        public ushort Reserved2;

        public IntPtr Reserved2Pointer;

        public IntPtr StdInput;

        public IntPtr StdOutput;

        public IntPtr StdError;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;

        public IntPtr Thread;

        public uint ProcessId;

        public uint ThreadId;
    }
}

/*
 * ================================================================
 * LAUNCH RESULT
 * ================================================================
 */

public sealed record ProcessLaunchResult(
    int ProcessId,
    int WindowsSessionId,
    string LaunchMode);