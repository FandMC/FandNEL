using System.IO;
using System.Globalization;
using System.Text.Json;
using FandNEL.Core.Entities;
using FandNEL.Core.Entities.WPFLauncher.NetGame.Skin;
using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.Core.Protocol;

namespace FandNEL.Gateway.Management;

public sealed class BedrockAccountNotActivatedException()
    : InvalidOperationException("请先登录网易基岩版账号。");

/// <summary>
/// Codexus Gateway 的游戏目录消息适配层。它只负责把本地 WebSocket 消息映射到
/// 已有的网易协议客户端，避免在 WebSocket 层重复实现账号、签名和响应校验。
/// </summary>
public sealed class GameCatalogMessages(GatewayRuntime runtime, Func<G79> bedrock)
{
    private static readonly string[] UserIdKeys = ["user_id", "id"];
    private static readonly string[] GameIdKeys = ["game", "game_id", "entity_id", "item_id"];
    private static readonly string[] GameIdKeysWithId = ["game", "game_id", "entity_id", "item_id", "id"];
    private static readonly string[] RoleNameKeys = ["name", "role_name"];
    private static readonly string[] GameTypeKeys = ["type", "game_type"];

    public bool CanHandle(string type) => type is
        "net_games" or "net_games_search" or "rental_games" or "rental_games_search" or
        "net_games_detail" or "rental_games_detail" or "get_roles" or "create_role" or
        "pe_net_games" or "pe_net_games_search" or "pe_rental_games" or
        "g79_net_game_address" or "g79_rental_game_address" or "g79_get_nickname" or
        "g79_set_nickname" or "java_edition/skin_list" or "java_edition/skin_details" or
        "java_edition/purchase_skin" or "java_edition/buy_skin_result" or "java_edition/apply_skin";

