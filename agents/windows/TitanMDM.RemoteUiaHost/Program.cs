
using System.Text.Json;
using TitanMDM.RemoteUiaHost.Security;

namespace TitanMDM.RemoteUiaHost;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            PrintHelp();
            return 2;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "--check" => CheckEnvironment(),
                "--self-test" => RunSelfTest(),
                "--help" => PrintHelpAndReturn(),
                _ => InvalidCommand()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Error: {exception.GetType().Name}");

            return 1;
        }
    }

    private static int CheckEnvironment()
    {
        var environment =
            UiaEnvironmentInspector.Inspect();

        var readiness =
            RemoteElevationReadinessEvaluator.Evaluate(
                environment);

        var result = new
        {
            Environment = environment,
            Readiness = readiness
        };

        Console.WriteLine(
            JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        return 0;
    }

    private static int RunSelfTest()
    {
        var testCases = 0;

        // Verificamos distintas combinaciones de
        // características sin interactuar con Windows UAC.
        foreach (var uacEnabled in new[] { false, true })
        foreach (var secureDesktop in new[] { false, true })
        foreach (var desktopToggle in new[] { false, true })
        foreach (var hasUiAccess in new[] { false, true })
        foreach (var protectedPath in new[] { false, true })
        {
            var environment = new UiaEnvironmentReport(
                GeneratedAtUtc: DateTime.UtcNow,
                OperatingSystem: "Windows Test Environment",
                WindowsUacEnabled: uacEnabled,
                SecureDesktopEnabled: secureDesktop,
                ConsentPromptBehaviorAdmin: 5,
                ConsentPromptBehaviorUser: 3,
                UiAccessDesktopToggleAllowed: desktopToggle,
                SecureUiAccessPathsRequired: true,
                ExecutableInProtectedLocation: protectedPath,
                ProcessHasUiAccessToken: hasUiAccess,
                CurrentProcessElevated: false,
                PrivilegedFeaturesEnabled: false,
                Findings: Array.Empty<string>());

            var readiness =
                RemoteElevationReadinessEvaluator.Evaluate(
                    environment);

            if (readiness.CanStartInteractiveElevation)
            {
                throw new InvalidOperationException(
                    "Prueba fallida: se autorizó una elevación.");
            }

            if (!readiness.Blockers.Contains(
                    RemoteElevationBlocker.FeatureDisabled))
            {
                throw new InvalidOperationException(
                    "Prueba fallida: falta bloqueo obligatorio.");
            }

            var rejected = false;

            try
            {
                RemoteElevationReadinessEvaluator
                    .RequireAuthorizedReadiness(readiness);
            }
            catch (UnauthorizedAccessException)
            {
                rejected = true;
            }

            if (!rejected)
            {
                throw new InvalidOperationException(
                    "Prueba fallida: elevación no rechazada.");
            }

            testCases++;
        }

        Console.WriteLine(
            $"RS-H3.5 SELF-TEST OK: {testCases} escenarios validados.");

        Console.WriteLine(
            "Todas las solicitudes privilegiadas fueron rechazadas.");

        return 0;
    }

    private static int InvalidCommand()
    {
        Console.Error.WriteLine(
            "Comando no reconocido.");

        PrintHelp();

        return 2;
    }

    private static int PrintHelpAndReturn()
    {
        PrintHelp();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            "TitanMDM RemoteUiaHost - RS-H3.5");

        Console.WriteLine();
        Console.WriteLine(
            "  --check       Inspeccionar configuración UAC");

        Console.WriteLine(
            "  --self-test   Ejecutar pruebas de bloqueo");

        Console.WriteLine(
            "  --help        Mostrar opciones");

        Console.WriteLine();
        Console.WriteLine(
            "La elevación interactiva no está disponible todavía.");
    }
}
