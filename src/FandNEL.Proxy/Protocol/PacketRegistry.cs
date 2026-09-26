using System.Reflection;

namespace FandNEL.Proxy.Protocol;

public interface IPacketHandler
{
    ValueTask HandleAsync(PacketContext context, CancellationToken cancellationToken);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RegisterPacketAttribute(
    ConnectionState state,
    PacketDirection direction,
    int packetId,
    params ProtocolVersion[] versions) : Attribute
{
    public ConnectionState State { get; } = state;
    public PacketDirection Direction { get; } = direction;
    public int PacketId { get; } = packetId;
    public ProtocolVersion[] Versions { get; } = versions;
    public int Priority { get; set; }
}

/// <summary>每个监听会话拥有独立注册表；同一包可绑定多个有序处理器。</summary>
public sealed class PacketRegistry
{
    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];
    private long _sequence;

    public PacketRegistry()
    {
        RegisterAssembly(typeof(Builtin.HandshakeHandler).Assembly);
    }

    public IDisposable Register(ConnectionState state, PacketDirection direction, int packetId,
        Func<PacketContext, CancellationToken, ValueTask> handler,
        IEnumerable<ProtocolVersion>? versions = null, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (packetId < 0)
            throw new ArgumentOutOfRangeException(nameof(packetId));
        var entry = new Entry(state, direction, packetId, versions?.ToHashSet(), priority,
            Interlocked.Increment(ref _sequence), handler);
        lock (_gate)
            _entries.Add(entry);
        return new Registration(() => { lock (_gate) _entries.Remove(entry); });
    }

    /// <summary>按属性自动注册；释放返回的作用域可一次卸载整个插件。</summary>
    public IDisposable RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var registrations = new List<IDisposable>();
        try
        {
            foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IPacketHandler).IsAssignableFrom(type)))
            {
                var attributes = type.GetCustomAttributes<RegisterPacketAttribute>().ToArray();
                if (attributes.Length == 0)
                    continue;
                var handler = (IPacketHandler?)Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"无法创建包处理器 {type.FullName}。");
                foreach (var attribute in attributes)
                    registrations.Add(Register(attribute.State, attribute.Direction, attribute.PacketId,
                        handler.HandleAsync, attribute.Versions.Length == 0 ? null : attribute.Versions, attribute.Priority));
            }
            return new Registration(() => { foreach (var registration in registrations) registration.Dispose(); });
        }
        catch
        {
            foreach (var registration in registrations)
                registration.Dispose();
            throw;
        }
    }

    internal async ValueTask DispatchAsync(PacketContext context, CancellationToken cancellationToken)
    {
        Entry[] handlers;
        lock (_gate)
            handlers = _entries.Where(entry => entry.State == context.State && entry.Direction == context.Direction
                    && entry.PacketId == context.PacketId && (entry.Versions is null || entry.Versions.Contains(context.Version)))
                .OrderByDescending(entry => entry.Priority).ThenBy(entry => entry.Sequence).ToArray();
        foreach (var entry in handlers)
        {
            await entry.Handler(context, cancellationToken).ConfigureAwait(false);
            if (context.IsCancelled || context.PropagationStopped)
                break;
        }
    }

    private sealed record Entry(ConnectionState State, PacketDirection Direction, int PacketId,
        HashSet<ProtocolVersion>? Versions, int Priority, long Sequence,
        Func<PacketContext, CancellationToken, ValueTask> Handler);

    private sealed class Registration(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
