using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TitanMDM.Infrastructure.Android;

/// <summary>
/// Low-level REST client for Google Android Management API.
///
/// Responsibilities:
/// - Enterprise signup
/// - Enterprise creation/query/update
/// - Enrollment tokens
/// - Policies
/// - Devices
/// - Device commands
/// - Applications
/// - Web tokens
/// - Web apps
/// - Long-running operations
///
/// This class intentionally exposes Google resources as JsonNode.
/// TitanMDM's domain/application layers must not depend directly
/// on Google's generated SDK models.
/// </summary>
public sealed class AndroidManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly AndroidManagementOptions _options;

    private readonly IGoogleAndroidAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<AndroidManagementClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

    public AndroidManagementClient(
    HttpClient httpClient,
    IOptions<AndroidManagementOptions> options,
    IGoogleAndroidAccessTokenProvider accessTokenProvider,
    ILogger<AndroidManagementClient> logger)
{
    _httpClient =
        httpClient ??
        throw new ArgumentNullException(nameof(httpClient));

    _options =
        options?.Value ??
        throw new ArgumentNullException(nameof(options));

    _accessTokenProvider =
        accessTokenProvider ??
        throw new ArgumentNullException(nameof(accessTokenProvider));

    _logger =
        logger ??
        throw new ArgumentNullException(nameof(logger));

    ConfigureHttpClient();
}

    // ============================================================
    // ENTERPRISE SIGNUP
    // ============================================================

    public Task<JsonNode> CreateSignupUrlAsync(
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);

        var path =
            $"signupUrls?projectId={Uri.EscapeDataString(_options.ProjectId)}" +
            $"&callbackUrl={Uri.EscapeDataString(callbackUrl)}";

        return SendAsync(
            HttpMethod.Post,
            path,
            body: null,
            cancellationToken);
    }

    public Task<JsonNode> CreateEnterpriseAsync(
        string enterpriseToken,
        string signupUrlName,
        string? enterpriseDisplayName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enterpriseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(signupUrlName);

        var path =
            $"enterprises" +
            $"?projectId={Uri.EscapeDataString(_options.ProjectId)}" +
            $"&enterpriseToken={Uri.EscapeDataString(enterpriseToken)}" +
            $"&signupUrlName={Uri.EscapeDataString(signupUrlName)}";

        JsonObject body = new();

        if (!string.IsNullOrWhiteSpace(enterpriseDisplayName))
        {
            body["enterpriseDisplayName"] =
                enterpriseDisplayName.Trim();
        }

        return SendAsync(
            HttpMethod.Post,
            path,
            body,
            cancellationToken);
    }

    // ============================================================
    // ENTERPRISE
    // ============================================================

    public Task<JsonNode> GetEnterpriseAsync(
        string enterpriseName,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);

        return SendAsync(
            HttpMethod.Get,
            enterpriseName,
            null,
            cancellationToken);
    }

    public Task<JsonNode> PatchEnterpriseAsync(
        string enterpriseName,
        JsonNode enterprise,
        string updateMask,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentNullException.ThrowIfNull(enterprise);
        ArgumentException.ThrowIfNullOrWhiteSpace(updateMask);

        var path =
            $"{enterpriseName}" +
            $"?updateMask={Uri.EscapeDataString(updateMask)}";

        return SendAsync(
            HttpMethod.Patch,
            path,
            enterprise,
            cancellationToken);
    }

    // ============================================================
    // ENROLLMENT TOKENS
    // ============================================================

    public Task<JsonNode> CreateEnrollmentTokenAsync(
        string enterpriseName,
        JsonNode enrollmentToken,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentNullException.ThrowIfNull(enrollmentToken);

        return SendAsync(
            HttpMethod.Post,
            $"{enterpriseName}/enrollmentTokens",
            enrollmentToken,
            cancellationToken);
    }

    public Task<JsonNode> ListEnrollmentTokensAsync(
    string enterpriseName,
    int pageSize = 100,
    string? pageToken = null,
    CancellationToken cancellationToken = default)
{
    ValidateEnterpriseName(
        enterpriseName);

    if (pageSize is < 1 or > 100)
    {
        throw new ArgumentOutOfRangeException(
            nameof(pageSize),
            "Page size must be between 1 and 100.");
    }

    var path =
        $"{enterpriseName}/enrollmentTokens" +
        $"?pageSize={pageSize}";

    if (!string.IsNullOrWhiteSpace(pageToken))
    {
        path +=
            $"&pageToken={Uri.EscapeDataString(pageToken)}";
    }

    return SendAsync(
        HttpMethod.Get,
        path,
        null,
        cancellationToken);
}

    public Task<JsonNode> GetEnrollmentTokenAsync(
        string enterpriseName,
        string enrollmentTokenId,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            enrollmentTokenId);

        return SendAsync(
            HttpMethod.Get,
            $"{enterpriseName}/enrollmentTokens/" +
            $"{Uri.EscapeDataString(enrollmentTokenId)}",
            null,
            cancellationToken);
    }

    public Task DeleteEnrollmentTokenAsync(
        string enterpriseName,
        string enrollmentTokenId,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            enrollmentTokenId);

        return SendWithoutResponseAsync(
            HttpMethod.Delete,
            $"{enterpriseName}/enrollmentTokens/" +
            $"{Uri.EscapeDataString(enrollmentTokenId)}",
            cancellationToken);
    }

    // ============================================================
    // POLICIES
    // ============================================================

    public Task<JsonNode> GetPolicyAsync(
        string enterpriseName,
        string policyId,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);

        return SendAsync(
            HttpMethod.Get,
            BuildPolicyName(
                enterpriseName,
                policyId),
            null,
            cancellationToken);
    }

    public Task<JsonNode> UpsertPolicyAsync(
        string enterpriseName,
        string policyId,
        JsonNode policy,
        string? updateMask = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        ArgumentNullException.ThrowIfNull(policy);

        var path =
            BuildPolicyName(
                enterpriseName,
                policyId);

        if (!string.IsNullOrWhiteSpace(updateMask))
        {
            path +=
                $"?updateMask={Uri.EscapeDataString(updateMask)}";
        }

        return SendAsync(
            HttpMethod.Patch,
            path,
            policy,
            cancellationToken);
    }

    public Task DeletePolicyAsync(
        string enterpriseName,
        string policyId,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);

        return SendWithoutResponseAsync(
            HttpMethod.Delete,
            BuildPolicyName(
                enterpriseName,
                policyId),
            cancellationToken);
    }

    // ============================================================
    // DEVICES
    // ============================================================

    public Task<JsonNode> GetDeviceAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        ValidateDeviceName(deviceName);

        return SendAsync(
            HttpMethod.Get,
            deviceName,
            null,
            cancellationToken);
    }

    public Task<JsonNode> ListDevicesAsync(
        string enterpriseName,
        int pageSize = 100,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);

        if (pageSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Page size must be between 1 and 1000.");
        }

        var path =
            $"{enterpriseName}/devices" +
            $"?pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            path +=
                $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }

        return SendAsync(
            HttpMethod.Get,
            path,
            null,
            cancellationToken);
    }

    public Task<JsonNode> PatchDeviceAsync(
        string deviceName,
        JsonNode device,
        string updateMask,
        CancellationToken cancellationToken = default)
    {
        ValidateDeviceName(deviceName);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(updateMask);

        var path =
            $"{deviceName}" +
            $"?updateMask={Uri.EscapeDataString(updateMask)}";

        return SendAsync(
            HttpMethod.Patch,
            path,
            device,
            cancellationToken);
    }

    public Task DeleteDeviceAsync(
        string deviceName,
        string? wipeDataFlags = null,
        string? wipeReasonMessage = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDeviceName(deviceName);

        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(wipeDataFlags))
        {
            parameters.Add(
                $"wipeDataFlags={Uri.EscapeDataString(wipeDataFlags)}");
        }

        if (!string.IsNullOrWhiteSpace(wipeReasonMessage))
        {
            parameters.Add(
                $"wipeReasonMessage=" +
                $"{Uri.EscapeDataString(wipeReasonMessage)}");
        }

        var path = deviceName;

        if (parameters.Count > 0)
        {
            path += "?" + string.Join("&", parameters);
        }

        return SendWithoutResponseAsync(
            HttpMethod.Delete,
            path,
            cancellationToken);
    }

    // ============================================================
    // DEVICE COMMANDS
    // ============================================================

    public Task<JsonNode> IssueCommandAsync(
        string deviceName,
        JsonNode command,
        CancellationToken cancellationToken = default)
    {
        ValidateDeviceName(deviceName);
        ArgumentNullException.ThrowIfNull(command);

        return SendAsync(
            HttpMethod.Post,
            $"{deviceName}:issueCommand",
            command,
            cancellationToken);
    }

    public Task<JsonNode> LockDeviceAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        return IssueCommandAsync(
            deviceName,
            new JsonObject
            {
                ["type"] = "LOCK"
            },
            cancellationToken);
    }

    public Task<JsonNode> RebootDeviceAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        return IssueCommandAsync(
            deviceName,
            new JsonObject
            {
                ["type"] = "REBOOT"
            },
            cancellationToken);
    }

    public Task<JsonNode> ResetPasswordAsync(
        string deviceName,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        return IssueCommandAsync(
            deviceName,
            new JsonObject
            {
                ["type"] = "RESET_PASSWORD",
                ["newPassword"] = newPassword
            },
            cancellationToken);
    }

    public Task<JsonNode> StartLostModeAsync(
        string deviceName,
        JsonNode lostModeCommand,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lostModeCommand);

        return IssueCommandAsync(
            deviceName,
            lostModeCommand,
            cancellationToken);
    }

    public Task<JsonNode> StopLostModeAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        return IssueCommandAsync(
            deviceName,
            new JsonObject
            {
                ["type"] = "STOP_LOST_MODE"
            },
            cancellationToken);
    }

    public Task<JsonNode> WipeDeviceAsync(
        string deviceName,
        JsonNode? wipeOptions = null,
        CancellationToken cancellationToken = default)
    {
        var command =
            wipeOptions as JsonObject ??
            new JsonObject();

        command["type"] = "WIPE";

        return IssueCommandAsync(
            deviceName,
            command,
            cancellationToken);
    }

    // ============================================================
    // APPLICATIONS / MANAGED GOOGLE PLAY
    // ============================================================

    public Task<JsonNode> GetApplicationAsync(
        string enterpriseName,
        string packageName,
        string languageCode = "es",
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

        var path =
            $"{enterpriseName}/applications/" +
            $"{Uri.EscapeDataString(packageName)}" +
            $"?languageCode={Uri.EscapeDataString(languageCode)}";

        return SendAsync(
            HttpMethod.Get,
            path,
            null,
            cancellationToken);
    }

    // ============================================================
    // WEB TOKENS
    // ============================================================

    public Task<JsonNode> CreateWebTokenAsync(
        string enterpriseName,
        JsonNode webToken,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentNullException.ThrowIfNull(webToken);

        return SendAsync(
            HttpMethod.Post,
            $"{enterpriseName}/webTokens",
            webToken,
            cancellationToken);
    }

    // ============================================================
    // WEB APPS
    // ============================================================

    public Task<JsonNode> CreateWebAppAsync(
        string enterpriseName,
        JsonNode webApp,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentNullException.ThrowIfNull(webApp);

        return SendAsync(
            HttpMethod.Post,
            $"{enterpriseName}/webApps",
            webApp,
            cancellationToken);
    }

    public Task<JsonNode> GetWebAppAsync(
        string enterpriseName,
        string webAppId,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(webAppId);

        return SendAsync(
            HttpMethod.Get,
            $"{enterpriseName}/webApps/" +
            $"{Uri.EscapeDataString(webAppId)}",
            null,
            cancellationToken);
    }

    public Task<JsonNode> PatchWebAppAsync(
        string enterpriseName,
        string webAppId,
        JsonNode webApp,
        string updateMask,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(webAppId);
        ArgumentNullException.ThrowIfNull(webApp);
        ArgumentException.ThrowIfNullOrWhiteSpace(updateMask);

        var path =
            $"{enterpriseName}/webApps/" +
            $"{Uri.EscapeDataString(webAppId)}" +
            $"?updateMask={Uri.EscapeDataString(updateMask)}";

        return SendAsync(
            HttpMethod.Patch,
            path,
            webApp,
            cancellationToken);
    }

    public Task DeleteWebAppAsync(
        string enterpriseName,
        string webAppId,
        CancellationToken cancellationToken = default)
    {
        ValidateEnterpriseName(enterpriseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(webAppId);

        return SendWithoutResponseAsync(
            HttpMethod.Delete,
            $"{enterpriseName}/webApps/" +
            $"{Uri.EscapeDataString(webAppId)}",
            cancellationToken);
    }

    // ============================================================
    // LONG-RUNNING OPERATIONS
    // ============================================================

    public Task<JsonNode> GetOperationAsync(
        string operationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            operationName);

        if (!operationName.StartsWith(
                "operations/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Google operation name must begin with 'operations/'.",
                nameof(operationName));
        }

        return SendAsync(
            HttpMethod.Get,
            operationName,
            null,
            cancellationToken);
    }

    // ============================================================
    // GENERIC GOOGLE API PIPELINE
    // ============================================================

    private async Task<JsonNode> SendAsync(
        HttpMethod method,
        string relativePath,
        JsonNode? body,
        CancellationToken cancellationToken)
    {
        using var response =
            await SendWithRetryAsync(
                method,
                relativePath,
                body,
                cancellationToken);

        var content =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(content)
                   ?? new JsonObject();
        }
        catch (JsonException exception)
        {
            throw new AndroidManagementApiException(
                response.StatusCode,
                "Google Android Management API returned invalid JSON.",
                content,
                exception);
        }
    }

    private async Task SendWithoutResponseAsync(
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var response =
            await SendWithRetryAsync(
                method,
                relativePath,
                null,
                cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpMethod method,
        string relativePath,
        JsonNode? body,
        CancellationToken cancellationToken)
    {
        var maximumAttempts =
            Math.Max(1, _options.MaxRetryAttempts + 1);

        Exception? lastException = null;

        for (var attempt = 1;
             attempt <= maximumAttempts;
             attempt++)
        {
            try
            {
                using var request =
                    await CreateRequestAsync(
                        method,
                        relativePath,
                        body,
                        cancellationToken);

                var response =
                    await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                if (!IsTransientStatusCode(
                        response.StatusCode) ||
                    attempt >= maximumAttempts)
                {
                    await ThrowApiExceptionAsync(
                        response,
                        cancellationToken);
                }

                _logger.LogWarning(
                    "Transient Android Management API failure. " +
                    "StatusCode={StatusCode}, Attempt={Attempt}/{MaximumAttempts}, Path={Path}",
                    (int)response.StatusCode,
                    attempt,
                    maximumAttempts,
                    relativePath);

                response.Dispose();
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                lastException =
                    new TimeoutException(
                        "Android Management API request timed out.");

                if (attempt >= maximumAttempts)
                {
                    throw lastException;
                }
            }
            catch (HttpRequestException exception)
            {
                lastException = exception;

                if (attempt >= maximumAttempts)
                {
                    throw;
                }

                _logger.LogWarning(
                    exception,
                    "Network failure communicating with Android Management API. Attempt={Attempt}/{MaximumAttempts}",
                    attempt,
                    maximumAttempts);
            }

            await DelayBeforeRetryAsync(
                attempt,
                cancellationToken);
        }

        throw new InvalidOperationException(
            "Android Management API request failed.",
            lastException);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string relativePath,
        JsonNode? body,
        CancellationToken cancellationToken)
    {
        var request =
            new HttpRequestMessage(
                method,
                NormalizeRelativePath(relativePath));

        var accessToken =
            await GetAccessTokenAsync(
                cancellationToken);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        request.Headers.UserAgent.ParseAdd(
            "TitanMDM-Enterprise/1.0");

        if (body is not null)
        {
            request.Content =
                new StringContent(
                    body.ToJsonString(JsonOptions),
                    Encoding.UTF8,
                    "application/json");
        }

        return request;
    }

    // ============================================================
    // AUTHENTICATION
    // ============================================================

    private Task<string> GetAccessTokenAsync(
    CancellationToken cancellationToken)
{
    return _accessTokenProvider
        .GetAccessTokenAsync(
            cancellationToken);
}

    // ============================================================
    // HELPERS
    // ============================================================

    private void ConfigureHttpClient()
    {
        _options.Validate();

        _httpClient.BaseAddress =
            new Uri(
                EnsureTrailingSlash(
                    _options.BaseUrl),
                UriKind.Absolute);

        _httpClient.Timeout =
            TimeSpan.FromSeconds(
                _options.HttpTimeoutSeconds);

        _httpClient.DefaultRequestHeaders.Accept.Clear();

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));
    }

    private static string BuildPolicyName(
        string enterpriseName,
        string policyId)
    {
        return
            $"{enterpriseName}/policies/" +
            $"{Uri.EscapeDataString(policyId)}";
    }

    private static string NormalizeRelativePath(
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            relativePath);

        return relativePath.TrimStart('/');
    }

    private static string EnsureTrailingSlash(
        string value)
    {
        return value.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? value
            : value + "/";
    }

    private static void ValidateEnterpriseName(
        string enterpriseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            enterpriseName);

        if (!enterpriseName.StartsWith(
                "enterprises/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Enterprise name must use the format 'enterprises/{enterpriseId}'.",
                nameof(enterpriseName));
        }
    }

    private static void ValidateDeviceName(
        string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            deviceName);

        if (!deviceName.StartsWith(
                "enterprises/",
                StringComparison.OrdinalIgnoreCase) ||
            !deviceName.Contains(
                "/devices/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Device name must use the format 'enterprises/{enterpriseId}/devices/{deviceId}'.",
                nameof(deviceName));
        }
    }

    private static bool IsTransientStatusCode(
        HttpStatusCode statusCode)
    {
        return statusCode is
                   HttpStatusCode.RequestTimeout or
                   HttpStatusCode.TooManyRequests or
                   HttpStatusCode.InternalServerError or
                   HttpStatusCode.BadGateway or
                   HttpStatusCode.ServiceUnavailable or
                   HttpStatusCode.GatewayTimeout;
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        CancellationToken cancellationToken)
    {
        var exponent =
            Math.Max(0, attempt - 1);

        var seconds =
            _options.InitialRetryDelaySeconds *
            Math.Pow(2, exponent);

        seconds =
            Math.Min(seconds, 60);

        await Task.Delay(
            TimeSpan.FromSeconds(seconds),
            cancellationToken);
    }

    private static async Task ThrowApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        string message;

        try
        {
            var json =
                JsonNode.Parse(content);

            message =
                json?["error"]?["message"]?
                    .GetValue<string>()
                ?? $"Android Management API returned HTTP {(int)response.StatusCode}.";
        }
        catch
        {
            message =
                $"Android Management API returned HTTP {(int)response.StatusCode}.";
        }

        response.Dispose();

        throw new AndroidManagementApiException(
            response.StatusCode,
            message,
            content);
    }
}


// ================================================================
// API EXCEPTION
// ================================================================

// ================================================================
// API EXCEPTION
// ================================================================

// ================================================================
// API EXCEPTION
// ================================================================

public sealed class AndroidManagementApiException : Exception
{
    public AndroidManagementApiException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string? ResponseBody { get; }
}

