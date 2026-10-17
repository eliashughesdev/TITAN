using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TitanMDM.Application.Assistant;

namespace TitanMDM.Infrastructure.Assistant;

/// <summary>
/// Almacén de conversaciones en memoria, aislado por
/// organización y usuario. Es la implementación fundacional:
/// no requiere migraciones EF y mantiene la separación
/// estricta de sesiones entre inquilinos.
/// </summary>
public sealed class InMemoryAssistantSessionStore
    : IAssistantSessionStore
{
    private readonly AssistantOptions _options;
    private readonly ILogger<InMemoryAssistantSessionStore>
        _logger;

    private readonly object _gate =
        new();

    private readonly Dictionary<Guid, State> _conversations =
        new();

    public InMemoryAssistantSessionStore(
        IOptions<AssistantOptions> options,
        ILogger<InMemoryAssistantSessionStore> logger)
    {
        _options =
            options.Value;

        _logger =
            logger;
    }

    public AssistantConversation Create(
        Guid organizationId,
        Guid userId,
        Guid conversationId,
        string title)
    {
        lock (_gate)
        {
            PruneExpired();

            if (_conversations.TryGetValue(
                    conversationId,
                    out var existing))
            {
                return ToConversation(
                    conversationId,
                    existing);
            }

            EnforceQuota(
                organizationId,
                userId);

            var now =
                DateTime.UtcNow;

            var state =
                new State
                {
                    OrganizationId =
                        organizationId,
                    UserId =
                        userId,
                    Title =
                        string.IsNullOrWhiteSpace(title)
                            ? "Nueva conversación"
                            : title.Trim(),
                    CreatedAtUtc =
                        now,
                    UpdatedAtUtc =
                        now
                };

            _conversations[conversationId] =
                state;

            return ToConversation(
                conversationId,
                state);
        }
    }

    public AssistantConversation? Get(
        Guid organizationId,
        Guid userId,
        Guid conversationId)
    {
        lock (_gate)
        {
            PruneExpired();

            if (!_conversations.TryGetValue(
                    conversationId,
                    out var state) ||
                state.OrganizationId != organizationId ||
                state.UserId != userId)
            {
                return null;
            }

            return ToConversation(
                conversationId,
                state);
        }
    }

    public IReadOnlyList<AssistantConversationSummary> List(
        Guid organizationId,
        Guid userId)
    {
        lock (_gate)
        {
            PruneExpired();

            return _conversations
                .Where(
                    pair =>
                        pair.Value.OrganizationId ==
                            organizationId &&
                        pair.Value.UserId == userId)
                .OrderByDescending(
                    pair =>
                        pair.Value.UpdatedAtUtc)
                .Select(
                    pair =>
                        new AssistantConversationSummary(
                            pair.Key,
                            pair.Value.Title,
                            pair.Value.CreatedAtUtc,
                            pair.Value.UpdatedAtUtc,
                            pair.Value.Entries.Count))
                .ToList();
        }
    }

    public bool Delete(
        Guid organizationId,
        Guid userId,
        Guid conversationId)
    {
        lock (_gate)
        {
            PruneExpired();

            if (!_conversations.TryGetValue(
                    conversationId,
                    out var state) ||
                state.OrganizationId != organizationId ||
                state.UserId != userId)
            {
                return false;
            }

            return _conversations.Remove(
                conversationId);
        }
    }

    public void Append(
        Guid organizationId,
        Guid userId,
        Guid conversationId,
        AssistantConversationEntry entry)
    {
        lock (_gate)
        {
            PruneExpired();

            if (!_conversations.TryGetValue(
                    conversationId,
                    out var state) ||
                state.OrganizationId != organizationId ||
                state.UserId != userId)
            {
                throw new InvalidOperationException(
                    "La conversación no pertenece al usuario en sesión.");
            }

            state.Entries.Add(
                entry);

            state.UpdatedAtUtc =
                entry.CreatedAtUtc;

            if (entry.Role == "user" &&
                !state.StartedFromUserMessage)
            {
                state.StartedFromUserMessage =
                    true;

                var candidate =
                    BuildTitle(
                        entry.Content);

                if (!string.IsNullOrWhiteSpace(
                        candidate))
                {
                    state.Title =
                        candidate;
                }
            }
        }
    }

    public int CountActive(
        Guid organizationId,
        Guid userId)
    {
        lock (_gate)
        {
            PruneExpired();

            return _conversations.Values.Count(
                state =>
                    state.OrganizationId == organizationId &&
                    state.UserId == userId);
        }
    }

    private string BuildTitle(
        string content)
    {
        var normalized =
            (content ?? string.Empty)
                .Replace(
                    '\r',
                    ' ')
                .Replace(
                    '\n',
                    ' ')
                .Trim();

        while (normalized.Contains(
                   "  ",
                   StringComparison.Ordinal))
        {
            normalized =
                normalized.Replace(
                    "  ",
                    " ",
                    StringComparison.Ordinal);
        }

        var max =
            Math.Clamp(
                _options.TitleMaxLength,
                8,
                160);

        if (normalized.Length <= max)
        {
            return normalized;
        }

        return normalized[..max].TrimEnd() + "…";
    }

    private void EnforceQuota(
        Guid organizationId,
        Guid userId)
    {
        var limit =
            Math.Clamp(
                _options.MaxActiveSessionsPerUser,
                1,
                200);

        var owned =
            _conversations
                .Where(
                    pair =>
                        pair.Value.OrganizationId ==
                            organizationId &&
                        pair.Value.UserId == userId)
                .OrderBy(
                    pair =>
                        pair.Value.UpdatedAtUtc)
                .ToList();

        while (owned.Count >= limit)
        {
            var oldest =
                owned[0];

            owned.RemoveAt(
                0);

            _conversations.Remove(
                oldest.Key);

            _logger.LogInformation(
                "Sesión de asistente reciclada por cuota. UserId={UserId}",
                userId);
        }
    }

    private void PruneExpired()
    {
        var ttl =
            TimeSpan.FromMinutes(
                Math.Clamp(
                    _options.SessionTtlMinutes,
                    5,
                    1440));

        var threshold =
            DateTime.UtcNow - ttl;

        var expired =
            _conversations
                .Where(
                    pair =>
                        pair.Value.UpdatedAtUtc < threshold)
                .Select(
                    pair =>
                        pair.Key)
                .ToList();

        foreach (var id in expired)
        {
            _conversations.Remove(
                id);
        }
    }

    private static AssistantConversation ToConversation(
        Guid conversationId,
        State state)
    {
        return new AssistantConversation(
            conversationId,
            state.OrganizationId,
            state.UserId,
            state.Title,
            state.CreatedAtUtc,
            state.UpdatedAtUtc,
            state.Entries.ToArray());
    }

    private sealed class State
    {
        public Guid OrganizationId { get; init; }

        public Guid UserId { get; init; }

        public string Title { get; set; } =
            "Nueva conversación";

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; set; }

        public bool StartedFromUserMessage { get; set; }

        public List<AssistantConversationEntry> Entries { get; } =
            new();
    }
}
