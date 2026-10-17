namespace TitanMDM.Infrastructure.Assistant;

public sealed class AssistantOptions
{
    public const string SectionName =
        "Assistant";

    public bool Enabled { get; set; } =
        true;

    /// <summary>
    /// Proveedor preferido: "Auto", "OpenRouter" u "Ollama".
    /// </summary>
    public string Provider { get; set; } =
        "Auto";

    public int MinMessageLength { get; set; } =
        2;

    public int MaxMessageLength { get; set; } =
        1500;

    public int MaxHistoryMessages { get; set; } =
        20;

    public int MaxAnswerCharacters { get; set; } =
        4000;

    public int MaxToolOutputCharacters { get; set; } =
        4000;

    public int MaxToolCallsPerTurn { get; set; } =
        4;

    public int SessionTtlMinutes { get; set; } =
        240;

    public int MaxActiveSessionsPerUser { get; set; } =
        20;

    /// <summary>
    /// Máximo de turnos por usuario dentro de la ventana de cuota.
    /// </summary>
    public int MaxTurnsPerWindow { get; set; } =
        30;

    /// <summary>
    /// Tamaño de la ventana de cuota, en segundos.
    /// </summary>
    public int QuotaWindowSeconds { get; set; } =
        60;

    public int TitleMaxLength { get; set; } =
        80;

    public int RequestTimeoutSeconds { get; set; } =
        60;

    public double Temperature { get; set; } =
        0.15;

    public int MaxTokens { get; set; } =
        700;
}

public sealed class OllamaAssistantOptions
{
    public const string SectionName =
        "Assistant:Ollama";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } =
        "http://localhost:11434";

    public string Model { get; set; } =
        "llama3.1";

    public int TimeoutSeconds { get; set; } =
        90;

    public int MaxTokens { get; set; } =
        700;
}
