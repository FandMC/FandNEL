using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FandNEL.Core.Utils.Cipher;

public static class G79AuthBuilder
{
	private const string FixedEngineVersion = "3.9.5.297103";

	private const string FixedPatchVersion = "3.9.9.297335";

	private const string G79LibraryHash = "18673c83b014387de63b3167bb5fe29c";

	private const string G79PatchHash = "1e84106f5336cfdb633e97f81371c2052b3e7ca013bb30a74d822579860c042b";

	private const string G79AndroidMessageTag = "dashen_cloudgame";

	private const string G79AndroidStep = "695616851";

	private const string G79AndroidStep2 = "2146985406";

	public static (string EngineVersion, string PatchVersion) GetVersionsSync()
	{
		Task<string> engineVersionTask = GetEngineVersionAsync();
		Task<string> patchVersionTask = GetPatchVersionAsync();
		Task.WhenAll(engineVersionTask, patchVersionTask).GetAwaiter().GetResult();
		return (engineVersionTask.Result, patchVersionTask.Result);
	}

	public static async Task<string> BuildAndEncryptPeAuthData(string cookieJson)
	{
		using JsonDocument cookieDocument = JsonDocument.Parse(cookieJson);
		JsonElement root = cookieDocument.RootElement;
		JsonElement authentication;
		string macAddress;
		string ram;
		string rom;
		int isGuest;
		int emulator;

		if (root.TryGetProperty("sauth_json", out JsonElement authenticationProperty))
		{
			if (authenticationProperty.ValueKind == JsonValueKind.String)
			{
				string authenticationJson = authenticationProperty.GetString() ?? "{}";
				using JsonDocument authenticationDocument = JsonDocument.Parse(authenticationJson);
				authentication = authenticationDocument.RootElement.Clone();
			}
			else if (authenticationProperty.ValueKind == JsonValueKind.Object)
			{
				authentication = authenticationProperty.Clone();
			}
			else
			{
				throw new ArgumentException("G79 cookie field sauth_json must be a string or object.", nameof(cookieJson));
			}
			macAddress = GetString(root, "mac_addr", "02:00:00:00:00:00");
			ram = GetString(root, "ram", "4294967296");
			rom = GetString(root, "rom", string.Empty);
			isGuest = GetBool(root, "is_guest") ? 1 : 0;
			emulator = GetInt(root, "emulator", 0);
		}
		else if (root.ValueKind == JsonValueKind.Object
			&& root.TryGetProperty("sdkuid", out _)
			&& root.TryGetProperty("sessionid", out _))
		{
			authentication = root.Clone();
			macAddress = "02:00:00:00:00:00";
			ram = "4294967296";
			rom = string.Empty;
			isGuest = 0;
			emulator = 0;
		}
		else
		{
			throw new ArgumentException("G79 cookie is missing sauth_json or direct sauth fields.", nameof(cookieJson));
		}

		string sdkUserId = GetRequiredString(authentication, "sdkuid");
		string sessionId = GetRequiredString(authentication, "sessionid");
		string udid = GetRequiredString(authentication, "udid");
		string deviceId = GetRequiredString(authentication, "deviceid");
		string gameId = GetString(authentication, "gameid", "x19");
		string loginChannel = GetString(authentication, "login_channel", string.Empty);
		string appChannel = GetString(authentication, "app_channel", "netease");
		string platform = GetString(authentication, "platform", "ad");
		string sdkVersion = GetString(authentication, "sdk_version", "5.9.0");
		string aimInfo = GetString(authentication, "aim_info", string.Empty);
		string realName = GetString(authentication, "realname", string.Empty);
		string gasToken = GetString(authentication, "gas_token", string.Empty);
		string ipAddress = GetString(authentication, "ip", "127.0.0.1");
		string sourceAppChannel = GetString(authentication, "source_app_channel", appChannel);
		string sourcePlatform = GetString(authentication, "source_platform", platform);
		string getAccessToken = GetString(authentication, "get_access_token", "1");
		int isUniSdkGuest = GetInt(authentication, "is_unisdk_guest", 0);

		if (string.IsNullOrWhiteSpace(loginChannel))
		{
			loginChannel = appChannel;
		}
		if (string.IsNullOrWhiteSpace(aimInfo))
		{
			aimInfo = "{\"aim\":\"127.0.0.1\",\"country\":\"CN\",\"tz\":\"+0800\",\"tzid\":\"\",\"celluar_ip\":\"\",\"operator\":\"\",\"is_vpn_enabled\":\"false\"}";
		}
		if (string.IsNullOrWhiteSpace(sdkVersion))
		{
			sdkVersion = "5.9.0";
		}
		if (string.IsNullOrWhiteSpace(ipAddress))
		{
			ipAddress = "127.0.0.1";
		}
		if (string.IsNullOrWhiteSpace(gameId))
		{
			gameId = "x19";
		}

		string seed = Guid.NewGuid().ToString().ToLowerInvariant();
		Dictionary<string, object> authenticationPayload = new Dictionary<string, object>
		{
			["aim_info"] = aimInfo,
			["app_channel"] = appChannel,
			["client_login_sn"] = RandomHexUpper(16),
			["deviceid"] = deviceId,
			["gameid"] = gameId,
			["gas_token"] = gasToken,
			["get_access_token"] = getAccessToken,
			["ip"] = ipAddress,
			["is_unisdk_guest"] = isUniSdkGuest,
			["login_channel"] = loginChannel,
			["platform"] = platform,
			["sdk_version"] = sdkVersion,
			["sdkuid"] = sdkUserId,
			["sessionid"] = sessionId,
			["source_app_channel"] = sourceAppChannel,
			["source_platform"] = sourcePlatform,
			["step"] = G79AndroidStep,
			["step2"] = G79AndroidStep2,
			["udid"] = udid
		};
		if (!string.IsNullOrWhiteSpace(realName))
		{
			authenticationPayload["realname"] = realName;
		}

		string engineVersion = await GetEngineVersionAsync();
		string patchVersion = await GetPatchVersionAsync();
		Dictionary<string, object> devicePayload = new Dictionary<string, object>
		{
			["app_channel"] = appChannel,
			["app_ver"] = patchVersion,
			["core_num"] = "u0004",
			["cpu_digit"] = "64",
			["cpu_hz"] = "2465600",
			["cpu_name"] = "placeholder",
			["device_height"] = "900",
			["device_model"] = "SAMSUNG#SM-G977N",
			["device_width"] = "1600",
			["disk"] = string.Empty,
			["emulator"] = emulator,
			["first_udid"] = udid,
			["is_guest"] = isGuest,
			["launcher_type"] = "PE_C++",
			["mac_addr"] = macAddress.ToUpperInvariant(),
			["network"] = "mm_10086",
			["os_name"] = "android",
			["os_ver"] = "5.1.1",
			["ram"] = ram,
			["rom"] = rom,
			["root"] = false,
			["sdk_ver"] = sdkVersion,
			["start_type"] = "default",
			["udid"] = udid
		};

		string versionMessage = $"{engineVersion}{G79LibraryHash}{patchVersion}{G79PatchHash}{seed}";
		Dictionary<string, object> peAuthenticationData = new Dictionary<string, object>
		{
			["engine_version"] = engineVersion,
			["extra_param"] = "extra",
			["message"] = versionMessage,
			["patch_version"] = patchVersion,
			["pay_channel"] = string.Empty,
			["sa_data"] = JsonSerializer.Serialize(devicePayload),
			["sauth_json"] = authenticationPayload,
			["seed"] = seed,
			["sign"] = G79HttpUtil.PeAuthSign(versionMessage, 3, 6)
		};

		byte[] encrypted = G79HttpUtil.Encrypt(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(peAuthenticationData)));
		return Convert.ToHexStringLower(encrypted);
	}

	private static Task<string> GetEngineVersionAsync()
	{
		return Task.FromResult(FixedEngineVersion);
	}

	private static Task<string> GetPatchVersionAsync()
	{
		return Task.FromResult(FixedPatchVersion);
	}

	private static string GetString(JsonElement element, string key, string fallback)
	{
		if (element.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
		{
			return value.GetString() ?? fallback;
		}
		return fallback;
	}

	private static string GetRequiredString(JsonElement element, string key)
	{
		string value = GetString(element, key, string.Empty);
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException($"G79 sauth_json is missing field {key}.");
		}
		return value;
	}

	private static bool GetBool(JsonElement element, string key)
	{
		return element.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.True;
	}

	private static int GetInt(JsonElement element, string key, int fallback)
	{
		if (!element.TryGetProperty(key, out JsonElement value))
		{
			return fallback;
		}
		if (value.ValueKind == JsonValueKind.Number)
		{
			return value.GetInt32();
		}
		return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int result) ? result : fallback;
	}

	private static string RandomHexUpper(int length)
	{
		byte[] bytes = new byte[length];
		Random.Shared.NextBytes(bytes);
		return Convert.ToHexString(bytes);
	}
}
