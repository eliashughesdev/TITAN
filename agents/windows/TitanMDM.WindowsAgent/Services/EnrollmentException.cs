namespace TitanMDM.WindowsAgent.Services;

public sealed class EnrollmentException
    : Exception
{
    public EnrollmentException(
        string code,
        string message,
        int statusCode)
        : base(message)
    {
        Code =
            string.IsNullOrWhiteSpace(code)
                ? "ENROLLMENT_FAILED"
                : code.Trim();

        StatusCode =
            statusCode;
    }

    public string Code
    {
        get;
    }

    public int StatusCode
    {
        get;
    }

    public bool IsClientError =>
        StatusCode >= 400 &&
        StatusCode <= 499;

    public bool IsServerError =>
        StatusCode >= 500;

    public bool IsTokenError =>
        Code is
            "INVALID_TOKEN"
            or
            "TOKEN_REQUIRED"
            or
            "TOKEN_EXPIRED"
            or
            "TOKEN_REVOKED"
            or
            "TOKEN_EXHAUSTED"
            or
            "TOKEN_NOT_ACTIVE";

    public bool IsRecoveryError =>
        Code.StartsWith(
            "RECOVERY_",
            StringComparison.OrdinalIgnoreCase)
        ||
        Code.Equals(
            "SERIAL_ALREADY_REGISTERED",
            StringComparison.OrdinalIgnoreCase);
}