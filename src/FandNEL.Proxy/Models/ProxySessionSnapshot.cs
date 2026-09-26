using System.Net;

namespace FandNEL.Proxy.Models;

public enum ProxySessionState
{
    Created,
    Running,
    Stopping,
    Stopped,
    Faulted
}

/// <summary>代理会话的状态快照，不包含访问令牌。</summary>
public sealed record ProxySessionSnapshot(
    Guid Id,
    ProxySessionState State,
    IPEndPoint? LocalEndpoint,
    ServerTarget Target,
    PlayerRole Role,
    int ActiveConnections,
    DateTimeOffset StartedAt,
    DateTimeOffset? StoppedAt,
    string? UserId,
    string? GameId,
    string? GameVersion,
    string? RentalServerId,
    string? LastError);
