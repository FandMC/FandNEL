using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FandNEL.Core.Entities;
using FandNEL.Core.Entities.G79;
using FandNEL.Core.Entities.G79.NetGame;
using FandNEL.Core.Entities.G79.RentalGame;
using FandNEL.Core.Entities.WPFLauncher;
using FandNEL.Core.Utils.Cipher;
using FandNEL.Core.Utils.Http;
using Serilog;

namespace FandNEL.Core.Protocol;

public class G79 : IDisposable
{
	private static readonly string CoreBaseUrl = GetCoreBaseUrl();

	private readonly HttpWrapper _client = new HttpWrapper("https://g79mclobt.minecraft.cn", null, new HttpClientHandler
	{
		AutomaticDecompression = DecompressionMethods.GZip,
		ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
		UseProxy = false
	});

	private readonly HttpWrapper _core = new HttpWrapper(CoreBaseUrl, builder =>
	{
		builder.UserAgent("libhttpclient/1.0.0.0");
	}, new HttpClientHandler
	{
		AutomaticDecompression = DecompressionMethods.GZip
	}, new Version(2, 0));

	private readonly MgbSdk _mgbSdk = new MgbSdk("x19");

	private static string GetCoreBaseUrl()
	{
		const string fallbackUrl = "https://g79obtcore.minecraft.cn:8443";
		try
		{
			using HttpClient httpClient = new HttpClient();
			string response = httpClient.GetStringAsync("https://g79.update.netease.com/serverlist/adr_release.0.17.json").GetAwaiter().GetResult();
			using JsonDocument serverList = JsonDocument.Parse(response);
			if (serverList.RootElement.TryGetProperty("CoreServerUrl", out JsonElement coreServerUrl))
			{
				return coreServerUrl.GetString() ?? fallbackUrl;
			}
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Failed to query the current G79 core server URL");
		}
		return fallbackUrl;
	}

	public void Dispose()
	{
		_core.Dispose();
		_client.Dispose();
		GC.SuppressFinalize(this);
	}

	public FandNEL.Core.Entities.G79.EntityAuthenticationOtp AuthenticationOtp(string cookieRequest, string nexusToken)
	{
		return AuthenticationOtpAsync(cookieRequest, nexusToken).GetAwaiter().GetResult();
	}

	private static string ExtractCookie(string cookie)
	{
		try
		{
			return JsonSerializer.Deserialize<EntityX19CookieRequest>(cookie).Json;
		}
		catch (Exception)
		{
			return cookie;
		}
	}

	private async Task<FandNEL.Core.Entities.G79.EntityAuthenticationOtp> AuthenticationOtpAsync(string cookieRequest, string nexusToken)
	{
		string extractedCookie = ExtractCookie(cookieRequest);
		Log.Information("Try extracting cookie...");
		if (cookieRequest.Contains("4399com"))
		{
			await _mgbSdk.AuthSession(extractedCookie);
		}

		Log.Information("Building PeAuthData...");
		string encryptedHex;
		if (extractedCookie.StartsWith('{'))
		{
			encryptedHex = await G79AuthBuilder.BuildAndEncryptPeAuthData(extractedCookie);
		}
		else
		{
			encryptedHex = Convert.ToHexStringLower(G79HttpUtil.Encrypt(Encoding.UTF8.GetBytes(extractedCookie)));
		}

		string response = await (await _core.PostAsync("/pe-authentication", encryptedHex)).Content.ReadAsStringAsync();
		Log.Information("Decrypting response locally...");
		byte[]? decryptedBody = G79HttpUtil.Decrypt(Convert.FromHexString(response));
		if (decryptedBody == null)
		{
			throw new Exception("Failed to decrypt response: " + response);
		}
		byte[] jsonBody = G79HttpUtil.ExtractJson(decryptedBody) ?? throw new Exception("Failed to extract JSON response");
		string responseJson = Encoding.UTF8.GetString(jsonBody);
		Entity<JsonElement?>? authenticationResult = JsonSerializer.Deserialize<Entity<JsonElement?>>(responseJson);
		if (authenticationResult == null)
		{
			throw new Exception("Failed to deserialize: " + responseJson);
		}
		if (authenticationResult.Code != 0 || !authenticationResult.Data.HasValue)
		{
			throw new Exception("Failed: " + authenticationResult.Message);
		}
		return JsonSerializer.Deserialize<FandNEL.Core.Entities.G79.EntityAuthenticationOtp>(authenticationResult.Data.Value.GetRawText())
			?? throw new Exception("Failed to deserialize authentication response");
	}

