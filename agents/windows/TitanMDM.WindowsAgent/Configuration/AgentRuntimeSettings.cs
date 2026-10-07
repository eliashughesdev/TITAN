namespace TitanMDM.WindowsAgent.Configuration;

public sealed class AgentRuntimeSettings
{
    public string ServerUrl
    {
        get;
        set;
    } = string.Empty;

    public string? EnrollmentToken
    {
        get;
        set;
    }

    public int HeartbeatIntervalSeconds
    {
        get;
        set;
    } = 60;

    public int CommandPollingIntervalSeconds
    {
        get;
        set;
    } = 10;

    public int RequestTimeoutSeconds
    {
        get;
        set;
    } = 30;
}