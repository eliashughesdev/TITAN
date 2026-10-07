using System.Text.Json;

namespace TitanMDM.Domain.Entities;

public sealed class HelpdeskRequestTemplate
{
    private HelpdeskRequestTemplate()
    {
    }

    public HelpdeskRequestTemplate(
        Guid organizationId,
        Guid actorId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UpdatedByUserId = actorId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }

    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Category { get; private set; } = "general";
    public string TicketType { get; private set; } = "incident";
    public string QuestionsJson { get; private set; } = "[]";

    public bool IsActive { get; private set; } = true;
    public int Revision { get; private set; }

    public Guid UpdatedByUserId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Configure(
        string title,
        string description,
        string category,
        string ticketType,
        string[] questions,
        bool active,
        Guid actorId)
    {
        title = title.Trim();
        description = description.Trim();
        category = category.Trim().ToLowerInvariant();

        if (title.Length is < 3 or > 100 ||
            description.Length > 300)
        {
            throw new ArgumentException(
                "El título debe tener entre 3 y 100 caracteres " +
                "y la descripción hasta 300.");
        }

        if (category.Length is < 1 or > 80 ||
            ticketType is not ("incident" or "request"))
        {
            throw new ArgumentException(
                "Categoría o tipo de solicitud inválidos.");
        }

        var normalized = questions
            .Select(x => x.Trim())
            .ToArray();

        if (normalized.Length is < 1 or > 8 ||
            normalized.Any(x => x.Length is < 3 or > 120) ||
            normalized
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != normalized.Length)
        {
            throw new ArgumentException(
                "Agrega entre 1 y 8 preguntas diferentes, " +
                "de 3 a 120 caracteres cada una.");
        }

        Title = title;
        Description = description;
        Category = category;
        TicketType = ticketType;
        QuestionsJson = JsonSerializer.Serialize(normalized);
        IsActive = active;

        UpdatedByUserId = actorId;
        UpdatedAtUtc = DateTime.UtcNow;
        Revision++;
    }
}