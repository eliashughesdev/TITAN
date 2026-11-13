namespace TitanMDM.Api.Ponches;

/// <summary>
/// Supervisión del servicio Python (Ponches / Fiorella) desde el API.
///
/// El host y el puerto salen de <c>Ponches:BaseUrl</c>, para que el API
/// lance Python exactamente donde después lo va a consultar.
/// </summary>
public sealed class PythonServiceOptions
{
    public const string SectionName = "PythonService";

    /// <summary>Lanzar y vigilar Python desde el API.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Carpeta del backend de Ponches (contiene <c>app\main.py</c>).
    /// Relativa a ContentRootPath si no es absoluta. Por defecto la carpeta
    /// hermana del API, como en el paquete y en la instalación
    /// (api\ y ponches\).
    /// </summary>
    public string WorkingDirectory { get; init; } =
        "../ponches";

    /// <summary>
    /// Intérprete. Relativo a WorkingDirectory si no es absoluto.
    /// </summary>
    public string PythonExecutable { get; init; } =
        ".venv\\Scripts\\python.exe";

    /// <summary>
    /// Carpeta de datos de Ponches (JSON y SQLite). Vacía: la del código
    /// (backend\data). En producción, %ProgramData%\TitanMDM\ponches-data,
    /// compartida con el stack de desarrollo.
    /// </summary>
    public string DataDirectory { get; init; } = string.Empty;

    /// <summary>Módulo ASGI de uvicorn.</summary>
    public string AppModule { get; init; } =
        "app.main:app";

    /// <summary>Espera antes de reiniciar tras una caída.</summary>
    public int RestartDelaySeconds { get; init; } = 5;

    /// <summary>Espera máxima cuando Python cae repetidamente al arrancar.</summary>
    public int MaxRestartDelaySeconds { get; init; } = 60;
}
