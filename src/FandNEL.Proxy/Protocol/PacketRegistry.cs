using System.Linq.Expressions;
using System.Reflection;
using System.Text;

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

/// <summary>声明包模型的协议元数据，由注册表反射扫描后自动挂载只读解析器。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RegisterPacketModelAttribute(
    ConnectionState state,
    PacketDirection direction,
    int packetId,
    params ProtocolVersion[] versions) : Attribute
{
    public ConnectionState State { get; } = state;
    public PacketDirection Direction { get; } = direction;
    public int PacketId { get; } = packetId;
    public ProtocolVersion[] Versions { get; } = versions;
    public int Priority { get; set; } = 1000;
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
            var types = assembly.GetTypes();
            foreach (var type in types.Where(type => !type.IsAbstract && typeof(IPacketHandler).IsAssignableFrom(type)))
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

            var parsers = new Dictionary<Type, Func<PacketContext, object>>();
            foreach (var type in types)
            {
                if (!type.GetCustomAttributes<RegisterPacketModelAttribute>().Any())
                    continue;
                if (!parsers.TryGetValue(type, out var parser))
                {
                    parser = PacketModelParser.Create(type);
                    parsers.Add(type, parser);
                }
                foreach (var attribute in type.GetCustomAttributes<RegisterPacketModelAttribute>())
                {
                    registrations.Add(Register(attribute.State, attribute.Direction, attribute.PacketId,
                        (context, _) =>
                        {
                            try
                            {
                                context.SetParsedPacket(parser(context));
                            }
                            catch (Exception exception) when (TryGetRecoverableParseException(exception, out var parseException))
                            {
                                context.SetParseException(parseException);
                            }

                            return ValueTask.CompletedTask;
                        }, attribute.Versions.Length == 0 ? null : attribute.Versions, attribute.Priority));
                }
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

    private static bool TryGetRecoverableParseException(Exception exception, out Exception parseException)
    {
        parseException = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
        return parseException is InvalidDataException or NotSupportedException or OverflowException
            or DecoderFallbackException or ArgumentException;
    }

    private static class PacketModelParser
    {
        public static Func<PacketContext, object> Create(Type packetType)
        {
            ArgumentNullException.ThrowIfNull(packetType);
            var methods = packetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "Read" && method.ReturnType != typeof(void))
                .ToArray();
            var payloadMethod = methods.SingleOrDefault(method => HasParameters(method, typeof(ReadOnlyMemory<byte>)));
            var packetMethod = methods.SingleOrDefault(method => HasParameters(method, typeof(int), typeof(ReadOnlyMemory<byte>)));
            if (payloadMethod is null && packetMethod is null)
                throw new InvalidOperationException($"包模型 {packetType.FullName} 缺少支持的 Read 方法。");
            if (payloadMethod is not null && packetMethod is not null)
                throw new InvalidOperationException($"包模型 {packetType.FullName} 的 Read 方法存在歧义。");

            var method = payloadMethod ?? packetMethod!;
            var contextParameter = Expression.Parameter(typeof(PacketContext), "context");
            var payload = Expression.Property(contextParameter, nameof(PacketContext.Payload));
            var call = payloadMethod is not null
                ? Expression.Call(method, payload)
                : Expression.Call(method, Expression.Property(contextParameter, nameof(PacketContext.PacketId)), payload);
            var body = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<PacketContext, object>>(body, contextParameter).Compile();
        }

        private static bool HasParameters(MethodInfo method, params Type[] parameters) =>
            method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters);
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
