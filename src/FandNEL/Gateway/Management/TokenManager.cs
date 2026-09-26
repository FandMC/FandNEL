using System.Collections.Concurrent;

namespace FandNEL.Gateway.Management;

public sealed record TokenSnapshot(
    string UserId,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// 管理已激活账号的短期游戏 token。token 只通过 TryGetAccessToken 返回给内部服务，
/// 快照和事件不会泄漏密钥。
/// </summary>
public sealed class TokenManager
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(25);
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new(StringComparer.Ordinal);
    private readonly GatewayEventHub? _events;

    public TokenManager(GatewayEventHub? events = null)
    {
        _events = events;
    }

    public int Count => _tokens.Count;

    public void UpdateToken(string userId, string token, TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var now = DateTimeOffset.UtcNow;
        var entry = new TokenEntry(token, now, now.Add(lifetime ?? DefaultLifetime));
        _tokens[userId] = entry;
        _events?.Publish(GatewayEventKind.TokenUpdated, userId);
    }

    public bool TryGetAccessToken(string userId, out string token)
    {
        if (_tokens.TryGetValue(userId, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                token = entry.Value;
                return true;
            }

            RemoveToken(userId, publish: true);
        }

        token = string.Empty;
        return false;
    }

    public string GetAccessToken(string userId) =>
        TryGetAccessToken(userId, out var token) ? token : string.Empty;

    public bool RemoveToken(string userId) => RemoveToken(userId, publish: true);

    public IReadOnlyList<TokenSnapshot> GetSnapshots()
    {
        RemoveExpired();
        return _tokens.Select(static pair => new TokenSnapshot(
            pair.Key, pair.Value.UpdatedAt, pair.Value.ExpiresAt)).ToArray();
    }

    public int RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var removed = 0;
        foreach (var pair in _tokens)
        {
            if (pair.Value.ExpiresAt <= now && RemoveToken(pair.Key, publish: true))
                removed++;
        }

        return removed;
    }

    private bool RemoveToken(string userId, bool publish)
    {
        var removed = _tokens.TryRemove(userId, out _);
        if (removed && publish)
            _events?.Publish(GatewayEventKind.TokenExpired, userId);
        return removed;
    }

    private sealed record TokenEntry(string Value, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt);
}
