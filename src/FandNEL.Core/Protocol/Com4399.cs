using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FandNEL.Core.Entities.Com4399;
using FandNEL.Core.Extensions.Com4399Extensions;
using FandNEL.Core.Utils.Http;
using FandNEL.Core.Services;
using Serilog;

namespace FandNEL.Core.Protocol;

public partial class Com4399 : IDisposable
{
	private const string Com4399File = "4399com.cds";

	private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private readonly HttpWrapper _4399Api = new HttpWrapper("https://m.4399api.com");

	private readonly Lock _lock = new Lock();

	private readonly HttpWrapper _login = new HttpWrapper("https://ptlogin.4399.com", null, new HttpClientHandler
	{
		AllowAutoRedirect = false
	});

	private readonly MgbSdk _mgbSdk = new MgbSdk("x19");

	private readonly WebNexusApi _nexus;

	private string _deviceIdentifier = string.Empty;

	private string _deviceIdentifierSm = string.Empty;

	private string _state = string.Empty;

	private string _udid = string.Empty;

	public Com4399(string nexusToken = "")
	{
		_nexus = new WebNexusApi(nexusToken);
		CreateOrLoadDeviceAsync().GetAwaiter().GetResult();
	}

	private async Task CreateOrLoadDeviceAsync()
	{
		byte[]? deviceData = MPay.LoadFromFile(Com4399File);
		Entity4399Device device = deviceData != null ? LoadDevice(deviceData) : await CreateDevice();
		if (device.DeviceState == null)
		{
			device = await CreateDevice();
		}

		_deviceIdentifier = device.DeviceIdentifier;
		_deviceIdentifierSm = device.DeviceIdentifierSm;
		_udid = device.DeviceUdid;
		_state = device.DeviceState;
	}

	private static Entity4399Device LoadDevice(byte[] data)
	{
		return JsonSerializer.Deserialize<Entity4399Device>(data);
	}

	private async Task<Entity4399Device> CreateDevice()
	{
		_deviceIdentifier = GenerateIdentifier();
		_deviceIdentifierSm = GenerateIdentifier();
		_udid = Guid.NewGuid().ToString();
		string deviceState = await OAuthDevice();
		Entity4399Device device = new Entity4399Device
		{
			DeviceIdentifier = _deviceIdentifier,
			DeviceIdentifierSm = _deviceIdentifierSm,
			DeviceUdid = _udid,
			DeviceState = deviceState
		};
		using (_lock.EnterScope())
		{
			MPay.SaveToFile(Com4399File, JsonSerializer.Serialize(device, DefaultOptions));
			return device;
		}
	}

	private async Task<string> OAuthDevice()
	{
		string body = new ParameterBuilder().Append("usernames", "").Append("top_bar", "1").Append("state", "")
			.Append("device", JsonSerializer.Serialize(new Entity4399OAuth
			{
				DeviceIdentifier = _deviceIdentifier,
				DeviceIdentifierSm = _deviceIdentifierSm,
				Udid = _udid
			}, DefaultOptions))
			.FormUrlEncode();
		HttpResponseMessage response = await _4399Api.PostAsync("/openapiv2/oauth.html", body, "application/x-www-form-urlencoded");
		response.EnsureSuccessStatusCode();
		string responseJson = await response.Content.ReadAsStringAsync();
		Entity4399OAuthResponse oauthResponse = JsonSerializer.Deserialize<Entity4399OAuthResponse>(responseJson) ?? throw new Exception("Failed to deserialize: " + responseJson);
		return new ParameterBuilder(oauthResponse.Result.LoginUrl).Get("state");
	}

