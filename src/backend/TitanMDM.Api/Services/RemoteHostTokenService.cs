using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TitanMDM.Api.Services;

public sealed class RemoteHostTokenService
{
    private readonly ConcurrentDictionary<
        string,
        RemoteHostTokenEntry> _tokens =
            new(StringComparer.Ordinal);

    public string Create(
        Guid organizationId,
        Guid deviceId,
        Guid sessionId,
        TimeSpan lifetime)
    {
        CleanupExpired();

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "La duración del token debe ser mayor que cero.");
        }

        var token =
            Convert.ToHexString(
                RandomNumberGenerator
                    .GetBytes(32));

        var entry =
            new RemoteHostTokenEntry(
                OrganizationId:
                    organizationId,

                DeviceId:
                    deviceId,

                SessionId:
                    sessionId,

                ExpiresAtUtc:
                    DateTime.UtcNow.Add(
                        lifetime));

        _tokens[token] =
            entry;

        return token;
    }

    public bool TryValidate(
        string token,
        Guid sessionId,
        out RemoteHostTokenEntry entry)
    {
        entry =
            default!;

        if (string.IsNullOrWhiteSpace(
                token))
        {
            return false;
        }

        CleanupExpired();

        if (!_tokens.TryGetValue(
                token,
                out var candidate))
        {
            return false;
        }

        if (candidate.SessionId !=
            sessionId)
        {
            return false;
        }

        if (candidate.ExpiresAtUtc <=
            DateTime.UtcNow)
        {
            _tokens.TryRemove(
                token,
                out _);

            return false;
        }

        entry =
            candidate;

        return true;
    }

    public void RevokeSession(
        Guid sessionId)
    {
        foreach (var pair in
                 _tokens.ToArray())
        {
            if (pair.Value.SessionId !=
                sessionId)
            {
                continue;
            }

            _tokens.TryRemove(
                pair.Key,
                out _);
        }
    }

    private void CleanupExpired()
    {
        var now =
            DateTime.UtcNow;

        foreach (var pair in
                 _tokens.ToArray())
        {
            if (pair.Value.ExpiresAtUtc >
                now)
            {
                continue;
            }

            _tokens.TryRemove(
                pair.Key,
                out _);
        }
    }
}

public sealed record RemoteHostTokenEntry(
    Guid OrganizationId,
    Guid DeviceId,
    Guid SessionId,
    DateTime ExpiresAtUtc);