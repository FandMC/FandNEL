using System.IO;
using FandNEL.Accounts;
using FandNEL.Core.Entities;
using FandNEL.Core.Entities.WPFLauncher.NetGame;
using FandNEL.Core.Protocol;
using FandNEL.Core.Utils;

namespace FandNEL.Gateway;

public enum GameKind { Network, Rental }
public sealed record GameEntry(string Id, string Name, GameKind Kind);
public sealed record GameVersionChoice(string Name, EnumGameVersion Version);
public sealed record GameDetails(GameEntry Game, string Host, int Port, IReadOnlyList<GameVersionChoice> Versions);
public sealed record GameRole(string Name);

/// <summary>Gateway 的游戏目录与角色操作；所有数据来自 Codexus 使用的网易接口。</summary>
public sealed class GameCatalogService(WPFLauncher launcher, AccountService accounts)
{
    public async Task<IReadOnlyList<GameEntry>> ListAsync(string userId, GameKind kind, string keyword,
        int offset, CancellationToken cancellationToken)
    {
        var session = await accounts.GetSessionAsync(userId, cancellationToken).ConfigureAwait(false);
        return await Task.Run<IReadOnlyList<GameEntry>>(() =>
        {
            if (kind == GameKind.Network)
            {
                var result = string.IsNullOrWhiteSpace(keyword)
                    ? launcher.GetAvailableNetGames(userId, session.Token, offset, 20)
                    : launcher.QueryNetGameWithKeyword(userId, session.Token, keyword);
                EnsureSuccess(result);
                return result!.Data.Select(g => new GameEntry(g.EntityId, g.Name, kind)).ToArray();
            }
            var rental = string.IsNullOrWhiteSpace(keyword)
                ? launcher.GetRentalGameList(userId, session.Token, offset)
                : launcher.SearchRentalGameByName(userId, session.Token, keyword);
            EnsureSuccess(rental);
            return rental.Data.Select(g => new GameEntry(g.EntityId, g.Name, kind)).ToArray();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameDetails> DetailsAsync(string userId, GameEntry game, string? password, CancellationToken cancellationToken)
    {
        var session = await accounts.GetSessionAsync(userId, cancellationToken).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            if (game.Kind == GameKind.Network)
            {
                var detail = launcher.QueryNetGameDetailById(userId, session.Token, game.Id);
                var address = launcher.GetNetGameServerAddress(userId, session.Token, game.Id);
                EnsureSuccess(detail);
                EnsureSuccess(address);
                var data = detail.Data ?? throw new InvalidDataException("服务器详情为空。");
                var endpoint = address.Data ?? throw new InvalidDataException("服务器地址为空。");
                return new GameDetails(game, endpoint.Ip, endpoint.Port,
                    data.McVersionList.Select(v => new GameVersionChoice(v.Name, GameVersionConverter.Convert(v.McVersionId)))
                        .Where(v => v.Version != EnumGameVersion.NONE && (uint)v.Version < 100000000).ToArray());
            }
            var rental = launcher.GetRentalGameDetails(userId, session.Token, game.Id);
            var rentalAddress = launcher.GetRentalGameServerAddress(userId, session.Token, game.Id,
                string.IsNullOrWhiteSpace(password) ? null : password);
            EnsureSuccess(rental);
            EnsureSuccess(rentalAddress);
            var rentalData = rental.Data ?? throw new InvalidDataException("租赁服详情为空。");
            var target = rentalAddress.Data ?? throw new InvalidDataException("租赁服地址为空。");
            if (!Enum.TryParse<EnumGameVersion>("V_" + rentalData.McVersion.Replace('.', '_'), out var version))
                throw new NotSupportedException($"不支持该租赁服版本：{rentalData.McVersion}");
            return new GameDetails(game, target.McServerHost, target.McServerPort,
                [new GameVersionChoice(rentalData.McVersion, version)]);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameRole>> RolesAsync(string userId, GameEntry game, CancellationToken cancellationToken)
    {
        var session = await accounts.GetSessionAsync(userId, cancellationToken).ConfigureAwait(false);
        return await Task.Run<IReadOnlyList<GameRole>>(() =>
        {
            if (game.Kind == GameKind.Network)
            {
                var roles = launcher.QueryNetGameCharacters(userId, session.Token, game.Id);
                EnsureSuccess(roles);
                return roles.Data.Select(r => new GameRole(r.Name)).ToArray();
            }
            var rental = launcher.GetRentalGameRolesList(userId, session.Token, game.Id);
            EnsureSuccess(rental);
            return rental.Data.Select(r => new GameRole(r.Name)).ToArray();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateRoleAsync(string userId, GameEntry game, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var session = await accounts.GetSessionAsync(userId, cancellationToken).ConfigureAwait(false);
        await Task.Run(() =>
        {
            if (game.Kind == GameKind.Network) launcher.CreateCharacter(userId, session.Token, game.Id, name);
            else EnsureSuccess(launcher.AddRentalGameRole(userId, session.Token, game.Id, name));
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureSuccess(EntityResponse? response)
    {
        if (response is null) throw new InvalidDataException("服务端返回了空响应。");
        if (response.Code != 0) throw new InvalidOperationException($"{response.Message}（{response.Code}）");
    }
}
