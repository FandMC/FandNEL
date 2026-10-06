using FandNEL.Proxy.Packet.Minecraft.V1206;
using FandNEL.Proxy.Packet.Heypixel;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Heypixel;

/// <summary>每条游戏连接独立持有握手、玩家和定时任务；取消沿用代理连接的生命周期。</summary>
internal sealed class HeypixelConnection(MinecraftConnection connection)
{
    private static readonly long KeepAliveOrigin = Environment.TickCount64;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HeypixelPluginMessages _plugins = new();
    private readonly HeypixelHolograms _holograms = new();
    private AntiCheatSession? _session;
    private HeypixelPlayerState? _player;
    private bool _playing;
    private bool _timerStarted;
    internal Task BackgroundCompletion { get; private set; } = Task.CompletedTask;

    internal async ValueTask InitializeAsync()
    {
        await _gate.WaitAsync(connection.LifetimeToken).ConfigureAwait(false);
        try { await GetSessionAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task<AntiCheatSession> GetSessionAsync()
    {
        if (_session is not null) return _session;
        var options = connection.Options;
        var profile = connection.PlayerUuid ?? throw new InvalidDataException("Heypixel 登录响应缺少玩家 UUID。");
        var userId = options.UserId ?? throw new InvalidDataException("Heypixel 连接缺少游戏用户 ID。");
        var name = connection.PlayerName ?? throw new InvalidDataException("Heypixel 登录响应缺少玩家名称。");
        var sessionId = options.Heypixel.UseLocalSessionKey
            ? Uuid128.Parse(Guid.NewGuid().ToString())
            : await HeypixelKeyDerivation.DeriveAsync(options.Heypixel, profile, userId, name, connection.LifetimeToken)
                .ConfigureAwait(false);
        connection.LifetimeToken.ThrowIfCancellationRequested();
        _session = new AntiCheatSession(sessionId,
            new HeypixelClientIdentity(userId, options.AccessToken ?? string.Empty, options.Heypixel.ModDirectories));
        return _session;
    }

    internal async ValueTask HandleAsync(PacketContext context)
    {
        await _gate.WaitAsync(connection.LifetimeToken).ConfigureAwait(false);
        try
        {
            if (connection.IsClosed) return;
            if (context.State == ConnectionState.Configuration)
            {
                if (context.Direction == PacketDirection.ServerBound && context.PacketId == MinecraftPacketIds.Configuration.Finish)
                    context.AfterForward(ResumeAsync);
                else
                    await _plugins.HandleAsync(this, context).ConfigureAwait(false);
                return;
            }

            if (context.Direction == PacketDirection.ClientBound)
            {
                if (context.State == ConnectionState.Play) HeypixelVipTags.Apply(context);
                switch (context.PacketId)
                {
                    case MinecraftPacketIds.Clientbound.StartConfiguration:
                        _playing = false;
                        break;
                    case MinecraftPacketIds.Clientbound.JoinGame:
                        var join = JoinGamePacket.Read(context.Payload);
                        _player = new HeypixelPlayerState(join.EntityId);
                        await ApplyHologramsAsync(context, _holograms.ProcessJoin(join, connection.PlayerUuid, connection.PlayerName)).ConfigureAwait(false);
                        context.AfterForward(JoinAsync);
                        break;
                    case MinecraftPacketIds.Clientbound.PlayerCorrection when _player is not null:
                        _player.Apply(PlayerPositionPacket.Read(context.Payload));
                        break;
                    case MinecraftPacketIds.Clientbound.EntityMetadata:
                        if (EntityMetadataPacket.TryRead(context.Payload, out var metadata) && metadata is not null)
                        {
                            if (metadata.EntityId == _player?.EntityId)
                            {
                                var entries = metadata.Entries.Where(entry => entry.SerializerId != (int)MetadataSerializer.Pose).ToArray();
                                if (entries.Length != metadata.Entries.Count)
                                    context.ReplacePayload(new EntityMetadataPacket(metadata.EntityId, entries).Write());
                            }
                            await ApplyHologramsAsync(context, _holograms.ProcessMetadata(metadata)).ConfigureAwait(false);
                        }
                        break;
                    case MinecraftPacketIds.Clientbound.Team:
                        if (SetPlayerTeamPacket.Read(context.Payload).Mode == 4) context.Cancel();
                        else await ApplyHologramsAsync(context, _holograms.Process(context.PacketId, context.Payload)).ConfigureAwait(false);
                        break;
                    case MinecraftPacketIds.Clientbound.CustomPayload:
                        await _plugins.HandleAsync(this, context).ConfigureAwait(false);
                        break;
                    default:
                        await ApplyHologramsAsync(context, _holograms.Process(context.PacketId, context.Payload)).ConfigureAwait(false);
                        break;
                }
                return;
            }

            switch (context.PacketId)
            {
                case MinecraftPacketIds.Serverbound.CustomPayload:
                    await _plugins.HandleAsync(this, context).ConfigureAwait(false);
                    break;
                case MinecraftPacketIds.Serverbound.Position:
                case MinecraftPacketIds.Serverbound.PositionAndRotation:
                case MinecraftPacketIds.Serverbound.Rotation:
                case MinecraftPacketIds.Serverbound.OnGround:
                    if (_player is not null) _player.Apply(PlayerMovementPacket.Read(context.PacketId, context.Payload));
                    break;
                case MinecraftPacketIds.Serverbound.SwingArm:
                    (await GetSessionAsync().ConfigureAwait(false)).Cps.RecordLeftClick();
                    break;
                case MinecraftPacketIds.Serverbound.UseItem:
                    (await GetSessionAsync().ConfigureAwait(false)).Cps.RecordRightClick();
                    break;
                case MinecraftPacketIds.Serverbound.UseItemOn when _player is not null:
                    var item = UseItemOnPacket.Read(context.Payload);
                    var session = await GetSessionAsync().ConfigureAwait(false);
                    await SendPluginAsync(HeypixelChannels.Event, session.Encode(new HeypixelUseItemOnPacket(
                        (float)_player.X, (float)_player.Y, (float)_player.Z, item.Face,
                        item.Location.X + item.CursorPositionX, item.Location.Y + item.CursorPositionY,
                        item.Location.Z + item.CursorPositionZ, item.Location.X, item.Location.Y, item.Location.Z,
                        item.InsideBlock, _player.Yaw, _player.Pitch, item.Hand == 0))).ConfigureAwait(false);
                    break;
            }
        }
        finally { _gate.Release(); }
    }

    private async ValueTask JoinAsync()
    {
        await _gate.WaitAsync(connection.LifetimeToken).ConfigureAwait(false);
        try
        {
            var session = await GetSessionAsync().ConfigureAwait(false);
            session.BeginHandshake();
            if (session.TryMarkInitialReportSent())
                await SendPluginAsync(HeypixelChannels.Event,
                    session.Encode(session.CreateReport(new HeypixelChallengePacket(session.SessionId.ToEncodedString(),
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), HeypixelReportType.Info, string.Empty))))
                    .ConfigureAwait(false);
            _playing = true;
            if (!_timerStarted)
            {
                _timerStarted = true;
                BackgroundCompletion = RunTelemetryAsync();
            }
        }
        finally { _gate.Release(); }
    }

    private async Task ApplyHologramsAsync(PacketContext context, HeypixelHolograms.Update update)
    {
        if (update.Payload is not null) context.ReplacePayload(update.Payload);
        foreach (var packet in update.Before)
            await connection.SendAsync(PacketDirection.ClientBound, packet.PacketId, packet.Payload, connection.LifetimeToken).ConfigureAwait(false);
        if (update.After.Count == 0) return;
        context.AfterForward(async () =>
        {
            foreach (var packet in update.After)
                await connection.SendAsync(PacketDirection.ClientBound, packet.PacketId, packet.Payload, connection.LifetimeToken).ConfigureAwait(false);
        });
    }

    private async ValueTask ResumeAsync()
    {
        await _gate.WaitAsync(connection.LifetimeToken).ConfigureAwait(false);
        try { _playing = _session?.IsHandshakeComplete == true; }
        finally { _gate.Release(); }
    }

    internal async Task RespondAsync(ReadOnlyMemory<byte> payload)
    {
        var session = await GetSessionAsync().ConfigureAwait(false);
        var challenge = session.ReadChallenge(payload);
        if (challenge is null) return;
        if (challenge.ReportType == HeypixelReportType.Reflect)
        {
            await ReflectionMetadataRepository.InitializeAsync(connection.LifetimeToken).ConfigureAwait(false);
        }
        await SendPluginAsync(HeypixelChannels.Event, session.Encode(session.CreateReport(challenge))).ConfigureAwait(false);
    }

    internal Task SendPluginAsync(string channel, ReadOnlyMemory<byte> payload)
    {
        connection.LifetimeToken.ThrowIfCancellationRequested();
        var packetId = connection.ServerState switch
        {
            ConnectionState.Configuration => MinecraftPacketIds.Configuration.ServerboundCustomPayload,
            ConnectionState.Play => MinecraftPacketIds.Serverbound.CustomPayload,
            _ => throw new InvalidOperationException("Heypixel plugin message cannot be sent before configuration.")
        };
        var packet = new CustomPayloadPacket(channel, payload.ToArray());
        return connection.SendAsync(PacketDirection.ServerBound, packetId, packet.Write(), connection.LifetimeToken);
    }

    private async Task RunTelemetryAsync()
    {
        var token = connection.LifetimeToken;
        var lastLeft = 0;
        var lastRight = 0;
        var lastCps = -1L;
        var nextKeepAlive = NextKeepAlive(Environment.TickCount64);
        try
        {
            using var timer = new PeriodicTimer(HeypixelConstants.CpsPollInterval);
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await _gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (!_playing || _session is null || connection.ClientState != ConnectionState.Play
                        || connection.ServerState != ConnectionState.Play || connection.IsClosed) continue;
                    var now = Environment.TickCount64;
                    if (now >= nextKeepAlive)
                    {
                        await SendPluginAsync(HeypixelChannels.Event,
                            _session.Encode(new HeypixelKeepAlivePacket(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))).ConfigureAwait(false);
                        nextKeepAlive = NextKeepAlive(now);
                    }
                    var left = _session.Cps.GetLeftCps();
                    var right = _session.Cps.GetRightCps();
                    if ((left != lastLeft || right != lastRight) && IsCpsDue(lastCps, now))
                    {
                        await SendPluginAsync(HeypixelChannels.Event, _session.Encode(new HeypixelCpsPacket(left, right))).ConfigureAwait(false);
                        (lastLeft, lastRight, lastCps) = (left, right, now);
                    }
                }
                finally { _gate.Release(); }
            }
        }
        catch (Exception) when (token.IsCancellationRequested || connection.IsClosed) { }
        catch (Exception exception)
        {
            await connection.FailAsync(exception).ConfigureAwait(false);
        }
    }

    internal static bool IsCpsDue(long previous, long now) => previous < 0 || now - previous >= HeypixelConstants.CpsSendInterval.TotalMilliseconds;

    private static long NextKeepAlive(long now)
    {
        var interval = (long)HeypixelConstants.KeepAliveInterval.TotalMilliseconds;
        return now + interval - Math.Max(0, now - KeepAliveOrigin) % interval;
    }
}
