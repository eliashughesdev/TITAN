
using System.Diagnostics;
using System.Text.Json;

using TitanMDM.RemoteHost.Capture;
using TitanMDM.RemoteHost.Interop;
using TitanMDM.RemoteHost.Models;
using TitanMDM.RemoteHost.UI;

namespace TitanMDM.RemoteHost;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(x =>
            string.Equals(
                x,
                "--capture-self-test",
                StringComparison.OrdinalIgnoreCase)))
        {
            return RunCaptureSelfTest(args);
        }

        try
        {
            var session = ParseSessionAsync(args)
                .GetAwaiter()
                .GetResult();

            Application.Run(
                new RemoteSessionIndicatorForm(session));

            return 0;
        }
        catch (Exception ex)
        {
            // No mostrar token, pipe ni payload.
            MessageBox.Show(
                $"No fue posible iniciar RemoteHost: {ex.GetType().Name}",
                "TitanMDM Remote Host",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return 1;
        }
    }

    private static async Task<RemoteHostSession>
        ParseSessionAsync(string[] args)
    {
        var index = Array.FindIndex(
            args,
            item => string.Equals(
                item,
                "--bootstrap-pipe",
                StringComparison.OrdinalIgnoreCase));

        if (index < 0 || index + 1 >= args.Length)
        {
            throw new InvalidOperationException(
                "RemoteHost requiere un canal bootstrap autorizado.");
        }

        return await RemoteHostBootstrapPipeClient.ReceiveAsync(
            args[index + 1]);
    }

    private static int RunCaptureSelfTest(
        string[] args)
    {
        var outputIndex = Array.FindIndex(
            args,
            value => string.Equals(
                value,
                "--output",
                StringComparison.OrdinalIgnoreCase));

        var outputPath =
            outputIndex >= 0 &&
            outputIndex + 1 < args.Length
                ? Path.GetFullPath(args[outputIndex + 1])
                : Path.Combine(
                    Path.GetTempPath(),
                    "TitanMDM-DXGI-SelfTest.json");

        var result = new CaptureSelfTestResult
        {
            StartedAtUtc = DateTime.UtcNow,
            MachineName = Environment.MachineName
        };

        try
        {
            using var capture =
                new DxgiDesktopCaptureService();

            if (!capture.TryInitialize())
            {
                result.Status = "DXGI_UNAVAILABLE";
                result.Details =
                    "No se pudo inicializar Desktop Duplication.";

                return WriteResult(outputPath, result, 2);
            }

            result.Width = capture.Width;
            result.Height = capture.Height;

            var timer = Stopwatch.StartNew();

            var received = 0;
            var unchanged = 0;
            long totalBytes = 0;

            // Prueba limitada: máximo diez segundos.
            // Nunca guarda contenido de la pantalla.
            while (timer.Elapsed < TimeSpan.FromSeconds(10) &&
                   received < 90)
            {
                var frame = capture.TryCapture(33);

                if (frame is null)
                {
                    unchanged++;

                    if (!capture.IsAvailable)
                    {
                        result.Status = "DXGI_ACCESS_LOST";
                        result.Details =
                            "Windows invalidó la duplicación.";
                        break;
                    }

                    continue;
                }

                if (frame.Width <= 0 ||
                    frame.Height <= 0 ||
                    frame.BgraPixels.Length !=
                        checked(frame.Width * frame.Height * 4))
                {
                    throw new InvalidDataException(
                        "DXGI devolvió un frame inconsistente.");
                }

                received++;
                totalBytes += frame.BgraPixels.Length;
            }

            timer.Stop();

            result.FramesCaptured = received;
            result.IdlePolls = unchanged;
            result.TotalBytesCaptured = totalBytes;
            result.DurationMs =
                Math.Round(timer.Elapsed.TotalMilliseconds, 2);

            result.ObservedFps =
                timer.Elapsed.TotalSeconds > 0
                    ? Math.Round(
                        received / timer.Elapsed.TotalSeconds,
                        2)
                    : 0;

            if (result.Status == "DXGI_ACCESS_LOST")
                return WriteResult(outputPath, result, 3);

            if (received == 0)
            {
                result.Status = "NO_FRAMES";
                result.Details =
                    "La captura inició, pero no produjo frames. " +
                    "Comprueba sesión interactiva y actividad de pantalla.";

                return WriteResult(outputPath, result, 4);
            }

            result.Status = "PASS";
            result.Details =
                "DXGI produjo frames BGRA válidos.";

            return WriteResult(outputPath, result, 0);
        }
        catch (Exception ex)
        {
            result.Status = "ERROR";
            result.Details = ex.GetType().Name;

            return WriteResult(outputPath, result, 5);
        }
    }

    private static int WriteResult(
        string path,
        CaptureSelfTestResult result,
        int exitCode)
    {
        try
        {
            result.FinishedAtUtc = DateTime.UtcNow;

            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));

            return exitCode;
        }
        catch
        {
            return 6;
        }
    }

    private sealed class CaptureSelfTestResult
    {
        public string Status { get; set; } = "STARTED";
        public string Details { get; set; } = "";
        public string MachineName { get; set; } = "";
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int FramesCaptured { get; set; }
        public int IdlePolls { get; set; }
        public long TotalBytesCaptured { get; set; }
        public double DurationMs { get; set; }
        public double ObservedFps { get; set; }
    }
}
