using System.Text.Json;
using System.Threading.Tasks;
using FandNEL.Core.Entities.InterConn;
using FandNEL.Core.Utils.Cipher;
using FandNEL.Core.Utils.Http;
using Serilog;

namespace FandNEL.Core.Protocol;

public static class InterConn
{
	private static readonly HttpWrapper Core = new HttpWrapper("https://x19obtcore.nie.netease.com:8443", builder =>
	{
		builder.UserAgent(WPFLauncher.GetUserAgent());
	});

	public static async Task LoginStart(string entityId, string entityToken)
	{
		await (await Core.PostAsync("/interconn/web/game-play-v2/login-start", "{\"strict_mode\":true}", builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, entityId, entityToken));
		})).Content.ReadAsByteArrayAsync();
	}

	public static async Task GameStart(string entityId, string entityToken, string gameId)
	{
		Log.Debug("GameStart response: {0}", await (await Core.PostAsync("/interconn/web/game-play-v2/start", JsonSerializer.Serialize(new InterConnGameStart
		{
			GameId = gameId,
			ItemList = ["10000"]
		}), builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, entityId, entityToken));
		})).Content.ReadAsStringAsync());
	}
}
