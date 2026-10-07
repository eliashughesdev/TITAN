namespace TitanMDM.WindowsAgent.Configuration;

public sealed class AgentOptions
{
    public const string SectionName =
        "TitanMDM";

    public string ServerUrl
    {
        get;
        set;
    } =
        "http://localhost:8020";

    public string?
        EnrollmentToken
    {
        get;
        set;
    }

    public int
        HeartbeatIntervalSeconds
    {
        get;
        set;
    } = 60;

    public int
        CommandPollingIntervalSeconds
    {
        get;
        set;
    } = 10;

    public int
        RequestTimeoutSeconds
    {
        get;
        set;
    } = 30;

    public void Apply(
        AgentRuntimeSettings settings)
    {
        ArgumentNullException
            .ThrowIfNull(
                settings);

        if (
            !string.IsNullOrWhiteSpace(
                settings.ServerUrl)
        )
        {
            ServerUrl =
                settings.ServerUrl;
        }

        if (
            !string.IsNullOrWhiteSpace(
                settings.EnrollmentToken)
        )
        {
            EnrollmentToken =
                settings
                    .EnrollmentToken;
        }

        HeartbeatIntervalSeconds =
            Math.Clamp(
                settings
                    .HeartbeatIntervalSeconds,

                15,
                3600);

        CommandPollingIntervalSeconds =
            Math.Clamp(
                settings
                    .CommandPollingIntervalSeconds,

                5,
                300);

        RequestTimeoutSeconds =
            Math.Clamp(
                settings
                    .RequestTimeoutSeconds,

                10,
                300);
    }
}