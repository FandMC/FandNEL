using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using FandNEL.Core.Entities.MPay;
using FandNEL.Core.Extensions;
using FandNEL.Core.Utils;
using FandNEL.Core.Utils.Exception;
using FandNEL.Core.Utils.Http;
using Serilog;

namespace FandNEL.Core.Protocol;

public class MPay : IDisposable
{
	private readonly EntityDevice _device;

	private readonly HttpWrapper _client = new HttpWrapper();

	private readonly HttpWrapper _service = new HttpWrapper("https://service.mkey.163.com");

	public readonly string Unique;

	private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private string GameId { get; }

	public string GameVersion { get; }

	public MPay(string gameId, string gameVersion)
	{
		GameId = gameId;
		GameVersion = gameVersion;
		Unique = CreateOrLoadUnique(gameId);
		_device = CreateOrLoadDeviceAsync(gameId, gameVersion).GetAwaiter().GetResult();
	}

	public void Dispose()
	{
		_client.Dispose();
		_service.Dispose();
		GC.SuppressFinalize(this);
	}

	public EntityDevice GetDevice()
	{
		return _device;
	}

	public async Task Configure(string appKey, string channel)
	{
		string query = new ParameterBuilder().Append("sub_app_key", "").Append("api_ver", "2").Append("gdpr", "0")
			.Append("app_channel", channel)
			.Append("sdk_version", "c1.0.0")
			.Append("app_key", appKey)
			.Append("device_id", Unique.ToUpper())
			.FormUrlEncode();
		(await _client.GetAsync("https://analytics.mpay.netease.com/config?" + query)).EnsureSuccessStatusCode();
	}

	private static string CreateOrLoadUnique(string gameId)
	{
		string fileName = gameId + "-guid.cds";
		byte[]? storedData = LoadFromFile(fileName);
		if (storedData != null)
		{
			return Encoding.UTF8.GetString(storedData);
		}
		return CreateUnique(fileName);
	}

	private static string CreateUnique(string fileName)
	{
		string uniqueId = Guid.NewGuid().ToString().Replace("-", "");
		SaveToFile(fileName, uniqueId);
		return uniqueId;
	}

	private async Task<EntityDevice> CreateOrLoadDeviceAsync(string gameId, string gameVersion)
	{
		string fileName = gameId + "-device.cds";
		byte[]? storedData = LoadFromFile(fileName);
		if (storedData == null)
		{
			return await CreateDeviceAsync(gameId, gameVersion, fileName);
		}
		return JsonSerializer.Deserialize<EntityDeviceResponse>(Encoding.UTF8.GetString(storedData)).EntityDevice;
	}

	private async Task<EntityDevice> CreateDeviceAsync(string gameId, string gameVersion, string fileName)
	{
		HttpResponseMessage response = await _service.PostAsync("/mpay/games/" + gameId + "/devices", BuildDeviceParams().Append("unique_id", Unique).FormUrlEncode(), "application/x-www-form-urlencoded");
		response.EnsureSuccessStatusCode();
		string responseJson = await response.Content.ReadAsStringAsync();
		SaveToFile(fileName, responseJson);
		return JsonSerializer.Deserialize<EntityDeviceResponse>(responseJson).EntityDevice;
	}

	public async Task<EntityMPayUserResponse> LoginWithEmailAsync(string email, string password)
	{
		string encryptedParameters = JsonSerializer.Serialize(new EntityUsersParameters
		{
			Password = password.EncodeMd5(),
			Unique = Unique,
			Username = email
		}, DefaultOptions).EncodeAes(_device.Key.DecodeHex()).EncodeHex();
		HttpResponseMessage response = await _service.PostAsync($"/mpay/games/{GameId}/devices/{_device.Id}/users", BuildBaseParams().Append("opt_fields", "nickname,avatar,realname_status,mobile_bind_status,mask_related_mobile,related_login_status").Append("params", encryptedParameters).Append("un", email.EncodeBase64())
			.FormUrlEncode(), "application/x-www-form-urlencoded");
		string responseJson = await response.Content.ReadAsStringAsync();
		if (!response.IsSuccessStatusCode)
		{
			EntityVerifyResponse? entityVerifyResponse = JsonSerializer.Deserialize<EntityVerifyResponse>(responseJson);
			if (entityVerifyResponse != null && entityVerifyResponse.Code == 1351)
			{
				throw new VerifyException(responseJson);
			}
			throw new Exception("Failed to login with email, response: " + responseJson);
		}
		return JsonSerializer.Deserialize<EntityMPayUserResponse>(responseJson);
	}

