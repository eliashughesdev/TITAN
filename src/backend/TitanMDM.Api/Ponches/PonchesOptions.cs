namespace TitanMDM.Api.Ponches;

public sealed class PonchesOptions
{
    public const string SectionName = "Ponches";

    /// <summary>
    /// Servicio Python interno.
    ///
    /// Por diseño empresarial debe permanecer accesible
    /// solamente desde el host TitanMDM.
    /// </summary>
    public string BaseUrl { get; init; } =
        "http://127.0.0.1:8127";

    /// <summary>
    /// Credencial machine-to-machine compartida temporalmente
    /// entre TitanMDM API y el servicio interno de Ponches.
    ///
    /// Nunca debe almacenarse en appsettings.json.
    /// </summary>
    public string IntegrationKey { get; init; } =
        string.Empty;

    /// <summary>
    /// Timeout general para operaciones normales.
    /// </summary>
    public int TimeoutSeconds { get; init; } =
        180;

    /// <summary>
    /// Timeout extendido para escritura/sincronización
    /// con relojes físicos.
    /// </summary>
    public int DeviceOperationTimeoutSeconds { get; init; } =
        240;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(IntegrationKey) &&
        IntegrationKey.Trim().Length >= 32;

    public Uri GetBaseUri()
    {
        if (!Uri.TryCreate(
                BaseUrl,
                UriKind.Absolute,
                out var uri))
        {
            throw new InvalidOperationException(
                "Ponches:BaseUrl no contiene una URL válida.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException(
                "El bridge local de Ponches debe utilizar HTTP interno.");
        }

        /*
         * El Python NO debe convertirse accidentalmente
         * en otro API público de TitanMDM.
         */
        if (!uri.IsLoopback)
        {
            throw new InvalidOperationException(
                "Ponches:BaseUrl debe apuntar a localhost/127.0.0.1.");
        }

        return uri;
    }
}