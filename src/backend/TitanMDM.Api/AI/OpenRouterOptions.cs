namespace TitanMDM.Api.AI;

public sealed class OpenRouterOptions
{
    public const string SectionName =
        "AI:OpenRouter";

    // ============================================================
    // GENERAL
    // ============================================================

    public bool Enabled { get; set; } =
        true;

    public string BaseUrl { get; set; } =
        "https://openrouter.ai/api/v1";

    public string ApiKey { get; set; } =
        string.Empty;

    // ============================================================
    // APPLICATION IDENTIFICATION
    // ============================================================

    public string ApplicationTitle { get; set; } =
        "TitanMDM";

    public string AppName { get; set; } =
        "TitanMDM";

    public string? SiteUrl { get; set; }

    // ============================================================
    // MODELS
    // ============================================================

    /// <summary>
    /// Default model used when no specialized model is configured.
    /// </summary>
    public string Model { get; set; } =
        "openrouter/free";

    /// <summary>
    /// Primary conversational model for Titan / Fiorella.
    /// </summary>
    public string AssistantModel { get; set; } =
        string.Empty;

    /// <summary>
    /// Model used for ticket classification and routing.
    /// </summary>
    public string ClassificationModel { get; set; } =
        string.Empty;

    /// <summary>
    /// Model used for automation / background processing.
    /// </summary>
    public string AutomationModel { get; set; } =
        string.Empty;

    // ============================================================
    // GENERATION
    // ============================================================

    public int MaxTokens { get; set; } =
        1200;

    public double Temperature { get; set; } =
        0.2;

    // ============================================================
    // HTTP / RESILIENCE
    // ============================================================

    public int TimeoutSeconds { get; set; } =
        60;

    public int MaxRetries { get; set; } =
        3;

    public int RetryBaseDelayMilliseconds { get; set; } =
        750;

    // ============================================================
    // FEATURE FLAGS
    // ============================================================

    public bool EnableAssistant { get; set; } =
        true;

    public bool EnableClassification { get; set; } =
        true;

    public bool EnableAutomation { get; set; } =
        true;

    // ============================================================
    // MODEL RESOLUTION
    // ============================================================

    public string ResolveAssistantModel()
    {
        if (
            !string.IsNullOrWhiteSpace(
                AssistantModel))
        {
            return AssistantModel.Trim();
        }

        if (
            !string.IsNullOrWhiteSpace(
                Model))
        {
            return Model.Trim();
        }

        return "openrouter/free";
    }

    public string ResolveClassificationModel()
    {
        if (
            !string.IsNullOrWhiteSpace(
                ClassificationModel))
        {
            return ClassificationModel.Trim();
        }

        if (
            !string.IsNullOrWhiteSpace(
                Model))
        {
            return Model.Trim();
        }

        return "openrouter/free";
    }

    public string ResolveAutomationModel()
    {
        if (
            !string.IsNullOrWhiteSpace(
                AutomationModel))
        {
            return AutomationModel.Trim();
        }

        if (
            !string.IsNullOrWhiteSpace(
                Model))
        {
            return Model.Trim();
        }

        return "openrouter/free";
    }
}