    public Task<object> HandleAsync(string type, string? payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => Handle(type, payload, cancellationToken), cancellationToken);
    }

    private object Handle(string type, string? payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return type switch
        {
            "net_games" => GetNetGames(payload),
            "net_games_search" => SearchNetGames(payload),
            "rental_games" => GetRentalGames(payload),
            "rental_games_search" => SearchRentalGames(payload),
            "net_games_detail" => GetNetGameDetails(payload),
            "rental_games_detail" => GetRentalGameDetails(payload),
            "get_roles" => GetRoles(payload),
            "create_role" => CreateRole(payload),
            "pe_net_games" => GetBedrockNetGames(),
            "pe_net_games_search" => SearchBedrockNetGames(payload),
            "pe_rental_games" => GetBedrockRentalGames(payload),
            "g79_net_game_address" => GetBedrockNetGameAddress(payload),
            "g79_rental_game_address" => GetBedrockRentalGameAddress(payload),
            "g79_get_nickname" => GetBedrockNickname(payload),
            "g79_set_nickname" => SetBedrockNickname(payload),
            "java_edition/skin_list" => GetSkinList(payload),
            "java_edition/skin_details" => GetSkinDetails(payload),
            "java_edition/purchase_skin" => PurchaseSkin(payload),
            "java_edition/buy_skin_result" => BuySkinResult(payload),
            "java_edition/apply_skin" => ApplySkin(payload),
            _ => throw new NotSupportedException($"不支持的游戏目录消息：{type}")
        };
    }

    private object GetNetGames(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var result = runtime.Accounts.Service.Launcher.GetAvailableNetGames(
            user.UserId, user.AccessToken, IntValue(request, 0, "offset"), IntValue(request, 20, "length"));
        EnsureSuccess(result, "获取网络服务器列表失败");
        AttachTitleImages(runtime.Accounts.Service.Launcher, user, result.Data);
        return result;
    }

    private object SearchNetGames(string? payload)
    {
        var user = JavaUser();
        var result = runtime.Accounts.Service.Launcher.QueryNetGameWithKeyword(user.UserId, user.AccessToken, ReadString(payload));
        EnsureSuccess(result, "搜索网络服务器失败");
        AttachTitleImages(runtime.Accounts.Service.Launcher, user, result?.Data);
        return result!;
    }

    private object GetRentalGames(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var result = runtime.Accounts.Service.Launcher.GetRentalGameList(
            user.UserId, user.AccessToken, IntValue(request, 0, "offset"));
        EnsureSuccess(result, "获取租赁服务器列表失败");
        return result;
    }

    private object SearchRentalGames(string? payload)
    {
        var user = JavaUser();
        var result = runtime.Accounts.Service.Launcher.SearchRentalGameByName(user.UserId, user.AccessToken, ReadString(payload));
        EnsureSuccess(result, "搜索租赁服务器失败");
        return result;
    }

    private object GetNetGameDetails(string? payload)
    {
        var user = JavaUser();
        var gameId = RequiredString(payload, "游戏 ID");
        var launcher = runtime.Accounts.Service.Launcher;
        var detail = launcher.QueryNetGameDetailById(user.UserId, user.AccessToken, gameId);
        var address = launcher.GetNetGameServerAddress(user.UserId, user.AccessToken, gameId);
        EnsureSuccess(detail, "获取网络服务器详情失败");
        EnsureSuccess(address, "获取网络服务器地址失败");
        var data = detail.Data ?? throw new InvalidDataException("网络服务器详情为空。");
        var endpoint = address.Data ?? throw new InvalidDataException("网络服务器地址为空。");
        data.ServerAddress = endpoint.Ip;
        data.ServerPort = endpoint.Port;
        return data;
    }

    private object GetRentalGameDetails(string? payload)
    {
        var user = JavaUser();
        var (gameId, password) = SplitGameAndPassword(payload);
        var launcher = runtime.Accounts.Service.Launcher;
        var detail = launcher.GetRentalGameDetails(user.UserId, user.AccessToken, gameId);
        var address = launcher.GetRentalGameServerAddress(user.UserId, user.AccessToken, gameId, password);
        EnsureSuccess(detail, "获取租赁服务器详情失败");
        EnsureSuccess(address, "获取租赁服务器地址失败");
        var data = detail.Data ?? throw new InvalidDataException("租赁服务器详情为空。");
        var endpoint = address.Data ?? throw new InvalidDataException("租赁服务器地址为空。");
        data.ServerIp = endpoint.McServerHost;
        data.ServerPort = endpoint.McServerPort;
        return data;
    }

    private object GetRoles(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var gameId = ResolveGameId(request, "游戏 ID");
        var kind = StringValue(request, GameTypeKeys);
        object roles = kind switch
        {
            "net_game" => RequireData(runtime.Accounts.Service.Launcher.QueryNetGameCharacters(user.UserId, user.AccessToken, gameId), "获取网络服务器角色失败"),
            "rental_game" => RequireData(runtime.Accounts.Service.Launcher.GetRentalGameRolesList(user.UserId, user.AccessToken, gameId), "获取租赁服务器角色失败"),
            _ => throw new ArgumentException($"未知的游戏类型：{kind}")
        };
        // Codexus Gateway 的原始协议直接返回角色数组；前端和已有插件都按这个
        // 形状读取，不能在本地适配层额外包一层 type/entity。
        return roles;
    }

    private object CreateRole(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var gameId = ResolveGameId(request, "游戏 ID");
        var name = RequiredString(request, "角色名", RoleNameKeys);
        switch (StringValue(request, GameTypeKeys))
        {
            case "net_game":
                runtime.Accounts.Service.Launcher.CreateCharacter(user.UserId, user.AccessToken, gameId, name);
                break;
            case "rental_game":
                EnsureSuccess(runtime.Accounts.Service.Launcher.AddRentalGameRole(user.UserId, user.AccessToken, gameId, name), "创建租赁服务器角色失败");
                break;
            default:
                throw new ArgumentException("未知的游戏类型。");
        }
        // 创建结果通过 create_role/success_notification 推送，随后由网关发送 get_roles。
        return string.Empty;
    }

    private object GetBedrockNetGames()
    {
        var user = BedrockUser();
        var result = bedrock().GetAvailableNetGames(user.UserId, user.AccessToken);
        EnsureSuccess(result, "获取基岩版网络服务器失败");
        var games = result.Data?.Res ?? throw new InvalidDataException("基岩版网络服务器响应为空。");
        return new { entities = games, total = games.Count };
    }

    private object SearchBedrockNetGames(string? payload)
    {
        // G79 当前协议没有独立的关键字搜索接口；服务器列表接口的结果是唯一可用数据源。
        var keyword = ReadString(payload).Trim();
        var user = BedrockUser();
        var response = bedrock().GetAvailableNetGames(user.UserId, user.AccessToken);
        EnsureSuccess(response, "搜索基岩版网络服务器失败");
        var games = response.Data?.Res ?? throw new InvalidDataException("基岩版网络服务器响应为空。");
        var filtered = string.IsNullOrEmpty(keyword)
            ? games
            : games.Where(item => item.ResName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Brief.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        return new { entities = filtered, total = filtered.Count };
    }

    private object GetBedrockRentalGames(string? payload)
    {
        var request = ReadObject(payload);
        var user = BedrockUser();
        var response = bedrock().GetAvailableRentalGames(user.UserId, user.AccessToken, IntValue(request, 0, "offset"));
        return ParseAndValidateRaw(response, "获取基岩版租赁服务器失败");
    }

    private object GetBedrockNetGameAddress(string? payload)
    {
        var request = ReadObject(payload);
        var gameId = ResolveGameId(request, "游戏 ID", allowTopLevelId: true);
        var user = BedrockUser();
        var result = bedrock().GetNetGameServerAddress(user.UserId, user.AccessToken, gameId);
        EnsureSuccess(result, "获取基岩版网络服务器地址失败");
        var endpoint = result.Data ?? throw new InvalidDataException("基岩版网络服务器地址为空。");
        return new { host = endpoint.Host, port = endpoint.Port, user_id = user.UserId };
    }

    private object GetBedrockRentalGameAddress(string? payload)
    {
        var request = ReadObject(payload);
        var gameId = ResolveGameId(request, "游戏 ID", allowTopLevelId: true);
        var password = StringValue(request, ["password"]);
        var users = runtime.BedrockUsers.GetAvailableUserAccounts();
        if (users.Count == 0) throw new BedrockAccountNotActivatedException();

        Entity<FandNEL.Core.Entities.G79.RentalGame.EntityRentalGameServerAddress>? last = null;
        foreach (var user in users)
        {
            var result = bedrock().GetRentalGameServerAddress(user.UserId, user.AccessToken, gameId, password);
            last = result;
            if (result.Code == 0 && result.Data is { Host: not null, Port: > 0 } endpoint)
                return new { mcserver_host = endpoint.Host, mcserver_port = endpoint.Port, user_id = user.UserId };
        }
        EnsureSuccess(last, "获取基岩版租赁服务器地址失败");
        throw new InvalidDataException("基岩版租赁服务器地址为空。");
    }

    private object GetBedrockNickname(string? payload)
    {
        var user = BedrockUser(RequiredString(payload, "用户 ID"));
        var result = bedrock().GetUserDetail(user.UserId, user.AccessToken);
        EnsureSuccess(result, "获取基岩版昵称失败");
        return result.Data?.Name ?? throw new InvalidDataException("基岩版用户信息为空。");
    }

    private object SetBedrockNickname(string? payload)
    {
        var request = ReadObject(payload);
        var user = BedrockUser(RequiredString(request, "用户 ID", UserIdKeys));
        var name = RequiredString(request, "昵称", ["new", "name"]);
        var result = bedrock().SetNickName(user.UserId, user.AccessToken, name);
        EnsureSuccess(result, "设置基岩版昵称失败");
        return new { code = 0, message = "成功设置昵称" };
    }

    private object GetSkinList(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var launcher = runtime.Accounts.Service.Launcher;
        var available = launcher.GetFreeSkinList(user.UserId, user.AccessToken,
            IntValue(request, 0, "offset"), IntValue(request, 20, "length"));
        EnsureSuccess(available, "获取皮肤列表失败");
        if (available.Data is not { Length: > 0 })
            return new { entities = Array.Empty<EntitySkin>(), total = available.Total };

        var details = launcher.GetSkinDetails(user.UserId, user.AccessToken, available);
        EnsureSuccess(details, "获取皮肤详情失败");
        return new { entities = details.Data ?? Array.Empty<EntitySkin>(), total = available.Total };
    }

    private object GetSkinDetails(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var itemId = RequiredString(request, "皮肤 ID", ["item_id", "entity_id"]);
        var details = runtime.Accounts.Service.Launcher.GetSkinDetails(user.UserId, user.AccessToken,
            new Entities<EntitySkin> { Data = [new EntitySkin { EntityId = itemId }] });
        EnsureSuccess(details, "获取皮肤详情失败");
        var skin = details.Data?.FirstOrDefault() ?? throw new InvalidDataException("皮肤详情为空。");
        return new { is_has = skin.IsHas };
    }

    private object PurchaseSkin(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var itemId = RequiredString(request, "皮肤 ID", ["item_id", "entity_id"]);
        var result = runtime.Accounts.Service.Launcher.PurchaseSkin(user.UserId, user.AccessToken, itemId);
        EnsureSuccess(result, "购买皮肤失败");
        return result.Data ?? throw new InvalidDataException("购买皮肤响应为空。");
    }

    private object BuySkinResult(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var orderId = RequiredString(request, "订单 ID", ["orderid", "order_id"]);
        return ParseAndValidateRaw(runtime.Accounts.Service.Launcher.BuyItemResult(
            user.UserId, user.AccessToken, orderId, IntValue(request, 0, "buy_type")), "查询皮肤购买结果失败");
    }

    private object ApplySkin(string? payload)
    {
        var request = ReadObject(payload);
        var user = JavaUser(StringValue(request, UserIdKeys));
        var itemId = RequiredString(request, "皮肤 ID", ["item_id", "entity_id"]);
        return runtime.Accounts.Service.Launcher.SetSkin(user.UserId, user.AccessToken, itemId);
    }

    private ManagedAvailableUser JavaUser(string? userId = null)
    {
        var available = string.IsNullOrWhiteSpace(userId)
            ? runtime.JavaUsers.GetLastAvailableUser()
            : runtime.JavaUsers.GetAvailableUser(userId);
        if (available is not null) return available;

        // Codexus Gateway 会在读取服务器目录前激活磁盘中的 cookie 账号。
        // FandNEL 的 token 只存在内存，因此重启后必须在第一次业务请求时恢复。
        var candidates = string.IsNullOrWhiteSpace(userId)
            ? runtime.JavaUsers.GetUsersNoDetails()
            : runtime.JavaUsers.GetUsersNoDetails().Where(user => user.UserId == userId);
        Exception? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                runtime.Accounts.GetSessionAsync(candidate.UserId).GetAwaiter().GetResult();
                available = runtime.JavaUsers.GetAvailableUser(candidate.UserId);
                if (available is not null) return available;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }
        }

        throw new InvalidOperationException("请先登录网易 Java 账号。", lastError);
    }

    private ManagedAvailableUser BedrockUser(string? userId = null)
    {
        var user = string.IsNullOrWhiteSpace(userId)
            ? runtime.BedrockUsers.GetLastAvailableUser()
            : runtime.BedrockUsers.GetAvailableUser(userId);
        return user ?? throw new BedrockAccountNotActivatedException();
    }

    private static void AttachTitleImages(WPFLauncher launcher, ManagedAvailableUser user, EntityNetGameItem[]? games)
    {
        if (games is not { Length: > 0 }) return;
        var details = launcher.QueryNetGameItemByIds(user.UserId, user.AccessToken, games.Select(game => game.EntityId).ToArray());
        EnsureSuccess(details, "获取网络服务器图片失败");
        var images = details.Data ?? Array.Empty<EntityQueryNetGameItem>();
        for (var index = 0; index < games.Length && index < images.Length; index++)
            games[index].TitleImageUrl = images[index].TitleImageUrl;
    }

    private static T[] RequireData<T>(Entities<T> response, string message)
    {
        EnsureSuccess(response, message);
        return response.Data ?? Array.Empty<T>();
    }

    private static void EnsureSuccess(EntityResponse? response, string message)
    {
        if (response is null) throw new InvalidDataException($"{message}：服务端返回为空。");
        if (response.Code != 0) throw new InvalidOperationException($"{message}：{response.Message}（{response.Code}）");
    }

    private static JsonElement ParseAndValidateRaw(string response, string message)
    {
        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("code", out var code) && code.TryGetInt32(out var status) && status != 0)
        {
            var detailMessage = root.TryGetProperty("message", out var detail) ? detail.GetString() : string.Empty;
            throw new InvalidOperationException($"{message}：{detailMessage}（{status}）");
        }
        return root.Clone();
    }

    private static JsonElement ReadObject(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("消息参数不能为空。");
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("消息参数必须是 JSON 对象。");
        return document.RootElement.Clone();
    }

    private static string ReadString(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString() ?? string.Empty
                : payload;
        }
        catch (JsonException) { return payload; }
    }

    private static string RequiredString(string? payload, string label)
    {
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException($"{label}不能为空。");
        return ReadString(payload).Trim();
    }

    private static string RequiredString(JsonElement objectValue, string label, IReadOnlyList<string> keys)
    {
        var value = StringValue(objectValue, keys);
        return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。") : value;
    }

    private static string ResolveGameId(JsonElement request, string label, bool allowTopLevelId = false)
    {
        var gameId = StringValue(request, allowTopLevelId ? GameIdKeysWithId : GameIdKeys);
        if (!string.IsNullOrWhiteSpace(gameId)) return gameId;

        if (request.TryGetProperty("details", out var details))
        {
            if (details.ValueKind == JsonValueKind.Object)
                gameId = StringValue(details, allowTopLevelId ? GameIdKeysWithId : GameIdKeys);
            else if (details.ValueKind == JsonValueKind.String)
            {
                try
                {
                    using var document = JsonDocument.Parse(details.GetString() ?? string.Empty);
                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                        gameId = StringValue(document.RootElement, allowTopLevelId ? GameIdKeysWithId : GameIdKeys);
                }
                catch (JsonException) { }
            }
        }

        return string.IsNullOrWhiteSpace(gameId)
            ? throw new ArgumentException($"{label}不能为空。")
            : gameId;
    }

    private static string StringValue(JsonElement objectValue, IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (!objectValue.TryGetProperty(key, out var value)) continue;
            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        return string.Empty;
    }

    private static int IntValue(JsonElement objectValue, int fallback, params string[] keys)
    {
        var text = StringValue(objectValue, keys);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static (string GameId, string? Password) SplitGameAndPassword(string? payload)
    {
        if (payload is null) throw new ArgumentException("游戏 ID 不能为空。");
        var parts = payload.Split(':', 2, StringSplitOptions.None);
        var gameId = parts[0].Trim();
        if (gameId.Length == 0) throw new ArgumentException("游戏 ID 不能为空。");
        return (gameId, parts.Length == 2 ? parts[1] : null);
    }

}
