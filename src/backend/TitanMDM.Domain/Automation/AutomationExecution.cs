namespace TitanMDM.Domain.Automation;

public sealed class AutomationExecution
{
    private AutomationExecution()
    {
    }

    public AutomationExecution(
        Guid organizationId,
        Guid automationRuleId,
        Guid? deviceId,
        string triggerType,
        string triggerPayloadJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AutomationRuleId = automationRuleId;
        DeviceId = deviceId;

        TriggerType =
            string.IsNullOrWhiteSpace(triggerType)
                ? "Manual"
                : triggerType.Trim();

        TriggerPayloadJson =
            string.IsNullOrWhiteSpace(
                triggerPayloadJson)
                ? "{}"
                : triggerPayloadJson.Trim();

        Status =
            AutomationExecutionStatus.Running;

        StartedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid AutomationRuleId { get; private set; }

    public Guid? DeviceId { get; private set; }

    public string TriggerType { get; private set; } =
        string.Empty;

    public string TriggerPayloadJson
    {
        get;
        private set;
    } = "{}";

    public AutomationExecutionStatus Status
    {
        get;
        private set;
    }

    public string? ResultJson { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public void Complete(
        string? resultJson = null)
    {
        Status =
            AutomationExecutionStatus.Success;

        ResultJson =
            string.IsNullOrWhiteSpace(resultJson)
                ? "{}"
                : resultJson;

        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string error)
    {
        Status =
            AutomationExecutionStatus.Failed;

        ErrorMessage =
            string.IsNullOrWhiteSpace(error)
                ? "Automation execution failed."
                : error.Trim();

        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Skip(string reason)
    {
        Status =
            AutomationExecutionStatus.Skipped;

        ResultJson = reason;
        CompletedAtUtc = DateTime.UtcNow;
    }
}