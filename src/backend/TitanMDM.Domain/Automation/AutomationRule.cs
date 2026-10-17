namespace TitanMDM.Domain.Automation;

public sealed class AutomationRule
{
    private AutomationRule()
    {
    }

    public AutomationRule(
        Guid organizationId,
        string name,
        string? description,
        AutomationTriggerType triggerType,
        string conditionsJson,
        AutomationActionType actionType,
        string actionPayloadJson,
        Guid createdByUserId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException(
                "OrganizationId is required.");

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException(
                "CreatedByUserId is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Name is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = Normalize(description);
        TriggerType = triggerType;

        ConditionsJson =
            NormalizeJson(conditionsJson);

        ActionType = actionType;

        ActionPayloadJson =
            NormalizeJson(actionPayloadJson);

        IsEnabled = true;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } =
        string.Empty;

    public string? Description { get; private set; }

    public AutomationTriggerType TriggerType
    {
        get;
        private set;
    }

    public string ConditionsJson
    {
        get;
        private set;
    } = "{}";

    public AutomationActionType ActionType
    {
        get;
        private set;
    }

    public string ActionPayloadJson
    {
        get;
        private set;
    } = "{}";

    public bool IsEnabled { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? LastExecutedAtUtc
    {
        get;
        private set;
    }

    public int ExecutionCount { get; private set; }

    public string? Module { get; private set; }

    public int Priority { get; private set; } = 100;

    public string? ScopeSiteIdsJson { get; private set; }

    public string? DefinitionJson { get; private set; }

    public Guid? ModifiedByUserId { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public bool HasDefinition =>
        !string.IsNullOrWhiteSpace(
            DefinitionJson);

    public void ApplyDefinition(
        string definitionJson,
        AutomationTriggerType triggerType,
        string? module,
        int priority,
        string? scopeSiteIdsJson,
        Guid modifiedByUserId)
    {
        if (string.IsNullOrWhiteSpace(
                definitionJson))
            throw new ArgumentException(
                "Definition JSON is required.",
                nameof(definitionJson));

        DefinitionJson =
            definitionJson.Trim();

        TriggerType = triggerType;

        Module = Normalize(module);

        Priority = Math.Clamp(priority, 0, 1000);

        ScopeSiteIdsJson =
            string.IsNullOrWhiteSpace(scopeSiteIdsJson)
                ? null
                : scopeSiteIdsJson.Trim();

        if (modifiedByUserId != Guid.Empty)
            ModifiedByUserId = modifiedByUserId;

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RegisterFailure()
    {
        ConsecutiveFailures++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ResetFailures()
    {
        if (ConsecutiveFailures == 0)
            return;

        ConsecutiveFailures = 0;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string? description,
        AutomationTriggerType triggerType,
        string conditionsJson,
        AutomationActionType actionType,
        string actionPayloadJson,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Name is required.");

        Name = name.Trim();
        Description = Normalize(description);
        TriggerType = triggerType;
        ConditionsJson =
            NormalizeJson(conditionsJson);

        ActionType = actionType;
        ActionPayloadJson =
            NormalizeJson(actionPayloadJson);

        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RegisterExecution()
    {
        ExecutionCount++;
        LastExecutedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string NormalizeJson(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "{}"
            : value.Trim();
    }
}