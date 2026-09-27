using System.Collections.Concurrent;

namespace FandNEL.Proxy.Irc;

/// <summary>短期记录本地回显，过滤服务端回环造成的重复消息。</summary>
internal sealed class IrcLocalEchoTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);

    internal void Remember(string text)
    {
        var now = DateTimeOffset.UtcNow;
        _entries[text] = now;
        if (_entries.Count <= IrcConstants.MaximumRecentEchoes)
            return;

        foreach (var key in _entries
                     .Where(pair => now - pair.Value > IrcConstants.RecentEchoLifetime)
                     .Select(pair => pair.Key)
                     .ToArray())
            _entries.TryRemove(key, out _);
    }

    internal bool Matches(string serverText)
    {
        var plain = IrcConstants.ColorCode().Replace(serverText, string.Empty);
        var now = DateTimeOffset.UtcNow;
        return _entries.Any(pair => now - pair.Value <= IrcConstants.RecentEchoLifetime
            && plain.Contains(pair.Key, StringComparison.Ordinal));
    }
}
