namespace TitanMDM.Infrastructure.Authentication;

public sealed class AuthenticationSecurityOptions
{
    public const string SectionName =
        "AuthenticationSecurity";

    public int MaximumFailedLoginAttempts
    {
        get;
        init;
    } = 5;

    public int LockoutMinutes
    {
        get;
        init;
    } = 15;

    public int LoginRequestsPerMinute
    {
        get;
        init;
    } = 10;

    public int RefreshRequestsPerMinute
    {
        get;
        init;
    } = 30;

    public int EntraRequestsPerMinute
    {
        get;
        init;
    } = 20;
}