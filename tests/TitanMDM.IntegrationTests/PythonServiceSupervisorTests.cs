using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TitanMDM.Api.Ponches;

namespace TitanMDM.IntegrationTests;

/*
 * Ciclo de vida real del proceso Python supervisado por el API: arranque,
 * variables entregadas (y secretos no heredados), reinicio tras una caída
 * y cierre sin huérfanos. Usa el Python del venv de Ponches con una app
 * ASGI mínima; si el venv no existe en esta máquina, la prueba no aplica.
 */
public sealed class PythonServiceSupervisorTests : IDisposable
{
    private static readonly string VenvPython = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "integrations", "ponches", "backend", ".venv", "Scripts", "python.exe"));

    private const string LeakVariable = "ConnectionStrings__SupervisorProbe";

    private readonly string _workDir = Path.Combine(
        Path.GetTempPath(), "titan-supervisor-" + Guid.NewGuid().ToString("N"));

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public PythonServiceSupervisorTests()
    {
        Directory.CreateDirectory(_workDir);
        File.WriteAllText(
            Path.Combine(_workDir, "probe_app.py"),
            """
            import json, os

            async def app(scope, receive, send):
                if scope["type"] != "http":
                    return
                body = json.dumps({
                    "pid": os.getpid(),
                    "fiorella": os.getenv("FIORELLA_OPENROUTER_KEY"),
                    "integration": os.getenv("TITAN_PONCHES_INTEGRATION_KEY"),
                    "leak": os.getenv("ConnectionStrings__SupervisorProbe"),
                }).encode()
                await send({"type": "http.response.start", "status": 200,
                            "headers": [(b"content-type", b"application/json")]})
                await send({"type": "http.response.body", "body": body})
            """);
    }

    [Fact]
    public async Task Lanza_Python_Lo_Reinicia_Al_Caer_Y_Lo_Termina_Al_Detenerse()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(VenvPython))
        {
            return; // Sin venv de Ponches en esta máquina.
        }

        var port = FreePort();
        Environment.SetEnvironmentVariable(LeakVariable, "no-debe-llegar");

        using var supervisor = new PythonServiceSupervisor(
            Options.Create(new PythonServiceOptions
            {
                Enabled = true,
                WorkingDirectory = _workDir,
                PythonExecutable = VenvPython,
                AppModule = "probe_app:app",
                RestartDelaySeconds = 1,
                MaxRestartDelaySeconds = 2,
            }),
            Options.Create(new PonchesOptions
            {
                BaseUrl = $"http://127.0.0.1:{port}",
                IntegrationKey = "integracion-prueba",
            }),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AI:Fiorella:OpenRouterApiKey"] = "fiorella-prueba",
                })
                .Build(),
            new HostingEnvironment { ContentRootPath = _workDir },
            NullLogger<PythonServiceSupervisor>.Instance);

        try
        {
            await supervisor.StartAsync(CancellationToken.None);

            // 1. Arranca y recibe sus variables, sin secretos del API.
            var first = await WaitForProbeAsync(port, previousPid: null);
            Assert.Equal("fiorella-prueba", first.GetProperty("fiorella").GetString());
            Assert.Equal("integracion-prueba", first.GetProperty("integration").GetString());
            Assert.Equal(JsonValueKind.Null, first.GetProperty("leak").ValueKind);

            // 2. Caída inesperada: el supervisor lo vuelve a lanzar.
            var firstPid = first.GetProperty("pid").GetInt32();
            Process.GetProcessById(firstPid).Kill(entireProcessTree: true);

            var second = await WaitForProbeAsync(port, previousPid: firstPid);
            var secondPid = second.GetProperty("pid").GetInt32();
            Assert.NotEqual(firstPid, secondPid);

            // 3. Al detener el API no queda Python escuchando ni vivo.
            await supervisor.StopAsync(CancellationToken.None);

            Assert.True(await WaitUntilAsync(() => !IsListening(port), TimeSpan.FromSeconds(15)),
                "Python siguió escuchando tras detener el supervisor.");
            Assert.True(await WaitUntilAsync(() => !IsAlive(secondPid), TimeSpan.FromSeconds(15)),
                "El proceso de Python quedó huérfano.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(LeakVariable, null);
            await supervisor.StopAsync(CancellationToken.None);
        }
    }

    private async Task<JsonElement> WaitForProbeAsync(int port, int? previousPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var json = await _http.GetStringAsync($"http://127.0.0.1:{port}/");
                var probe = JsonDocument.Parse(json).RootElement.Clone();
                if (previousPid is null || probe.GetProperty("pid").GetInt32() != previousPid)
                {
                    return probe;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                // Aún no escucha.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Python no respondió a tiempo.");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(250);
        }

        return condition();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsListening(int port)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        try { Directory.Delete(_workDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
