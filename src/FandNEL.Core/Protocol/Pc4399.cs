using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FandNEL.Core.Entities.Pc4399;
using FandNEL.Core.Utils;
using FandNEL.Core.Utils.Exception;
using FandNEL.Core.Utils.Http;

namespace FandNEL.Core.Protocol;

public class Pc4399 : IDisposable
{
	private const string AppId = "kid_wdsj";

	private const string GameUrl = "https://cdn.h5wan.4399sj.com/microterminal-h5-frame?game_id=500352";

	private readonly CookieContainer _cookieContainer = new CookieContainer();

	private readonly HttpWrapper _login;

	private readonly MgbSdk _mgbSdk = new MgbSdk("x19");

	private readonly HttpWrapper _service = new HttpWrapper("https://microgame.5054399.net");

	public MPay MPay { get; }

	public Pc4399()
	{
		MPay = new MPay("aecfrxodyqaaaajp-g-x19", WPFLauncher.GetLatestVersionAsync().GetAwaiter().GetResult());
		_login = new HttpWrapper("https://ptlogin.4399.com", null, new HttpClientHandler
		{
			CookieContainer = _cookieContainer,
			UseCookies = true,
			AllowAutoRedirect = true
		});
	}

	public void Dispose()
	{
		MPay.Dispose();
		_mgbSdk.Dispose();
		_login.Dispose();
		_cookieContainer.GetAllCookies().Clear();
		GC.SuppressFinalize(this);
	}

	public async Task<string> LoginWithPasswordAsync(string username, string password, string? captchaIdentifier, string? captcha)
	{
		bool isInitialAttempt = captchaIdentifier == null && captcha == null;
		ParameterBuilder parameter = BuildParametersLogin().Append("username", username).Append("password", password);
		if (isInitialAttempt && (await (await _login.PostAsync("/ptlogin/loginFrame.do?v=1", parameter.FormUrlEncode(), "application/x-www-form-urlencoded")).Content.ReadAsStringAsync()).Contains("账号异常，请输入验证码"))
		{
			throw new CaptchaException("^Captcha required^");
		}
		if (captchaIdentifier != null && captcha != null)
		{
			parameter.Append("sessionId", captchaIdentifier).Append("inputCaptcha", captcha);
		}
		if (!(await _login.PostAsync("/ptlogin/login.do?v=1", parameter.FormUrlEncode(), "application/x-www-form-urlencoded")).IsSuccessStatusCode)
		{
			throw new Exception("Login to Pc499 failed");
		}
		return await GenerateCookie();
	}

	private async Task<string> GenerateCookie()
	{
		long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		HttpResponseMessage response = await _login.GetAsync("/ptlogin/checkKidLoginUserCookie.do?" + new ParameterBuilder().Append("appId", "kid_wdsj").Append("gameUrl", "https://cdn.h5wan.4399sj.com/microterminal-h5-frame?game_id=500352").Append("isCrossDomain", "1")
			.Append("nick", "null")
			.Append("onLineStart", "false")
			.Append("ptLogin", "true")
			.Append("rand_time", "$randTime")
			.Append("retUrl", $"https://ptlogin.4399.com/resource/ucenter.html?action=login&appId=kid_wdsj&loginLevel=8&regLevel=8&bizId=2201001794&externalLogin=qq&qrLogin=true&layout=vertical&level=101&css=https://microgame.5054399.net/v2/resource/cssSdk/default/login.css&v=2018_11_26_16&postLoginHandler=redirect&checkLoginUserCookie=true&redirectUrl=http%3A%2F%2Fcdn.h5wan.4399sj.com%2Fmicroterminal-h5-frame%3Fgame_id%3D500352%26rand_time%3D{timestamp}")
			.Append("show", "1")
			.FormUrlEncode());
		if (response.RequestMessage == null || response.RequestMessage.RequestUri == null)
		{
			throw new Exception("Login to Pc499 failed");
		}
		string requestUri = response.RequestMessage.RequestUri.ToString();
		int queryStart = requestUri.LastIndexOf('?') + 1;
		string parameter = requestUri.Substring(queryStart);
		ParameterBuilder parameterBuilder = await GetUniAuth(parameter);
		return _mgbSdk.GenerateSAuth(MPay.Unique, parameterBuilder.Get("username"), parameterBuilder.Get("uid"), parameterBuilder.Get("token"), parameterBuilder.Get("time"), "4399pc");
	}

	private async Task<ParameterBuilder> GetUniAuth(string parameter)
	{
		long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		string callback = $"jQuery1830{StringGenerator.GenerateRandomString(16, includeNumbers: true, includeUppercase: false, includeLowercase: false)}_{timestamp}";
		HttpResponseMessage response = await _service.GetAsync("/v2/service/sdk/info?" + new ParameterBuilder().Append("callback", callback).Append("queryStr", parameter).FormUrlEncode());
		if (!response.IsSuccessStatusCode)
		{
			throw new Exception("Get Uni-auth failed");
		}
		string responseText = await response.Content.ReadAsStringAsync();
		int jsonStart = responseText.IndexOf(callback + "(", StringComparison.Ordinal) + callback.Length + 1;
		return new ParameterBuilder((JsonSerializer.Deserialize<Entity4399Response>(responseText.Substring(jsonStart, responseText.Length - 1 - jsonStart)) ?? throw new Exception("Get Uni-auth failed")).Data.SdkLoginData);
	}

	private static ParameterBuilder BuildParametersLogin()
	{
		return new ParameterBuilder().Append("appId", AppId).Append("autoLogin", "on").Append("bizId", "2201001794")
			.Append("css", "https://microgame.5054399.net/v2/resource/cssSdk/default/login.css")
			.Append("displayMode", "popup")
			.Append("externalLogin", "qq")
			.Append("gameId", "wd")
			.Append("iframeId", "popup_login_frame")
			.Append("includeFcmInfo", "false")
			.Append("layout", "vertical")
			.Append("layoutSelfAdapting", "true")
			.Append("level", "8")
			.Append("loginFrom", "uframe")
			.Append("mainDivId", "popup_login_div")
			.Append("postLoginHandler", "default")
			.Append("redirectUrl", "")
			.Append("regLevel", "8")
			.Append("sec", "1")
			.Append("sessionId", "")
			.Append("userNameLabel", "4399用户名")
			.Append("userNameTip", "请输入4399用户名")
			.Append("welcomeTip", "欢迎回到4399");
	}
}
