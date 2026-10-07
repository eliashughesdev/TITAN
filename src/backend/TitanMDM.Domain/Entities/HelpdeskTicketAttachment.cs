namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskTicketAttachment
{
    private HelpdeskTicketAttachment()
    {
    }

    public HelpdeskTicketAttachment(
        Guid organizationId,
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string storageName,
        string contentType,
        long sizeBytes,
        bool isInternal,
        bool isInline = false,
        string? contentId = null)
    {
        if (
            organizationId == Guid.Empty
            ||
            ticketId == Guid.Empty
            ||
            uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organización, ticket y usuario son obligatorios.");
        }

        if (
            string.IsNullOrWhiteSpace(
                fileName)
            ||
            string.IsNullOrWhiteSpace(
                storageName)
            ||
            sizeBytes <= 0)
        {
            throw new ArgumentException(
                "El archivo adjunto no es válido.");
        }

        Id =
            Guid.NewGuid();

        OrganizationId =
            organizationId;

        TicketId =
            ticketId;

        UploadedByUserId =
            uploadedByUserId;

        FileName =
            NormalizeRequired(
                fileName,
                180);

        StorageName =
            NormalizeRequired(
                storageName,
                80);

        ContentType =
            NormalizeRequired(
                contentType,
                80);

        SizeBytes =
            sizeBytes;

        IsInternal =
            isInternal;

        IsInline =
            isInline;

        ContentId =
            NormalizeOptional(
                contentId,
                500);

        CreatedAtUtc =
            DateTime.UtcNow;
    }

    public Guid Id
    {
        get;
        private set;
    }

    public Guid OrganizationId
    {
        get;
        private set;
    }

    public Guid TicketId
    {
        get;
        private set;
    }

    public Guid UploadedByUserId
    {
        get;
        private set;
    }

    public string FileName
    {
        get;
        private set;
    } = string.Empty;

    public string StorageName
    {
        get;
        private set;
    } = string.Empty;

    public string ContentType
    {
        get;
        private set;
    } = string.Empty;

    public long SizeBytes
    {
        get;
        private set;
    }

    public bool IsInternal
    {
        get;
        private set;
    }

    /*
     * true:
     * imagen incrustada dentro del correo.
     *
     * Normalmente:
     * - firma corporativa
     * - logo
     * - foto incrustada
     *
     * false:
     * archivo adjunto convencional.
     */
    public bool IsInline
    {
        get;
        private set;
    }

    /*
     * CID proveniente del correo.
     *
     * Ejemplo:
     *
     * image001.png@01DA...
     *
     * Más adelante permitirá reconstruir
     * firmas HTML de forma segura.
     */
    public string? ContentId
    {
        get;
        private set;
    }

    public DateTime CreatedAtUtc
    {
        get;
        private set;
    }

    private static string NormalizeRequired(
        string value,
        int maxLength)
    {
        var normalized =
            value.Trim();

        return normalized[
            ..Math.Min(
                normalized.Length,
                maxLength)];
    }

    private static string? NormalizeOptional(
        string? value,
        int maxLength)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        return normalized[
            ..Math.Min(
                normalized.Length,
                maxLength)];
    }
}