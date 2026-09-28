using System.Text;
using System.Text.Json;
using FandNEL.Proxy.Packet.Heypixel;

namespace FandNEL.Proxy.Heypixel;

/// <summary>每条游戏连接的挑战状态和加密上下文，不依赖 Codexus SDK。</summary>
internal sealed class AntiCheatSession
{
    private readonly SaltedDesCipher _cipher;
    private int _challengeAccepted;
    private int _initialReportSent;
    private long _nextSentinelRefresh;

    internal AntiCheatSession(Uuid128 sessionId, HeypixelClientIdentity identity)
    {
        SessionId = sessionId;
        _cipher = new SaltedDesCipher(sessionId.ToString());
        Environment = new ClientFingerprintBuilder(identity, _cipher);
        RefreshSentinels();
    }

    internal Uuid128 SessionId { get; }
    internal ClientFingerprintBuilder Environment { get; }
    internal CpsTracker Cps { get; } = new();
    internal int BlacklistedClassCount { get; } = Random.Shared.Next(77_772, 81_079);
    internal string BlacklistedModuleName { get; private set; } = string.Empty;
    internal string BlacklistedModuleVersion { get; private set; } = string.Empty;
    internal string BlacklistedClassName { get; private set; } = string.Empty;
    internal bool IsHandshakeComplete { get; private set; }
    internal bool IsChallengeAccepted => Volatile.Read(ref _challengeAccepted) != 0;
    internal bool TryMarkInitialReportSent() => Interlocked.Exchange(ref _initialReportSent, 1) == 0;
    internal void AcceptChallenge() => Volatile.Write(ref _challengeAccepted, 1);
    internal byte[] Encrypt(string text) => Encrypt(Encoding.UTF8.GetBytes(text));
    internal byte[] Encrypt(byte[] payload) => _cipher.Encrypt(payload);
    internal byte[] Decrypt(byte[] payload) => _cipher.Decrypt(payload);

    internal byte[] Encode(IHeypixelPacket packet) => HeypixelPacketCodec.Encode(
        packet, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), SessionId, _cipher, IsChallengeAccepted);

    internal HeypixelChallengePacket? ReadChallenge(ReadOnlyMemory<byte> payload)
    {
        var challenge = HeypixelChallengePacket.Read(payload, _cipher);
        if (challenge is null) return null;
        if (challenge.ReportType == HeypixelReportType.Reflect) _ = ReadReflectionPath(challenge.Payload);
        // 只有消息和业务字段都校验成功后，才启用后续报告保护。
        AcceptChallenge();
        return challenge;
    }

    internal HeypixelReportPacket CreateReport(HeypixelChallengePacket challenge)
    {
        HeypixelReportData data = challenge.ReportType switch
        {
            HeypixelReportType.Info => Environment.BuildInfoReport(),
            HeypixelReportType.BlackClass => new BlackClassReportData(BlacklistedClassCount, BlacklistedClassName),
            HeypixelReportType.BlackModule => new BlackModuleReportData(LoadedModuleFingerprints.Count,
                BlacklistedModuleName, BlacklistedModuleVersion),
            HeypixelReportType.Reflect => BuildReflectionReport(challenge.Payload),
            _ => throw new InvalidDataException("Unknown Heypixel report type.")
        };
        return new HeypixelReportPacket(SessionId, challenge.Key, challenge.Timestamp, data);
    }

    private ReflectionReportData BuildReflectionReport(string payload)
    {
        var path = ReadReflectionPath(payload);
        var classPath = string.Join('.', path[..^2]);
        var metadata = ReflectionMetadataRepository.TryGetField(
            ReflectionMetadataRepository.NormalizeName(classPath),
            ReflectionMetadataRepository.NormalizeName(path[^2]),
            ReflectionMetadataRepository.NormalizeName(path[^1]));
        var content = metadata?.Content ?? "CheckError:" + classPath;
        return new ReflectionReportData(metadata?.HashCode ?? content.JavaHashCode(),
            Convert.ToBase64String(Encrypt(content)));
    }

    private static string[] ReadReflectionPath(string payload)
    {
        ReflectionCheckReport report;
        try { report = ReflectionCheckResultParser.Parse(payload); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("Invalid Heypixel reflection request.");
        }
        var path = report.AccessPath?.Split('.');
        if (report.FinalAction != "getStaticField" || path is not { Length: >= 3 } || path.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Unsupported Heypixel reflection request.");
        return path;
    }

    internal void BeginHandshake()
    {
        IsHandshakeComplete = true;
        var now = System.Environment.TickCount64;
        if (now < _nextSentinelRefresh) return;
        _nextSentinelRefresh = now + (long)HeypixelConstants.SentinelRefreshInterval.TotalMilliseconds;
        RefreshSentinels();
    }

    private void RefreshSentinels()
    {
        BlacklistedModuleName = _cipher.EncryptString(SessionId.ToString());
        BlacklistedModuleVersion = _cipher.EncryptString("0");
        BlacklistedClassName = _cipher.EncryptString("0");
    }
}
