using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TitanMDM.Api.Services;

public sealed class WindowsAgentDistributionService
    : IWindowsAgentDistributionService
{
    private readonly WindowsAgentDistributionOptions
        _options;

    private readonly IWebHostEnvironment
        _environment;

    public WindowsAgentDistributionService(
        IOptions<WindowsAgentDistributionOptions>
            options,
        IWebHostEnvironment environment)
    {
        _options =
            options.Value;

        _environment =
            environment;
    }

    public string GetPackagePath()
    {
        var path =
            ResolveConfiguredPath(
                _options.PackagePath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "No se encontró el paquete Windows Agent. Ejecuta Build-TitanMDMAgentPackage.ps1.",
                path);
        }

        return path;
    }

    public string GetPackageFileName()
    {
        return string.IsNullOrWhiteSpace(
            _options.PackageFileName)
            ? "TitanMDM-WindowsAgent-x64.zip"
            : _options.PackageFileName.Trim();
    }

    public async Task<string>
        GetPackageSha256Async(
            CancellationToken cancellationToken =
                default)
    {
        var path =
            GetPackagePath();

        await using var stream =
            File.OpenRead(path);

        var hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert.ToHexString(hash);
    }

    public async Task<WindowsDistributionArtifact>
        BuildIndividualInstallerAsync(
            string enrollmentToken,
            CancellationToken cancellationToken =
                default)
    {
        ValidateEnrollmentToken(
            enrollmentToken);

        var scriptsPath =
            GetScriptsPath();

        var installerScript =
            Path.Combine(
                scriptsPath,
                "Install-TitanMDMAgent.ps1");

        var setupScript =
            Path.Combine(
                scriptsPath,
                "Setup-TitanMDM.iss");

        EnsureFileExists(
            installerScript,
            "Install-TitanMDMAgent.ps1");

        EnsureFileExists(
            setupScript,
            "Setup-TitanMDM.iss");

        var compilerPath =
            GetInnoSetupCompilerPath();

        EnsureFileExists(
            compilerPath,
            "ISCC.exe");

        var temporaryRoot =
            CreateTemporaryDirectory(
                "individual");

        try
        {
            var sourceRoot =
                Path.Combine(
                    temporaryRoot,
                    "source");

            var outputRoot =
                Path.Combine(
                    temporaryRoot,
                    "output");

            Directory.CreateDirectory(
                sourceRoot);

            Directory.CreateDirectory(
                outputRoot);

            File.Copy(
                installerScript,
                Path.Combine(
                    sourceRoot,
                    "Install-TitanMDMAgent.ps1"),
                overwrite:
                    true);

            var configPath =
                Path.Combine(
                    sourceRoot,
                    "config.json");

            await WriteConfigurationAsync(
                configPath,
                enrollmentToken,
                cancellationToken);

            var outputName =
                Path.GetFileNameWithoutExtension(
                    GetIndividualInstallerFileName());

            var arguments =
                string.Join(
                    " ",
                    new[]
                    {
                        Quote(
                            $"/DSourceRoot={sourceRoot}"),

                        Quote(
                            $"/DOutputDir={outputRoot}"),

                        Quote(
                            $"/DOutputName={outputName}"),

                        Quote(
                            setupScript)
                    });

            var result =
                await RunProcessAsync(
                    compilerPath,
                    arguments,
                    cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Inno Setup no pudo generar el instalador TitanMDM."
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + result.StandardError);
            }

            var generatedInstaller =
                Path.Combine(
                    outputRoot,
                    $"{outputName}.exe");

            EnsureFileExists(
                generatedInstaller,
                "TitanMDM Agent Setup");

            var content =
                await File.ReadAllBytesAsync(
                    generatedInstaller,
                    cancellationToken);

            return new WindowsDistributionArtifact(
                content,
                GetIndividualInstallerFileName(),
                "application/vnd.microsoft.portable-executable");
        }
        finally
        {
            DeleteDirectorySafe(
                temporaryRoot);
        }
    }

    public async Task<WindowsDistributionArtifact>
        BuildGpoPackageAsync(
            string enrollmentToken,
            CancellationToken cancellationToken =
                default)
    {
        ValidateEnrollmentToken(
            enrollmentToken);

        var scriptsPath =
            GetScriptsPath();

        var mainInstaller =
            Path.Combine(
                scriptsPath,
                "Install-TitanMDMAgent.ps1");

        var gpoInstaller =
            Path.Combine(
                scriptsPath,
                "Install-TitanMDMAgent-GPO.ps1");

        var batchWrapper =
            Path.Combine(
                scriptsPath,
                "install-gpo.bat");

        EnsureFileExists(
            mainInstaller,
            "Install-TitanMDMAgent.ps1");

        EnsureFileExists(
            gpoInstaller,
            "Install-TitanMDMAgent-GPO.ps1");

        EnsureFileExists(
            batchWrapper,
            "install-gpo.bat");

        var temporaryRoot =
            CreateTemporaryDirectory(
                "gpo");

        try
        {
            File.Copy(
                mainInstaller,
                Path.Combine(
                    temporaryRoot,
                    "Install-TitanMDMAgent.ps1"),
                overwrite:
                    true);

            File.Copy(
                gpoInstaller,
                Path.Combine(
                    temporaryRoot,
                    "Install-TitanMDMAgent-GPO.ps1"),
                overwrite:
                    true);

            File.Copy(
                batchWrapper,
                Path.Combine(
                    temporaryRoot,
                    "install-gpo.bat"),
                overwrite:
                    true);

            await WriteConfigurationAsync(
                Path.Combine(
                    temporaryRoot,
                    "config.json"),
                enrollmentToken,
                cancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(
                    temporaryRoot,
                    "README.txt"),
                BuildGpoReadme(),
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false),
                cancellationToken);

            var zipPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"TitanMDM-GPO-{Guid.NewGuid():N}.zip");

            try
            {
                ZipFile.CreateFromDirectory(
                    temporaryRoot,
                    zipPath,
                    CompressionLevel.Optimal,
                    includeBaseDirectory:
                        false);

                var content =
                    await File.ReadAllBytesAsync(
                        zipPath,
                        cancellationToken);

                return new WindowsDistributionArtifact(
                    content,
                    GetGpoPackageFileName(),
                    "application/zip");
            }
            finally
            {
                DeleteFileSafe(
                    zipPath);
            }
        }
        finally
        {
            DeleteDirectorySafe(
                temporaryRoot);
        }
    }

    private async Task WriteConfigurationAsync(
        string path,
        string enrollmentToken,
        CancellationToken cancellationToken)
    {
        var serverUrl =
            NormalizeServerUrl(
                _options.PublicServerUrl);

        var packageHash =
            await GetPackageSha256Async(
                cancellationToken);

        var config =
            new
            {
                serverUrl,

                enrollmentToken,

                packageUrl =
                    $"{serverUrl}/api/enrollment/windows/package",

                packageSha256 =
                    packageHash
            };

        var json =
            JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions
                {
                    WriteIndented =
                        true,

                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase
                });

        await File.WriteAllTextAsync(
            path,
            json,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier:
                    false),
            cancellationToken);
    }

    private string BuildGpoReadme()
    {
        var serverUrl =
            NormalizeServerUrl(
                _options.PublicServerUrl);

        return $"""
TitanMDM Enterprise - Windows GPO Deployment
=============================================

Servidor TitanMDM:
{serverUrl}

CONTENIDO
---------

Install-TitanMDMAgent.ps1
    Instalador principal TitanMDM.

Install-TitanMDMAgent-GPO.ps1
    Bootstrap para ejecución mediante GPO.

install-gpo.bat
    Wrapper recomendado para Computer Startup Script.

config.json
    Configuración de servidor y credencial temporal.

INSTALACIÓN EN ACTIVE DIRECTORY
--------------------------------

1. Copia todos los archivos de este ZIP a una ubicación
   accesible mediante SYSVOL / GPO.

2. Abre Group Policy Management.

3. Crea o edita una GPO destinada a los equipos donde
   TitanMDM será instalado.

4. Navega a:

   Computer Configuration
   -> Windows Settings
   -> Scripts (Startup/Shutdown)
   -> Startup

5. Agrega:

   install-gpo.bat

6. Vincula la GPO a la OU correspondiente.

7. Los equipos ejecutarán la instalación bajo el contexto:

   NT AUTHORITY\SYSTEM

IMPORTANTE
----------

La credencial incluida es temporal y tiene el número de usos
definido al generar este paquete desde TitanMDM.

No publiques este ZIP en ubicaciones accesibles para usuarios
no autorizados.

TitanMDM no desactiva Microsoft Defender, AppLocker, WDAC,
ASR ni políticas corporativas.

Los registros de instalación se encuentran en:

C:\ProgramData\TitanMDM\logs\

Servicio:

TitanMDMWindowsAgent
""";
    }

    private string GetScriptsPath()
    {
        var path =
            ResolveConfiguredPath(
                _options.ScriptsPath);

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"No existe el directorio de scripts Windows: {path}");
        }

        return path;
    }

    private string GetInnoSetupCompilerPath()
    {
        if (
            !string.IsNullOrWhiteSpace(
                _options.InnoSetupCompilerPath)
            &&
            File.Exists(
                _options.InnoSetupCompilerPath))
        {
            return _options
                .InnoSetupCompilerPath;
        }

        var candidates =
            new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFilesX86),
                    "Inno Setup 6",
                    "ISCC.exe"),

                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFiles),
                    "Inno Setup 6",
                    "ISCC.exe")
            };

        var path =
            candidates.FirstOrDefault(
                File.Exists);

        if (path is null)
        {
            throw new FileNotFoundException(
                "No se encontró Inno Setup 6. Instala Inno Setup en el servidor TitanMDM.");
        }

        return path;
    }

    private string GetIndividualInstallerFileName()
    {
        return string.IsNullOrWhiteSpace(
            _options.IndividualInstallerFileName)
            ? "TitanMDM-Agent-Setup.exe"
            : _options.IndividualInstallerFileName.Trim();
    }

    private string GetGpoPackageFileName()
    {
        return string.IsNullOrWhiteSpace(
            _options.GpoPackageFileName)
            ? "TitanMDM-GPO.zip"
            : _options.GpoPackageFileName.Trim();
    }

    private string ResolveConfiguredPath(
        string configuredPath)
    {
        if (
            string.IsNullOrWhiteSpace(
                configuredPath))
        {
            throw new InvalidOperationException(
                "Existe una ruta WindowsAgentDistribution sin configurar.");
        }

        return Path.IsPathRooted(
            configuredPath)
            ? Path.GetFullPath(
                configuredPath)
            : Path.GetFullPath(
                Path.Combine(
                    _environment.ContentRootPath,
                    configuredPath));
    }

    private static string CreateTemporaryDirectory(
        string suffix)
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                $"TitanMDM-{suffix}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            path);

        return path;
    }

    private static async Task<ProcessResult>
        RunProcessAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    fileName,

                Arguments =
                    arguments,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true
            };

        using var process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        process.Start();

        var outputTask =
            process.StandardOutput
                .ReadToEndAsync();

        var errorTask =
            process.StandardError
                .ReadToEndAsync();

        await process.WaitForExitAsync(
            cancellationToken);

        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static string Quote(
        string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static void EnsureFileExists(
        string path,
        string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"No se encontró {description}.",
                path);
        }
    }

    private static void ValidateEnrollmentToken(
        string enrollmentToken)
    {
        if (
            string.IsNullOrWhiteSpace(
                enrollmentToken))
        {
            throw new ArgumentException(
                "EnrollmentToken es obligatorio.",
                nameof(enrollmentToken));
        }
    }

    private static string NormalizeServerUrl(
        string serverUrl)
    {
        if (
            string.IsNullOrWhiteSpace(
                serverUrl))
        {
            throw new InvalidOperationException(
                "WindowsAgentDistribution:PublicServerUrl no está configurado.");
        }

        serverUrl =
            serverUrl
                .Trim()
                .TrimEnd('/');

        if (
            !Uri.TryCreate(
                serverUrl,
                UriKind.Absolute,
                out var uri))
        {
            throw new InvalidOperationException(
                "PublicServerUrl no es una URL válida.");
        }

        if (
            uri.Scheme != Uri.UriSchemeHttp
            &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "PublicServerUrl debe utilizar HTTP o HTTPS.");
        }

        return serverUrl;
    }

    private static void DeleteDirectorySafe(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(
                    path,
                    recursive:
                        true);
            }
        }
        catch
        {
        }
    }

    private static void DeleteFileSafe(
        string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}