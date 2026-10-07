namespace TitanMDM.Infrastructure.Android;

public sealed class AndroidManagementOptions
{
    public const string SectionName = "AndroidManagement";

    public const string DefaultBaseUrl =
        "https://androidmanagement.googleapis.com/v1/";

    public const string DefaultScope =
        "https://www.googleapis.com/auth/androidmanagement";

    public string ProjectId { get; set; } = string.Empty;

    public string ServiceAccountEmail { get; set; } = string.Empty;

    public string CallbackUrl { get; set; } = string.Empty;

    public string FrontendSuccessUrl { get; set; } =
        "http://localhost:3020/enrollment?androidEnterprise=connected";

    public string FrontendErrorUrl { get; set; } =
        "http://localhost:3020/enrollment?androidEnterprise=error";

    public string BaseUrl { get; set; } =
        DefaultBaseUrl;

    public string ApplicationName { get; set; } =
        "TitanMDM Enterprise";

    public string Scope { get; set; } =
        DefaultScope;

    public int HttpTimeoutSeconds { get; set; } = 60;

    public int MaxRetryAttempts { get; set; } = 3;

    public int InitialRetryDelaySeconds { get; set; } = 2;

    public int DefaultEnrollmentTokenLifetimeMinutes { get; set; } = 60;

    public int MaximumEnrollmentTokenLifetimeMinutes { get; set; } = 10080;

    public bool EnableEnterpriseSignup { get; set; } = true;

    public bool EnableEnrollment { get; set; } = true;

    public bool EnablePolicyManagement { get; set; } = true;

    public bool EnableDeviceSynchronization { get; set; } = true;

    public bool EnableDeviceCommands { get; set; } = true;

    public bool EnableApplicationManagement { get; set; } = true;

    public bool EnableKioskManagement { get; set; } = true;

    public bool EnableComplianceSynchronization { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectId) &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Scope);

    public bool HasSignupCallback =>
        Uri.TryCreate(
            CallbackUrl,
            UriKind.Absolute,
            out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            throw new InvalidOperationException(
                "AndroidManagement:ProjectId is required.");

        if (!Uri.TryCreate(
                BaseUrl,
                UriKind.Absolute,
                out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "AndroidManagement:BaseUrl must be a valid HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(Scope))
            throw new InvalidOperationException(
                "AndroidManagement:Scope is required.");

        if (HttpTimeoutSeconds is < 10 or > 300)
            throw new InvalidOperationException(
                "AndroidManagement:HttpTimeoutSeconds must be between 10 and 300.");

        if (MaxRetryAttempts is < 0 or > 10)
            throw new InvalidOperationException(
                "AndroidManagement:MaxRetryAttempts must be between 0 and 10.");

        if (InitialRetryDelaySeconds is < 1 or > 60)
            throw new InvalidOperationException(
                "AndroidManagement:InitialRetryDelaySeconds must be between 1 and 60.");

        if (DefaultEnrollmentTokenLifetimeMinutes <= 0)
            throw new InvalidOperationException(
                "Default enrollment lifetime must be greater than zero.");

        if (MaximumEnrollmentTokenLifetimeMinutes <
            DefaultEnrollmentTokenLifetimeMinutes)
        {
            throw new InvalidOperationException(
                "Maximum enrollment lifetime cannot be less than default lifetime.");
        }
    }

    public void ValidateSignup()
    {
        Validate();

        if (!EnableEnterpriseSignup)
            throw new InvalidOperationException(
                "Android Enterprise signup is disabled.");

        if (!HasSignupCallback)
            throw new InvalidOperationException(
                "A public HTTPS Android Enterprise callback must be configured.");
    }
}