using FandNEL.Proxy.Models;

namespace FandNEL.Proxy.Protocol;

public enum ProxyEventKind
{
    Started,
    ClientConnected,
    ClientDisconnected,
    ServerChanged,
    RoleChanged,
    JoinServer,
    ConnectionFailed,
    Faulted,
    Stopped
}

public sealed record ProxyEvent(
    Guid SessionId,
    ProxyEventKind Kind,
    DateTimeOffset OccurredAt,
    ProxySessionSnapshot Snapshot,
    string? Message = null);

public sealed class ProxyEventArgs(ProxyEvent value) : EventArgs
{
    public ProxyEvent Value { get; } = value;
}
