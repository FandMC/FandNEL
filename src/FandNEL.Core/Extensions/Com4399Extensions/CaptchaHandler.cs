using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FandNEL.Core.Utils.Http;
using FandNEL.Core.Services;
using Serilog;

namespace FandNEL.Core.Extensions.Com4399Extensions;

public static class CaptchaHandler
{
	private static TaskCompletionSource<string>? _captchaTaskCompletionSource;

	private static CancellationTokenSource? _cancellationTokenSource;

	public static string BackgroundImageBase64 { get; private set; } = "";

	public static string SliderImageBase64 { get; private set; } = "";

	public static string ClickableText { get; private set; } = "";

	public static string CurrentCaptchaType { get; private set; } = "jigsaw";

	public static void SetCaptchaResult(string data)
	{
		_captchaTaskCompletionSource?.SetResult(data);
	}

	private static async Task<string> WaitForCaptchaCompletionAsync(int timeoutSeconds = 300)
	{
		_captchaTaskCompletionSource = new TaskCompletionSource<string>();
		_cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
		_cancellationTokenSource.Token.Register(() =>
		{
			_captchaTaskCompletionSource?.TrySetCanceled();
		});
		try
		{
			return await _captchaTaskCompletionSource.Task;
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException($"Captcha verification timeout after {timeoutSeconds} seconds");
		}
	}

	private static void ResetCaptchaState()
	{
		BackgroundImageBase64 = "";
		SliderImageBase64 = "";
		ClickableText = "";
	}

	public static async Task<string> HandleLoginCaptchaAsync(string resultJson)
	{
		if (string.IsNullOrEmpty(resultJson))
		{
			Log.Information("Result JSON is null or empty");
			return resultJson;
		}
		using JsonDocument document = JsonDocument.Parse(resultJson);
		JsonElement rootElement = document.RootElement;
		if (!rootElement.TryGetProperty("code", out JsonElement codeElement) || codeElement.GetString() != "103")
		{
			return resultJson;
		}

		string? captchaUrl = null;
		if (rootElement.TryGetProperty("result", out JsonElement resultElement) && resultElement.TryGetProperty("url", out JsonElement urlElement))
		{
			captchaUrl = urlElement.GetString();
		}
		if (string.IsNullOrEmpty(captchaUrl))
		{
			Log.Information("Captcha URL is null or empty");
			return resultJson;
		}

		if (captchaUrl.Contains("jigsaw"))
		{
			CurrentCaptchaType = "jigsaw";
			resultJson = await HandleJigsawCaptchaAsync(captchaUrl);
		}
		else if (captchaUrl.Contains("click"))
		{
			CurrentCaptchaType = "click";
			resultJson = await HandleClickCaptchaAsync(captchaUrl);
		}
		else
		{
			Log.Information("Unknown captcha type");
		}

		return resultJson;
	}

	private static async Task<string> HandleJigsawCaptchaAsync(string captchaUrl)
	{
		using HttpWrapper wrapper = new HttpWrapper(captchaUrl);
		string responseJson = await (await wrapper.GetAsync("")).Content.ReadAsStringAsync();
		if (string.IsNullOrEmpty(responseJson))
		{
			Log.Information("Captcha response is null or empty");
			return "";
		}
		using JsonDocument document = JsonDocument.Parse(responseJson);
		JsonElement rootElement = document.RootElement;
		BackgroundImageBase64 = "";
		SliderImageBase64 = "";
		string captchaId = "";
		if (rootElement.TryGetProperty("result", out JsonElement resultElement))
		{
			if (resultElement.TryGetProperty("img", out JsonElement backgroundElement))
			{
				BackgroundImageBase64 = backgroundElement.GetString() ?? "";
			}
			if (resultElement.TryGetProperty("img2", out JsonElement sliderElement))
			{
				SliderImageBase64 = sliderElement.GetString() ?? "";
			}
			if (resultElement.TryGetProperty("captchaId", out JsonElement captchaIdElement))
			{
				captchaId = captchaIdElement.GetString() ?? "";
			}
		}
		CurrentCaptchaType = "jigsaw";
		if (string.IsNullOrEmpty(BackgroundImageBase64) || string.IsNullOrEmpty(SliderImageBase64))
		{
			Log.Information("Captcha images are null or empty");
			return "";
		}
		CaptchaHttpServer server = await StartCaptchaServerAsync();
		try
		{
			string captchaSolution = await WaitForCaptchaCompletionAsync();
			string checkUrl = "https://m.4399api.com/captcha/jigsaw-check.html?refer=sdk&v=" + UrlEncoder.Default.Encode(captchaSolution) + "&captchaId=" + UrlEncoder.Default.Encode(captchaId);
			using HttpWrapper checkWrapper = new HttpWrapper(checkUrl);
			string checkJson = await (await checkWrapper.GetAsync("")).Content.ReadAsStringAsync();
			if (string.IsNullOrEmpty(checkJson))
			{
				Log.Information("Check response is null or empty");
				return "";
			}
			using JsonDocument checkDocument = JsonDocument.Parse(checkJson);
			JsonElement checkRoot = checkDocument.RootElement;
			if (!checkRoot.TryGetProperty("code", out JsonElement checkCodeElement) || checkCodeElement.GetInt32() != 100)
			{
				Log.Information("Captcha check failed, retrying");
				return await HandleJigsawCaptchaAsync(captchaUrl);
			}
			string token = "";
			if (checkRoot.TryGetProperty("result", out JsonElement checkResultElement) && checkResultElement.TryGetProperty("token", out JsonElement tokenElement))
			{
				token = tokenElement.GetString() ?? "";
			}
			return BuildCaptchaParameter(token, captchaId);
		}
		finally
		{
			server.Stop();
			ResetCaptchaState();
		}
	}

