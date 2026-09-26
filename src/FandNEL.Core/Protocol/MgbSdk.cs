using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using FandNEL.Core.Entities.MgbSdk;
using FandNEL.Core.Utils;
using FandNEL.Core.Utils.Http;

namespace FandNEL.Core.Protocol;

public class MgbSdk : IDisposable
{
	private readonly string _gameId;

	private readonly HttpWrapper _sdk;

	private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public MgbSdk(string gameId)
	{
		_gameId = gameId;
		_sdk = new HttpWrapper("https://mgbsdk.matrix.netease.com");
	}

	public void Dispose()
	{
		_sdk.Dispose();
		GC.SuppressFinalize(this);
	}

	public string GenerateSAuth(string deviceId, string userid, string sdkUid, string sessionId, string timestamp, string channel, string platform = "pc")
	{
		return JsonSerializer.Serialize(new EntityMgbSdkCookie
		{
			Ip = InternalQuery.Gw,
			AimInfo = InternalQuery.ToAimInfo(),
			AppChannel = channel,
			ClientLoginSn = deviceId,
			DeviceId = deviceId,
			GameId = _gameId,
			LoginChannel = channel,
			SdkUid = sdkUid,
			SessionId = sessionId,
			Timestamp = timestamp,
			Platform = platform,
			SourcePlatform = platform,
			Udid = StringGenerator.GenerateHexString(16).ToLower(),
			UserId = userid
		}, DefaultOptions);
	}

	public async Task AuthSession(string cookie)
	{
		HttpResponseMessage response = await _sdk.PostAsync("/" + _gameId + "/sdk/uni_sauth", cookie);
		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException(response.ReasonPhrase);
		}
		Dictionary<string, object> authentication = JsonSerializer.Deserialize<Dictionary<string, object>>(await response.Content.ReadAsStringAsync());
		if (authentication["code"].ToString() != "200")
		{
			throw new HttpRequestException("Status: " + authentication["status"]);
		}
	}
}
