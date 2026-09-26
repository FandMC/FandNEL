#nullable enable
using System.Globalization;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FandNEL.Core.Connection.ChaCha;
using FandNEL.Core.Entities.Connection;
using FandNEL.Core.Extensions;
using FandNEL.Core.Services;
using FandNEL.Core.Utils;

namespace FandNEL.Core.Connection;

public sealed record JavaJoinRequest(string ServerId, string GameId, string GameVersion, string ModInfo, string NexusToken, int UserId, string UserToken);

public static class NetEaseConnection
{
    private static readonly byte[] TokenKey = [172, 36, 156, 105, 199, 44, 179, 180, 78, 192, 204, 108, 84, 58, 129, 149];
    private static readonly byte[] ChaChaNonce = "163 NetEase\n"u8.ToArray();
    private static readonly HttpClient ServerListClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<YggdrasilServer> RandomAuthServerAsync(CancellationToken cancellationToken = default)
    {
        var servers = await ServerListClient.GetFromJsonAsync<YggdrasilServer[]>(UrlsUtil.AuthServerUrl, cancellationToken).ConfigureAwait(false);
        var available = servers?.Where(server => !string.IsNullOrWhiteSpace(server.Ip) && server.Port is > 0 and <= 65535).ToArray();
        if (available is not { Length: > 0 }) throw new InvalidDataException("网易认证服务器列表为空或无效。");
        return available[Random.Shared.Next(available.Length)];
    }

    public static async Task AuthenticateAsync(JavaJoinRequest request, CancellationToken cancellationToken = default)
    {
        var server = await RandomAuthServerAsync(cancellationToken).ConfigureAwait(false);
        await AuthenticateAsync(request, server.Ip, server.Port, null, null, cancellationToken).ConfigureAwait(false);
    }

    public static async Task CreateAuthenticatorAsync(string serverId, string gameId, string gameVersion, string modInfo, string nexusToken, int userId, string userToken, Action handleSuccess, Func<string, string, int, string, byte[], string, byte[]>? buildEstablishing = null, Func<string, ChaChaOfSalsa, string, long, string, string, string, int, byte[], byte[]>? buildJoinServerMessage = null, CancellationToken cancellationToken = default)
    {
        var server = await RandomAuthServerAsync(cancellationToken).ConfigureAwait(false);
        await CreateAuthenticatorAsync(serverId, gameId, gameVersion, modInfo, nexusToken, userId, userToken, server.Ip, server.Port, handleSuccess, buildEstablishing, buildJoinServerMessage, cancellationToken).ConfigureAwait(false);
    }

    public static async Task CreateAuthenticatorAsync(string serverId, string gameId, string gameVersion, string modInfo, string nexusToken, int userId, string userToken, string authAddress, int authPort, Action handleSuccess, Func<string, string, int, string, byte[], string, byte[]>? buildEstablishing = null, Func<string, ChaChaOfSalsa, string, long, string, string, string, int, byte[], byte[]>? buildJoinServerMessage = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handleSuccess);
        await AuthenticateAsync(new JavaJoinRequest(serverId, gameId, gameVersion, modInfo, nexusToken, userId, userToken), authAddress, authPort, buildEstablishing, buildJoinServerMessage, cancellationToken).ConfigureAwait(false);
        handleSuccess();
    }

    private static async Task AuthenticateAsync(JavaJoinRequest request, string address, int port, Func<string, string, int, string, byte[], string, byte[]>? buildEstablishing, Func<string, ChaChaOfSalsa, string, long, string, string, string, int, byte[], byte[]>? buildJoinServerMessage, CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.GameId, NumberStyles.None, CultureInfo.InvariantCulture, out var gameId)) throw new ArgumentException("游戏 ID 必须为数字。", nameof(request));
        var tokenBytes = Encoding.ASCII.GetBytes(request.UserToken);
        if (tokenBytes.Length != TokenKey.Length) throw new ArgumentException("网易游戏令牌长度无效，请重新激活账号。", nameof(request));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        using var client = new TcpClient();
        using var api = new WebNexusApi(request.NexusToken);
        await client.ConnectAsync(address, port, token).ConfigureAwait(false);
        await using var stream = client.GetStream();
        using var details = await stream.ReadSteamWithInt16Async(token).ConfigureAwait(false);
        var context = details.ToArray();
        if (context.Length < 272) throw new InvalidDataException("网易认证握手缺少密钥或证书。");
        var remoteKey = context[..16];
        byte[] establishing;
        if (buildEstablishing is not null) establishing = buildEstablishing(request.NexusToken, request.GameVersion, request.UserId, request.UserToken, context, "netease");
        else
        {
            var json = await api.ComputeHandshakeBodyAsync(request.UserId, request.UserToken, Convert.ToBase64String(context), "netease", request.GameVersion, token).ConfigureAwait(false);
            establishing = Convert.FromBase64String(JsonSerializer.Deserialize<EntityHandshake>(json)?.HandshakeBody ?? throw new InvalidDataException("Codexus 未返回认证握手。"));
        }
        await stream.WriteAsync(establishing, token).ConfigureAwait(false);
        using var status = await stream.ReadSteamWithInt16Async(token).ConfigureAwait(false);
        if (status.ReadByte() != 0) throw new InvalidDataException("网易认证握手被拒绝。");
        var tokenKey = tokenBytes.Xor(TokenKey);
        var encrypt = new ChaChaOfSalsa(tokenKey.CombineWith(remoteKey), ChaChaNonce, true);
        var decrypt = new ChaChaOfSalsa(remoteKey.CombineWith(tokenKey), ChaChaNonce, false);
        byte[] join;
        if (buildJoinServerMessage is not null) join = buildJoinServerMessage(request.NexusToken, encrypt, request.ServerId, gameId, request.GameVersion, request.ModInfo, "netease", request.UserId, remoteKey);
        else
        {
            var json = await api.ComputeAuthenticationBodyAsync(request.ServerId, gameId, request.GameVersion, request.ModInfo, "netease", request.UserId, Convert.ToBase64String(remoteKey), token).ConfigureAwait(false);
            var authBody = JsonSerializer.Deserialize<AuthenticationBody>(json)?.AuthBody ?? throw new InvalidDataException("Codexus 未返回进服认证载荷。");
            join = encrypt.PackMessage(9, Convert.FromBase64String(authBody));
        }
        await stream.WriteAsync(join, token).ConfigureAwait(false);
        using var result = await stream.ReadSteamWithInt16Async(token).ConfigureAwait(false);
        var (messageType, authentication) = decrypt.UnpackMessage(result.ToArray());
        if (messageType != 9 || authentication.Length == 0 || authentication[0] != 0) throw new InvalidDataException("网易进服认证失败。");
    }

    private sealed record AuthenticationBody([property: System.Text.Json.Serialization.JsonPropertyName("authBody")] string AuthBody);
}