	private static async Task<string> HandleClickCaptchaAsync(string captchaUrl)
	{
		using HttpWrapper wrapper = new HttpWrapper(captchaUrl);
		string responseJson = await (await wrapper.GetAsync("")).Content.ReadAsStringAsync();
		if (string.IsNullOrEmpty(responseJson))
		{
			Log.Information("Captcha response is null or empty");
			return "";
		}
		using JsonDocument document = JsonDocument.Parse(responseJson);
		JsonElement rootElement = document.RootElement;
		BackgroundImageBase64 = "";
		ClickableText = "";
		string captchaId = "";
		if (rootElement.TryGetProperty("result", out JsonElement resultElement))
		{
			if (resultElement.TryGetProperty("img", out JsonElement backgroundElement))
			{
				BackgroundImageBase64 = backgroundElement.GetString() ?? "";
			}
			if (resultElement.TryGetProperty("text", out JsonElement textElement))
			{
				ClickableText = textElement.GetString() ?? "";
			}
			if (resultElement.TryGetProperty("captchaId", out JsonElement captchaIdElement))
			{
				captchaId = captchaIdElement.GetString() ?? "";
			}
		}
		CurrentCaptchaType = "click";
		if (string.IsNullOrEmpty(BackgroundImageBase64) || string.IsNullOrEmpty(ClickableText))
		{
			Log.Information("Captcha image or click text is null or empty");
			return "";
		}
		CaptchaHttpServer server = await StartCaptchaServerAsync();
		try
		{
			string captchaSolution = await WaitForCaptchaCompletionAsync();
			string checkUrl = "https://m.4399api.com/captcha/click-check.html?refer=sdk&v=" + UrlEncoder.Default.Encode(captchaSolution) + "&captchaId=" + UrlEncoder.Default.Encode(captchaId);
			using HttpWrapper checkWrapper = new HttpWrapper(checkUrl);
			string checkJson = await (await checkWrapper.GetAsync("")).Content.ReadAsStringAsync();
			if (string.IsNullOrEmpty(checkJson))
			{
				Log.Information("Check response is null or empty");
				return "";
			}
			using JsonDocument checkDocument = JsonDocument.Parse(checkJson);
			JsonElement checkRoot = checkDocument.RootElement;
			if (!checkRoot.TryGetProperty("code", out JsonElement checkCodeElement) || checkCodeElement.GetInt32() != 100)
			{
				Log.Information("Captcha check failed, retrying");
				return await HandleClickCaptchaAsync(captchaUrl);
			}
			string token = "";
			if (checkRoot.TryGetProperty("result", out JsonElement checkResultElement) && checkResultElement.TryGetProperty("token", out JsonElement tokenElement))
			{
				token = tokenElement.GetString() ?? "";
			}
			return BuildCaptchaParameter(token, captchaId);
		}
		finally
		{
			server.Stop();
			ResetCaptchaState();
		}
	}

	private static string BuildCaptchaParameter(string token, string captchaId)
	{
		return JsonSerializer.Serialize(new Dictionary<string, string>
		{
			{ "v_token", token },
			{ "captcha_id", captchaId },
			{ "type", "0" }
		});
	}

	private static async Task<CaptchaHttpServer> StartCaptchaServerAsync()
	{
		int port = NetworkUtil.GetAvailablePort();
		while (true)
		{
			try
			{
				CaptchaHttpServer server = new CaptchaHttpServer(port);
				await server.StartAsync();
				Log.Information("Captcha HTTP server started on port {Port}", port);
				string fileName = $"http://127.0.0.1:{port}/";
				Process.Start(new ProcessStartInfo
				{
					FileName = fileName,
					UseShellExecute = true
				});
				return server;
			}
			catch (HttpListenerException)
			{
				port = NetworkUtil.GetAvailablePort();
			}
		}
	}
}