	public async Task<bool> SendSmsCodeAsync(string phoneNumber)
	{
		HttpResponseMessage response = await _service.PostAsync("/mpay/api/users/login/mobile/get_sms", BuildBaseParams().Append("device_id", _device.Id).Append("mobile", phoneNumber).FormUrlEncode(), "application/x-www-form-urlencoded");
		string propertyValue = await response.Content.ReadAsStringAsync();
		if (!response.IsSuccessStatusCode)
		{
			Log.Error("Failed to send sms code.");
		}
		return response.IsSuccessStatusCode;
	}

	public async Task<EntitySmsTicket?> VerifySmsCodeAsync(string phoneNumber, string code)
	{
		HttpResponseMessage response = await _service.PostAsync("/mpay/api/users/login/mobile/verify_sms", BuildBaseParams().Append("device_id", _device.Id).Append("mobile", phoneNumber).Append("smscode", code)
			.Append("up_content", "")
			.FormUrlEncode(), "application/x-www-form-urlencoded");
		string responseJson = await response.Content.ReadAsStringAsync();
		if (response.IsSuccessStatusCode)
		{
			return JsonSerializer.Deserialize<EntitySmsTicket>(responseJson);
		}
		Log.Error("Failed to verify sms code.");
		return null;
	}

	public async Task<EntityMPayUserResponse?> FinishSmsCodeAsync(string phoneNumber, string ticket)
	{
		string encodedPhoneNumber = phoneNumber.EncodeBase64();
		HttpResponseMessage response = await _service.PostAsync("/mpay/api/users/login/mobile/finish?un=" + encodedPhoneNumber, BuildBaseParams().Append("device_id", _device.Id).Append("opt_fields", "nickname,avatar,realname_status,mobile_bind_status,mask_related_mobile,related_login_status").Append("ticket", ticket)
			.FormUrlEncode(), "application/x-www-form-urlencoded");
		string responseJson = await response.Content.ReadAsStringAsync();
		if (response.IsSuccessStatusCode)
		{
			return JsonSerializer.Deserialize<EntityMPayUserResponse>(responseJson);
		}
		Log.Error("Failed to finish sms code.");
		return null;
	}

	private ParameterBuilder BuildBaseParams()
	{
		return new ParameterBuilder().Append("app_channel", "netease").Append("app_mode", "2").Append("app_type", "games")
			.Append("arch", "win_x64")
			.Append("cv", "c4.2.0")
			.Append("mcount_app_key", "EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq")
			.Append("mcount_transaction_id", "0")
			.Append("process_id", $"{Environment.ProcessId}")
			.Append("sv", "10.0.22621")
			.Append("updater_cv", "c1.0.0")
			.Append("game_id", GameId)
			.Append("gv", GameVersion);
	}

	private ParameterBuilder BuildDeviceParams()
	{
		return BuildBaseParams().Append("brand", "Microsoft").Append("device_model", "pc_mode").Append("device_name", "PC-" + StringGenerator.GenerateRandomString(12))
			.Append("device_type", "Computer")
			.Append("init_urs_device", "0")
			.Append("mac", StringGenerator.GenerateRandomMacAddress())
			.Append("resolution", "1920x1080")
			.Append("system_name", "windows")
			.Append("system_version", "10.0.22621");
	}

	public static void SaveToFile(string filename, string content)
	{
		File.WriteAllText(filename, content);
	}

	public static byte[]? LoadFromFile(string filename)
	{
		try
		{
			if (!File.Exists(filename))
			{
				return null;
			}
			using FileStream fileStream = new FileStream(filename, FileMode.Open);
			byte[] data = new byte[fileStream.Length];
			fileStream.ReadExactly(data, 0, data.Length);
			return data;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
