using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace TitanMDM.Api.Ponches;

/// <summary>
/// Ciclo de vida del servicio Python (Ponches / Fiorella) desde el API.
///
/// - Al arrancar: si ya hay algo escuchando en el puerto de
///   <c>Ponches:BaseUrl</c> (p. ej. el stack de desarrollo), no lanza nada.
///   Si no, ejecuta <c>python -m uvicorn</c> con las variables que necesita
///   (clave de integración, clave OpenRouter de Fiorella).
/// - Si Python termina inesperadamente, lo registra y lo reinicia tras unos
///   segundos, con espera creciente si cae nada más arrancar.
/// - Al detener el API (servicio de Windows, consola o reciclado de IIS),
///   termina el árbol de procesos de Python. Además, el proceso hijo se
///   asocia a un Job Object de Windows con KILL_ON_JOB_CLOSE: si el API
///   muere de golpe, Windows termina Python y no quedan huérfanos.
/// </summary>
public sealed class PythonServiceSupervisor : BackgroundService
{
    /*
     * Secretos del API que Python no necesita: no se heredan al hijo.
     */
    private static readonly string[] PrivateVariablePrefixes =
    [
        "ConnectionStrings__",
        "Jwt__",
        "AI__",
        "AndroidManagement__",
        "DataProtection__",
        "Ponches__",
        "PythonService__",
    ];

    private static readonly TimeSpan StableUptime =
        TimeSpan.FromSeconds(60);

    private readonly PythonServiceOptions _options;
    private readonly PonchesOptions _ponches;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<PythonServiceSupervisor> _logger;

    private readonly object _sync = new();
    private Process? _process;
    private WindowsJob? _job;

    public PythonServiceSupervisor(
        IOptions<PythonServiceOptions> options,
        IOptions<PonchesOptions> ponches,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<PythonServiceSupervisor> logger)
    {
        _options = options.Value;
        _ponches = ponches.Value;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Supervisor de Python deshabilitado (PythonService:Enabled=false).");
            return;
        }

        if (!TryResolveEndpoint(out var host, out var port))
        {
            return;
        }

        var workingDirectory = ResolvePath(_environment.ContentRootPath, _options.WorkingDirectory);
        var pythonExe = ResolvePath(workingDirectory, _options.PythonExecutable);

        if (!File.Exists(pythonExe))
        {
            _logger.LogError(
                "No existe el intérprete de Python {PythonExe}. Cree el venv en {WorkingDirectory} e instale requirements.txt. Ponches y Fiorella no estarán disponibles.",
                pythonExe,
                workingDirectory);
            return;
        }

