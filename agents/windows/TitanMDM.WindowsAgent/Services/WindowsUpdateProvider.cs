using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Win32;

namespace TitanMDM.WindowsAgent.Services;

public sealed class WindowsUpdateProvider
{
    private readonly ILogger<WindowsUpdateProvider>
        _logger;

    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

    public WindowsUpdateProvider(
        ILogger<WindowsUpdateProvider> logger)
    {
        _logger =
            logger;
    }

    public async Task<WindowsUpdateSnapshot>
        CollectAsync(
            CancellationToken cancellationToken = default)
    {
        var serviceStatus =
            await QueryServiceAsync(
                cancellationToken);

        var history =
            await QueryUpdateHistoryAsync(
                cancellationToken);

        var available =
            await QueryAvailableUpdatesAsync(
                cancellationToken);

        return new WindowsUpdateSnapshot(
            WindowsUpdateService:
                serviceStatus,

            PendingReboot:
                DetectPendingReboot(),

            UpdateHistoryJson:
                history.Output,

            HistoryAvailable:
                history.Success,

            HistoryError:
                history.Success
                    ? null
                    : history.Error,

            AvailableUpdatesJson:
                available.Output,

            AvailableUpdatesAvailable:
                available.Success,

            AvailableUpdatesError:
                available.Success
                    ? null
                    : available.Error,

            CollectedAtUtc:
                DateTime.UtcNow);
    }

