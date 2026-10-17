namespace TitanMDM.Application.Automation;

public sealed class AutomationPlatformUnavailableException
    : Exception
{
    public AutomationPlatformUnavailableException(
        string message)
        : base(message)
    {
    }
}