	public Entity<EntityUserDetails> GetUserDetail(string userId, string userToken)
	{
		return GetUserDetailAsync(userId, userToken).GetAwaiter().GetResult();
	}

	private async Task<Entity<EntityUserDetails>> GetUserDetailAsync(string userId, string userToken)
	{
		string body = JsonSerializer.Serialize(new EntityQueryUserDetail
		{
			Version = new Version(2, 0)
		});
		string responseJson = await (await _core.PostAsync("/pe-user-detail/get", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
		return JsonSerializer.Deserialize<Entity<EntityUserDetails>>(responseJson) ?? throw new Exception("Failed to deserialize: " + responseJson);
	}

	public FandNEL.Core.Entities.G79.Entities<EntityNetGame> GetAvailableNetGames(string userId, string userToken)
	{
		return GetAvailableNetGamesAsync(userId, userToken).GetAwaiter().GetResult();
	}

	private async Task<FandNEL.Core.Entities.G79.Entities<EntityNetGame>> GetAvailableNetGamesAsync(string userId, string userToken)
	{
		string body = JsonSerializer.Serialize(new EntityNetGameRequest
		{
			Version = "2.12",
			ChannelId = 5
		});
		return JsonSerializer.Deserialize<FandNEL.Core.Entities.G79.Entities<EntityNetGame>>(await (await _client.PostAsync("/pe-game/query/get-list-v4", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync());
	}

	public Entity<EntityNetGameServerAddress> GetNetGameServerAddress(string userId, string userToken, string gameId)
	{
		return GetNetGameServerAddressAsync(userId, userToken, gameId).GetAwaiter().GetResult();
	}

	private async Task<Entity<EntityNetGameServerAddress>> GetNetGameServerAddressAsync(string userId, string userToken, string gameId)
	{
		string body = JsonSerializer.Serialize(new EntityNetGameServerAddressRequest
		{
			ItemId = gameId
		});
		return JsonSerializer.Deserialize<Entity<EntityNetGameServerAddress>>(await (await _client.PostAsync("/pe-game/query/get-server-address", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync());
	}

	public string GetAvailableRentalGames(string userId, string userToken, int offset)
	{
		return GetAvailableRentalGamesAsync(userId, userToken, offset).GetAwaiter().GetResult();
	}

	private async Task<string> GetAvailableRentalGamesAsync(string userId, string userToken, int offset)
	{
		string body = JsonSerializer.Serialize(new EntityRentalGameRequest
		{
			SortType = 0,
			OrderType = 0,
			Offset = offset
		});
		return await (await _client.PostAsync("/rental-server/query/available-by-sort-type", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public Entity<EntityRentalGameServerAddress> GetRentalGameServerAddress(string userId, string userToken, string gameId, string password = "")
	{
		return GetRentalGameServerAddressAsync(userId, userToken, gameId, password).GetAwaiter().GetResult();
	}

	private async Task<Entity<EntityRentalGameServerAddress>> GetRentalGameServerAddressAsync(string userId, string userToken, string gameId, string password = "")
	{
		string body = JsonSerializer.Serialize(new EntityRentalGameServerAddressRequest
		{
			ServerId = gameId,
			Password = password
		});
		return JsonSerializer.Deserialize<Entity<EntityRentalGameServerAddress>>(await (await _client.PostAsync("/rental-server-world-enter/get", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync());
	}

	public Entity<EntitySetNickName> SetNickName(string userId, string userToken, string nickName)
	{
		return SetNickNameAsync(userId, userToken, nickName).GetAwaiter().GetResult();
	}

	private async Task<Entity<EntitySetNickName>> SetNickNameAsync(string userId, string userToken, string nickName)
	{
		string body = JsonSerializer.Serialize(new EntitySetNickNameRequest
		{
			Name = nickName,
			EntityId = userId
		});
		return JsonSerializer.Deserialize<Entity<EntitySetNickName>>(await (await _client.PostAsync("/nickname-setting", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync());
	}

	public string GetMyDomainServers(string userId, string userToken)
	{
		return GetMyDomainServersAsync(userId, userToken).GetAwaiter().GetResult();
	}

	private async Task<string> GetMyDomainServersAsync(string userId, string userToken)
	{
		return await (await _client.PostAsync("/domain-server/get-my-server-details", "{}", builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public string GetOtherDomainServers(string userId, string userToken)
	{
		return GetOtherDomainServersAsync(userId, userToken).GetAwaiter().GetResult();
	}

	private async Task<string> GetOtherDomainServersAsync(string userId, string userToken)
	{
		return await (await _client.PostAsync("/domain-server/get-other-servers", "{}", builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public string JoinDomainServerWithInviteCode(string userId, string userToken, string code)
	{
		return JoinDomainServerWithInviteCodeAsync(userId, userToken, code).GetAwaiter().GetResult();
	}

	private async Task<string> JoinDomainServerWithInviteCodeAsync(string userId, string userToken, string code)
	{
		string body = JsonSerializer.Serialize(new { code });
		return await (await _client.PostAsync("/domain-server/join-server-with-invite-code", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public string DeleteOtherDomainServer(string userId, string userToken, string sid)
	{
		return DeleteOtherDomainServerAsync(userId, userToken, sid).GetAwaiter().GetResult();
	}

	private async Task<string> DeleteOtherDomainServerAsync(string userId, string userToken, string sid)
	{
		string body = JsonSerializer.Serialize(new { sid });
		return await (await _client.PostAsync("/domain-server/del-other-server", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public string EnterDomainServer(string userId, string userToken, string sid)
	{
		return EnterDomainServerAsync(userId, userToken, sid).GetAwaiter().GetResult();
	}

	private async Task<string> EnterDomainServerAsync(string userId, string userToken, string sid)
	{
		string body = JsonSerializer.Serialize(new { sid });
		return await (await _client.PostAsync("/domain-server/req-enter-domain-server", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public string GetDomainServerDetail(string userId, string userToken, string sid)
	{
		return GetDomainServerDetailAsync(userId, userToken, sid).GetAwaiter().GetResult();
	}

	private async Task<string> GetDomainServerDetailAsync(string userId, string userToken, string sid)
	{
		string body = JsonSerializer.Serialize(new { sid });
		return await (await _client.PostAsync("/domain-server/get-server-detail", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public string SearchLobbyItemByIdList(string userId, string userToken, List<string> itemIds)
	{
		return SearchLobbyItemByIdListAsync(userId, userToken, itemIds).GetAwaiter().GetResult();
	}

	private async Task<string> SearchLobbyItemByIdListAsync(string userId, string userToken, List<string> itemIds)
	{
		string body = JsonSerializer.Serialize(new { item_ids = itemIds });
		return await (await _client.PostAsync("/pe-item/query/search-lobby-by-id-list", body, builder =>
		{
			builder.AddHeader(TokenUtil.ComputeHttpRequestToken(builder.Url, builder.Body, userId, userToken));
		})).Content.ReadAsStringAsync();
	}

	public string GetEncryptionKeyListForGuests(string userId, string userToken, string deviceId, List<string> itemIds)
	{
		return GetEncryptionKeyListForGuestsAsync(userId, userToken, deviceId, itemIds).GetAwaiter().GetResult();
	}

	private async Task<string> GetEncryptionKeyListForGuestsAsync(string userId, string userToken, string deviceId, List<string> itemIds)
	{
		string body = JsonSerializer.Serialize(new { device_id = deviceId, item_ids = itemIds });
		string encryptedHex = Convert.ToHexString(G79HttpUtil.Encrypt(Encoding.UTF8.GetBytes(body)));
		string token = G79HttpUtil.ComputeDynamicToken("/pe-item/get-encryption-key-list-for-guests", body, userToken);
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, CoreBaseUrl + "/pe-item/get-encryption-key-list-for-guests");
		request.Content = new StringContent(encryptedHex, Encoding.UTF8, "application/json");
		request.Headers.Add("user-id", userId);
		request.Headers.Add("user-token", token);
		using HttpClient client = new HttpClient(new HttpClientHandler
		{
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
		});
		HttpResponseMessage response = await client.SendAsync(request);
		string hex = await response.Content.ReadAsStringAsync();
		byte[]? decrypted = G79HttpUtil.Decrypt(Convert.FromHexString(hex));
		if (decrypted == null)
		{
			throw new Exception("Failed to decrypt encryption key response");
		}
		byte[] json = G79HttpUtil.ExtractJson(decrypted) ?? decrypted;
		return Encoding.UTF8.GetString(json);
	}
}