    public async Task<string>
        TriggerScanAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var system32 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.System);

        var usoClient =
            Path.Combine(
                system32,
                "UsoClient.exe");

        if (
            !File.Exists(
                usoClient))
        {
            throw new FileNotFoundException(
                "UsoClient.exe no está disponible en este equipo.",
                usoClient);
        }

        using var process =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        usoClient,

                    Arguments =
                        "StartScan",

                    UseShellExecute =
                        false,

                    CreateNoWindow =
                        true
                });

        if (process is null)
        {
            throw new InvalidOperationException(
                "No fue posible iniciar la búsqueda de actualizaciones.");
        }

        await process
            .WaitForExitAsync(
                cancellationToken);

        _logger.LogInformation(
            "TitanMDM solicitó una búsqueda de Windows Update.");

        return JsonSerializer.Serialize(
            new
            {
                action =
                    "WINDOWS_UPDATE_SCAN",

                accepted =
                    true,

                exitCode =
                    process.ExitCode,

                executedAtUtc =
                    DateTime.UtcNow
            });
    }

    public async Task<string>
        InstallAvailableAsync(
            string payloadJson,
            CancellationToken cancellationToken = default)
    {
        var request =
            ParseInstallRequest(
                payloadJson);

        /*
         * Para impedir inyección de PowerShell,
         * la selección de KB se serializa y luego
         * se envía al script codificada en Base64.
         */
        var kbJson =
            JsonSerializer.Serialize(
                request.KbArticleIds
                ??
                []);

        var kbBase64 =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    kbJson));

        var acceptEula =
            request.AcceptEula
                ? "$true"
                : "$false";

        var downloadOnly =
            request.DownloadOnly
                ? "$true"
                : "$false";

        var script =
            $$"""
            $ErrorActionPreference = 'Stop'

            $kbJson = [Text.Encoding]::UTF8.GetString(
                [Convert]::FromBase64String('{{kbBase64}}')
            )

            $requestedKb =
                @(
                    $kbJson |
                    ConvertFrom-Json
                )

            $acceptEula =
                {{acceptEula}}

            $downloadOnly =
                {{downloadOnly}}

            $session =
                New-Object -ComObject Microsoft.Update.Session

            $searcher =
                $session.CreateUpdateSearcher()

            $searchResult =
                $searcher.Search(
                    "IsInstalled=0 and Type='Software' and IsHidden=0"
                )

            $selected =
                New-Object -ComObject Microsoft.Update.UpdateColl

            foreach ($update in $searchResult.Updates) {

                $kbList =
                    @(
                        $update.KBArticleIDs |
                        ForEach-Object {
                            [string]$_
                        }
                    )

                $include =
                    $requestedKb.Count -eq 0

                if (-not $include) {
                    foreach ($kb in $requestedKb) {
                        if ($kbList -contains ([string]$kb)) {
                            $include = $true
                            break
                        }
                    }
                }

                if (-not $include) {
                    continue
                }

                if (
                    $acceptEula -and
                    -not $update.EulaAccepted
                ) {
                    try {
                        $update.AcceptEula()
                    }
                    catch {
                    }
                }

                [void]$selected.Add(
                    $update
                )
            }

            if ($selected.Count -eq 0) {

                [pscustomobject]@{
                    action = 'WINDOWS_UPDATE_INSTALL'
                    selectedCount = 0
                    downloaded = 0
                    installed = 0
                    rebootRequired = $false
                    resultCode = 0
                    message = 'No hay actualizaciones que coincidan con la selección.'
                } |
                ConvertTo-Json -Compress

                exit 0
            }

            $downloader =
                $session.CreateUpdateDownloader()

            $downloader.Updates =
                $selected

            $downloadResult =
                $downloader.Download()

            if ($downloadOnly) {

                [pscustomobject]@{
                    action = 'WINDOWS_UPDATE_DOWNLOAD'
                    selectedCount = $selected.Count
                    downloaded = $selected.Count
                    installed = 0
                    rebootRequired = $false
                    resultCode = [int]$downloadResult.ResultCode
                    message = 'Descarga finalizada.'
                } |
                ConvertTo-Json -Compress

                exit 0
            }

            $installer =
                $session.CreateUpdateInstaller()

            $installer.Updates =
                $selected

            $installResult =
                $installer.Install()

            $items =
                @()

            for (
                $index = 0;
                $index -lt $selected.Count;
                $index++
            ) {
                $update =
                    $selected.Item(
                        $index
                    )

                $result =
                    $installResult
                        .GetUpdateResult(
                            $index
                        )

                $items +=
                    [pscustomobject]@{
                        title =
                            $update.Title

                        kbArticleIds =
                            @(
                                $update.KBArticleIDs
                            )

                        resultCode =
                            [int]$result.ResultCode

                        hResult =
                            $result.HResult

                        rebootRequired =
                            $result.RebootRequired
                    }
            }

            [pscustomobject]@{
                action = 'WINDOWS_UPDATE_INSTALL'

                selectedCount =
                    $selected.Count

                downloaded =
                    $selected.Count

                installed =
                    @(
                        $items |
                        Where-Object {
                            $_.resultCode -eq 2
                            -or
                            $_.resultCode -eq 3
                        }
                    ).Count

                rebootRequired =
                    $installResult.RebootRequired

                resultCode =
                    [int]$installResult.ResultCode

                message =
                    'Instalación de Windows Update finalizada.'

                updates =
                    $items
            } |
            ConvertTo-Json -Depth 8 -Compress
            """;

        var execution =
            await ExecutePowerShellAsync(
                script,
                cancellationToken);

        if (!execution.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(
                    execution.Error)
                    ? "Windows Update devolvió un error."
                    : execution.Error);
        }

        _logger.LogInformation(
            "TitanMDM completó WINDOWS_UPDATE_INSTALL.");

        return string.IsNullOrWhiteSpace(
            execution.Output)
            ? "{}"
            : execution.Output;
    }

    private static WindowsUpdateInstallRequest
        ParseInstallRequest(
            string payloadJson)
    {
        if (
            string.IsNullOrWhiteSpace(
                payloadJson))
        {
            return new WindowsUpdateInstallRequest();
        }

        try
        {
            return JsonSerializer
                .Deserialize<
                    WindowsUpdateInstallRequest>(
                        payloadJson,
                        JsonOptions)
                ??
                new WindowsUpdateInstallRequest();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "Payload de WINDOWS_UPDATE_INSTALL inválido.");
        }
    }

    private static async Task<
        WindowsUpdateServiceSnapshot>
        QueryServiceAsync(
            CancellationToken cancellationToken)
    {
        var info =
            new ProcessStartInfo
            {
                FileName =
                    "sc.exe",

                Arguments =
                    "query wuauserv",

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true
            };

        using var process =
            new Process
            {
                StartInfo =
                    info
            };

        process.Start();

        var outputTask =
            process.StandardOutput
                .ReadToEndAsync(
                    cancellationToken);

        var errorTask =
            process.StandardError
                .ReadToEndAsync(
                    cancellationToken);

        await process
            .WaitForExitAsync(
                cancellationToken);

        var output =
            await outputTask;

        var error =
            await errorTask;

        var status =
            output.Contains(
                "RUNNING",
                StringComparison.OrdinalIgnoreCase)
                ? "Running"
                : output.Contains(
                    "STOPPED",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Stopped"
                    : "Unknown";

        return new WindowsUpdateServiceSnapshot(
            status,
            process.ExitCode == 0,
            output.Trim(),
            error.Trim());
    }

    private static async Task<
        PowerShellQueryResult>
        QueryUpdateHistoryAsync(
            CancellationToken cancellationToken)
    {
        const string script =
            """
            $ErrorActionPreference = 'Stop'

            $session =
                New-Object -ComObject Microsoft.Update.Session

            $searcher =
                $session.CreateUpdateSearcher()

            $count =
                $searcher.GetTotalHistoryCount()

            $take =
                [Math]::Min(
                    $count,
                    30
                )

            if ($take -eq 0) {
                @() |
                ConvertTo-Json -Compress

                exit 0
            }

            $searcher
                .QueryHistory(
                    0,
                    $take
                ) |
                Select-Object `
                    Title,
                    Date,
                    ResultCode,
                    HResult |
                ConvertTo-Json -Compress
            """;

        return await ExecutePowerShellAsync(
            script,
            cancellationToken);
    }

    private static async Task<
        PowerShellQueryResult>
        QueryAvailableUpdatesAsync(
            CancellationToken cancellationToken)
    {
        const string script =
            """
            $ErrorActionPreference = 'Stop'

            $session =
                New-Object -ComObject Microsoft.Update.Session

            $searcher =
                $session.CreateUpdateSearcher()

            $result =
                $searcher.Search(
                    "IsInstalled=0 and Type='Software' and IsHidden=0"
                )

            $items =
                @()

            foreach ($update in $result.Updates) {

                $items +=
                    [pscustomobject]@{
                        title =
                            $update.Title

                        kbArticleIds =
                            @(
                                $update.KBArticleIDs
                            )

                        severity =
                            $update.MsrcSeverity

                        rebootRequired =
                            $update.RebootRequired

                        isDownloaded =
                            $update.IsDownloaded

                        eulaAccepted =
                            $update.EulaAccepted

                        autoSelectOnWebSites =
                            $update.AutoSelectOnWebSites
                    }
            }

            $items |
            ConvertTo-Json -Depth 6 -Compress
            """;

        return await ExecutePowerShellAsync(
            script,
            cancellationToken);
    }

    private static async Task<
        PowerShellQueryResult>
        ExecutePowerShellAsync(
            string script,
            CancellationToken cancellationToken)
    {
        var encoded =
            Convert.ToBase64String(
                Encoding.Unicode
                    .GetBytes(
                        script));

        var info =
            new ProcessStartInfo
            {
                FileName =
                    "powershell.exe",

                Arguments =
                    "-NoLogo -NoProfile -NonInteractive "
                    +
                    $"-EncodedCommand {encoded}",

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true
            };

        using var process =
            new Process
            {
                StartInfo =
                    info
            };

        try
        {
            process.Start();

            var outputTask =
                process.StandardOutput
                    .ReadToEndAsync(
                        cancellationToken);

            var errorTask =
                process.StandardError
                    .ReadToEndAsync(
                        cancellationToken);

            await process
                .WaitForExitAsync(
                    cancellationToken);

            var output =
                (
                    await outputTask
                )
                .Trim();

            var error =
                (
                    await errorTask
                )
                .Trim();

            return new PowerShellQueryResult(
                process.ExitCode == 0,
                output,
                error);
        }
        catch (Exception ex)
        {
            return new PowerShellQueryResult(
                false,
                string.Empty,
                ex.Message);
        }
    }

    private static bool
        DetectPendingReboot()
    {
        try
        {
            using var cbs =
                Registry.LocalMachine
                    .OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");

            if (cbs is not null)
            {
                return true;
            }

            using var wu =
                Registry.LocalMachine
                    .OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");

            if (wu is not null)
            {
                return true;
            }

            using var session =
                Registry.LocalMachine
                    .OpenSubKey(
                        @"SYSTEM\CurrentControlSet\Control\Session Manager");

            return session
                ?.GetValue(
                    "PendingFileRenameOperations")
                is not null;
        }
        catch
        {
            return false;
        }
    }

    private sealed record
        PowerShellQueryResult(
            bool Success,
            string Output,
            string Error);
}

public sealed record WindowsUpdateSnapshot(
    WindowsUpdateServiceSnapshot WindowsUpdateService,

    bool PendingReboot,

    string UpdateHistoryJson,

    bool HistoryAvailable,

    string? HistoryError,

    string AvailableUpdatesJson,

    bool AvailableUpdatesAvailable,

    string? AvailableUpdatesError,

    DateTime CollectedAtUtc);

public sealed record WindowsUpdateServiceSnapshot(
    string Status,

    bool QuerySucceeded,

    string RawOutput,

    string Error);

public sealed class WindowsUpdateInstallRequest
{
    public string[]? KbArticleIds
    {
        get;
        set;
    }

    public bool AcceptEula
    {
        get;
        set;
    } = true;

    public bool DownloadOnly
    {
        get;
        set;
    }
}