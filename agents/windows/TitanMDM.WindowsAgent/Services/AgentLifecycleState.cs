namespace TitanMDM.WindowsAgent.Services;

public enum AgentLifecycleState
{
    Starting = 0,

    NotConfigured = 10,

    EnrollmentRequired = 20,

    Enrolling = 30,

    Enrolled = 40,

    TokenExpired = 50,

    TokenInvalid = 60,

    RecoveryRequired = 70,

    ServerUnavailable = 80,

    Healthy = 90,

    Stopping = 100
}