        var restartDelay = TimeSpan.FromSeconds(Math.Max(1, _options.RestartDelaySeconds));
        var maxDelay = TimeSpan.FromSeconds(Math.Max(_options.RestartDelaySeconds, _options.MaxRestartDelaySeconds));
        var delay = restartDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await IsListeningAsync(host, port, stoppingToken))
            {
                // Otro proceso (stack de desarrollo, servicio NSSM) ya lo atiende.
                _logger.LogInformation(
                    "Python ya escucha en {Host}:{Port}; no se lanza otra instancia.",
                    host,
                    port);
                await WaitWhileListeningAsync(host, port, stoppingToken);
                continue;
            }

            var startedAt = DateTime.UtcNow;
            int? exitCode;

            try
            {
                exitCode = await RunOnceAsync(pythonExe, workingDirectory, host, port, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No fue posible iniciar Python ({PythonExe}).", pythonExe);
                exitCode = null;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Caída tras un rato estable: espera corta. Caída al arrancar: creciente.
            delay = DateTime.UtcNow - startedAt >= StableUptime
                ? restartDelay
                : TimeSpan.FromTicks(Math.Min(maxDelay.Ticks, delay.Ticks * 2));

            _logger.LogWarning(
                "Python terminó inesperadamente (código {ExitCode}). Se reinicia en {Delay} s.",
                exitCode?.ToString() ?? "n/d",
                (int)delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        StopChild("detención del API");
    }

    public override void Dispose()
    {
        StopChild("liberación del supervisor");
        base.Dispose();
    }

    private async Task<int?> RunOnceAsync(
        string pythonExe,
        string workingDirectory,
        string host,
        int port,
        CancellationToken stoppingToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in new[]
                 {
                     "-m", "uvicorn", _options.AppModule,
                     "--host", host,
                     "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        ConfigureEnvironment(startInfo.Environment, port);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => LogChildLine(e.Data);
        process.ErrorDataReceived += (_, e) => LogChildLine(e.Data);

        if (!process.Start())
        {
            throw new InvalidOperationException("Process.Start devolvió false.");
        }

        lock (_sync)
        {
            _process = process;
            AttachToJob(process);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _logger.LogInformation(
            "Python iniciado (PID {Pid}) en {Host}:{Port} desde {WorkingDirectory}.",
            process.Id,
            host,
            port,
            workingDirectory);

        try
        {
            await process.WaitForExitAsync(stoppingToken);
            return process.ExitCode;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                Kill(process, "detención del API");
            }

            process.Dispose();
        }
    }

    private void ConfigureEnvironment(IDictionary<string, string?> environment, int port)
    {
        foreach (var name in environment.Keys.ToList())
        {
            if (PrivateVariablePrefixes.Any(prefix =>
                    name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                environment.Remove(name);
            }
        }

        environment["PYTHONIOENCODING"] = "utf-8";
        environment["PYTHONUNBUFFERED"] = "1";
        environment["TITAN_PONCHES_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Sin valor configurado, Python usa el de su .env.
        SetIfConfigured(environment, "TITAN_PONCHES_INTEGRATION_KEY", _ponches.IntegrationKey);
        SetIfConfigured(environment, "FIORELLA_OPENROUTER_KEY", _configuration["AI:Fiorella:OpenRouterApiKey"]);
        SetIfConfigured(environment, "OPENROUTER_BASE_URL", _configuration["AI:OpenRouter:BaseUrl"]);
        SetIfConfigured(environment, "FIORELLA_MODEL", _configuration["AI:Fiorella:Model"]);

        if (!string.IsNullOrWhiteSpace(_options.DataDirectory))
        {
            var dataDirectory = ResolvePath(_environment.ContentRootPath, _options.DataDirectory);
            Directory.CreateDirectory(dataDirectory);
            environment["PONCHES_DATA_DIR"] = dataDirectory;
        }
    }

    private static void SetIfConfigured(
        IDictionary<string, string?> environment,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            environment[name] = value.Trim();
        }
    }

    private void LogChildLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        // uvicorn escribe su log normal en stderr: el nivel sale del texto.
        if (line.Contains("ERROR", StringComparison.Ordinal) ||
            line.Contains("Traceback", StringComparison.Ordinal))
        {
            _logger.LogWarning("[python] {Line}", line);
        }
        else
        {
            _logger.LogInformation("[python] {Line}", line);
        }
    }

    private bool TryResolveEndpoint(out string host, out int port)
    {
        host = "127.0.0.1";
        port = 0;

        if (!Uri.TryCreate(_ponches.BaseUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogError("Ponches:BaseUrl no es una URL válida; no se lanza Python.");
            return false;
        }

        // Python solo debe escuchar en el propio servidor.
        if (!uri.IsLoopback)
        {
            _logger.LogInformation(
                "Ponches:BaseUrl apunta a {Host}, fuera de este servidor; el supervisor no lanza Python.",
                uri.Host);
            return false;
        }

        host = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : uri.Host;
        port = uri.Port;
        return true;
    }

    private static async Task<bool> IsListeningAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(IPAddress.Parse(host), port, timeout.Token);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task WaitWhileListeningAsync(string host, int port, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            if (!await IsListeningAsync(host, port, cancellationToken))
            {
                return;
            }
        }
    }

    private void StopChild(string reason)
    {
        Process? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        if (process is not null)
        {
            Kill(process, reason);
        }

        lock (_sync)
        {
            _job?.Dispose();
            _job = null;
        }
    }

    private void Kill(Process process, string reason)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
                _logger.LogInformation("Python (PID {Pid}) detenido por {Reason}.", process.Id, reason);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // Ya terminó.
        }
    }

    private void AttachToJob(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            _job ??= WindowsJob.Create();
            _job.Assign(process);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo asociar Python a un Job Object; si el API termina de forma abrupta podría quedar huérfano.");
        }
    }

    private static string ResolvePath(string basePath, string path)
    {
        // Admite %ProgramData% y similares en la configuración.
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(basePath, expanded));
    }

    /// <summary>Job Object con KILL_ON_JOB_CLOSE: los hijos mueren con el API.</summary>
    private sealed class WindowsJob : IDisposable
    {
        private const uint KillOnJobClose = 0x2000;
        private const int ExtendedLimitInformation = 9;

        private IntPtr _handle;

        private WindowsJob(IntPtr handle) => _handle = handle;

        public static WindowsJob Create()
        {
            var handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var info = new ExtendedLimits();
            info.Basic.LimitFlags = KillOnJobClose;

            if (!SetInformationJobObject(handle, ExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<ExtendedLimits>()))
            {
                var error = Marshal.GetLastWin32Error();
                CloseHandle(handle);
                throw new Win32Exception(error);
            }

            return new WindowsJob(handle);
        }

        public void Assign(Process process)
        {
            if (!AssignProcessToJobObject(_handle, process.Handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