	public async Task<string> LoginAndAuthorize(string username, string password, string? captcha = null, string? captchaId = null, int retry = 0)
	{
		if (retry > 5)
		{
			throw new Exception("Retry limit exceeded");
		}
		ParameterBuilder parameterBuilder = new ParameterBuilder().Append("isInputRealname", "false").Append("isValidRealname", "false").Append("sec", "1")
			.Append("password", password)
			.Append("username", username.ToLowerInvariant())
			.Append("css", "")
			.Append("show_close_button", "")
			.Append("response_type", "TOKEN")
			.Append("client_id", "40f9e9b95d6c71ba5c6e0bd14c0abeff")
			.Append("show_4399", "")
			.Append("username_history", "")
			.Append("uid", "")
			.Append("expand_ext_login_list", "")
			.Append("ref", "{\"game\":\"115716\",\"channel\":\"\"}")
			.Append("autoCreateAccount", "")
			.Append("scope", "basic")
			.Append("bizId", "2100001792")
			.Append("state", await RequestStateAsync())
			.Append("show_ext_login", "")
			.Append("reg_mode", "reg_phone")
			.Append("_d", _deviceIdentifier)
			.Append("show_back_button", "")
			.Append("auto_scroll", "")
			.Append("access_token", "")
			.Append("show_forget_password", "")
			.Append("auth_action", "ORILOGIN")
			.Append("redirect_uri", "https://m.4399api.com/openapi/oauth-callback.html?gamekey=44770&game_key=115716")
			.Append("show_topbar", "false")
			.Append("aid", "")
			.Append("cid", "");
		if (captcha != null && captchaId != null)
		{
			parameterBuilder.Append("captcha", captcha).Append("captcha_id", captchaId);
		}
		string body = parameterBuilder.FormUrlEncode();
		Log.Information("Executing loginAndAuthorize...");
		HttpResponseMessage loginResponse = await _login.PostAsync("/oauth2/loginAndAuthorize.do?channel=&sdk=op&sdk_version=3.12.2.503", body, "application/x-www-form-urlencoded");
		string loginResponseText = await loginResponse.Content.ReadAsStringAsync();
		if (loginResponseText.Contains("验证码"))
		{
			return await HandleCaptchaWithHtml(username, password, loginResponseText, retry);
		}
		Uri? redirectLocation = loginResponse.Headers.Location;
		if (redirectLocation == null)
		{
			if (loginResponseText.Contains("用户名或密码错误") || loginResponseText.Contains("密码错误"))
			{
				throw new Exception("用户名或密码错误");
			}

			string responsePreview = loginResponseText[..Math.Min(loginResponseText.Length, 200)];
			throw new Exception("Login failed: no redirect location. Response: " + responsePreview);
		}

		ParameterBuilder redirectParameters = new ParameterBuilder(redirectLocation.AbsoluteUri);
		if (captcha != null && captchaId == null)
		{
			redirectParameters.Append("captcha", captcha);
		}
		using HttpWrapper redirectClient = new HttpWrapper(redirectParameters.ToQueryUrl());
		HttpResponseMessage authorizationResponse = await redirectClient.GetAsync("");
		authorizationResponse.EnsureSuccessStatusCode();
		string authorizationResponseText = await authorizationResponse.Content.ReadAsStringAsync();
		if (authorizationResponseText.Contains("登录状态已失效，请重新登录"))
		{
			string deviceState = await OAuthDevice();
			Entity4399Device device = new Entity4399Device
			{
				DeviceIdentifier = _deviceIdentifier,
				DeviceIdentifierSm = _deviceIdentifierSm,
				DeviceUdid = _udid,
				DeviceState = deviceState
			};
			_state = deviceState;
			using (_lock.EnterScope())
			{
				MPay.SaveToFile(Com4399File, JsonSerializer.Serialize(device, DefaultOptions));
			}
			return await LoginAndAuthorize(username, password);
		}
		if (authorizationResponseText.Contains("登录成功，但账号存在异常，需要验证"))
		{
			return await LoginAndAuthorize(username, password, await CaptchaHandler.HandleLoginCaptchaAsync(authorizationResponseText), null, retry + 1);
		}
		Entity4399UserInfoResponse userInfoResponse = JsonSerializer.Deserialize<Entity4399UserInfoResponse>(authorizationResponseText);
		if (userInfoResponse == null || userInfoResponse.Code != "100" || userInfoResponse.Result == null)
		{
			throw new Exception("Failed to deserialize: " + authorizationResponseText);
		}
		Entity4399UserInfoResult result = userInfoResponse.Result;
		return _mgbSdk.GenerateSAuth(Guid.NewGuid().ToString("N").ToUpper(), "", result.Uid.ToString(), result.State, "", "4399com", "ad");
	}

	public void Dispose()
	{
		_4399Api.Dispose();
		_login.Dispose();
		_mgbSdk.Dispose();
		_nexus.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task<string> RequestStateAsync()
	{
		HttpResponseMessage response = await _4399Api.GetAsync("/openapi/oauth-callback.html?gamekey=44770&game_key=115716");
		response.EnsureSuccessStatusCode();
		string responseJson = await response.Content.ReadAsStringAsync();
		Entity4399OAuthCallback callback = JsonSerializer.Deserialize<Entity4399OAuthCallback>(responseJson)
			?? throw new Exception("Failed to deserialize OAuth callback: " + responseJson);
		string state = new ParameterBuilder(callback.Result).Get("state");
		if (string.IsNullOrEmpty(state))
		{
			throw new Exception("OAuth callback did not contain a state: " + responseJson);
		}
		return state;
	}

	private async Task<string> HandleCaptchaWithHtml(string username, string password, string html, int retry)
	{
		Match match = CaptchaRegex().Match(html);
		if (!match.Success)
		{
			throw new Exception("Cannot find captcha in html");
		}
		string matchedCaptchaId = match.Groups[1].Value;
		HttpResponseMessage response = await _login.GetAsync("/ptlogin/captcha.do?captchaId=" + matchedCaptchaId + "&xx=1");
		string captcha = _nexus.ComputeCaptchaAsync(await response.Content.ReadAsByteArrayAsync());
		return await LoginAndAuthorize(username, password, captcha, matchedCaptchaId, retry + 1);
	}

	private static string GenerateIdentifier(DateTime? dateTime = null, string? additionalData = null)
	{
		string timestamp = (dateTime ?? DateTime.Now).ToString("yyyyMMddHHmm");
		string hash = GenerateHash50(additionalData);
		return timestamp + hash;
	}

	private static string GenerateHash50(string? data = null)
	{
		if (string.IsNullOrEmpty(data))
		{
			data = Guid.NewGuid().ToString() + DateTime.Now.Ticks;
		}
		return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data))).Substring(0, 50);
	}

	[GeneratedRegex("name\\s*=\\s*[\"']captcha_id[\"']\\s+value\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase)]
	private static partial Regex CaptchaRegex();
}
