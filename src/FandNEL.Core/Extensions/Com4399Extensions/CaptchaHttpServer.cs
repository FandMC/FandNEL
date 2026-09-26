using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace FandNEL.Core.Extensions.Com4399Extensions;

public class CaptchaHttpServer
{
	private readonly HttpListener _httpListener;

	private bool _isRunning;

	public readonly int Port;

	public CaptchaHttpServer(int port)
	{
		Port = port;
		string uriPrefix = $"http://127.0.0.1:{port}/";
		_httpListener = new HttpListener();
		_httpListener.Prefixes.Add(uriPrefix);
	}

	public Task StartAsync()
	{
		if (_isRunning)
		{
			return Task.CompletedTask;
		}
		_httpListener.Start();
		_isRunning = true;
		_ = Task.Run(async () =>
		{
			while (_isRunning && _httpListener.IsListening)
			{
				try
				{
					HttpListenerContext context = await _httpListener.GetContextAsync();
					_ = Task.Run(() => HandleHttpRequest(context));
				}
				catch (HttpListenerException)
				{
					break;
				}
				catch (Exception exception)
				{
					Log.Error("Error processing request: {Message}", exception.Message);
				}
			}
		});
		return Task.CompletedTask;
	}

	public void Stop()
	{
		if (_isRunning)
		{
			_isRunning = false;
			_httpListener.Stop();
		}
	}

	private void HandleHttpRequest(HttpListenerContext context)
	{
		try
		{
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			if (request.Url == null)
			{
				response.StatusCode = 400;
				response.Close();
				return;
			}
			string path = request.Url.AbsolutePath.Trim('/').ToLowerInvariant();
			if ((path.Length == 0 || path == "index.html") && request.HttpMethod == "GET")
			{
				HandleIndexRequest(response);
				return;
			}
			if (path == "img.json" && request.HttpMethod == "GET")
			{
				HandleImageJsonRequest(response);
				return;
			}
			if (path == "resolve" && request.HttpMethod == "POST")
			{
				HandleResolveRequest(request, response);
				return;
			}

			response.StatusCode = 404;
			WriteStringToResponse(response, "Not Found");
		}
		catch (Exception ex)
		{
			try
			{
				context.Response.StatusCode = 500;
				WriteStringToResponse(context.Response, ex.ToString());
			}
			catch
			{
				Log.Error("Server Error");
			}
		}
		finally
		{
			context.Response.Close();
		}
	}

	private void HandleIndexRequest(HttpListenerResponse response)
	{
		string content = ((CaptchaHandler.CurrentCaptchaType == "jigsaw") ? CaptchaResources.JigsawCaptchaHtml : CaptchaResources.ClickCaptchaHtml);
		response.ContentType = "text/html; charset=utf-8";
		WriteStringToResponse(response, content);
	}

	private void HandleImageJsonRequest(HttpListenerResponse response)
	{
		response.ContentType = "application/json";
		Dictionary<string, string> payload = ((!(CaptchaHandler.CurrentCaptchaType == "jigsaw")) ? new Dictionary<string, string>
		{
			{
				"img",
				"data:image/png;base64," + CaptchaHandler.BackgroundImageBase64
			},
			{
				"text",
				CaptchaHandler.ClickableText
			}
		} : new Dictionary<string, string>
		{
			{
				"verificationImage",
				"data:image/png;base64," + CaptchaHandler.BackgroundImageBase64
			},
			{
				"sliderImage",
				"data:image/png;base64," + CaptchaHandler.SliderImageBase64
			}
		});
		WriteStringToResponse(response, JsonSerializer.Serialize(payload));
	}

	private void HandleResolveRequest(HttpListenerRequest request, HttpListenerResponse response)
	{
		response.ContentType = "application/json";
		using StreamReader streamReader = new StreamReader(request.InputStream, request.ContentEncoding);
		using JsonDocument jsonDocument = JsonDocument.Parse(streamReader.ReadToEnd());
		CaptchaHandler.SetCaptchaResult(jsonDocument.RootElement.GetProperty("data").GetString() ?? "");
		WriteStringToResponse(response, "{\"result\":\"resolved\"}");
		_ = Task.Delay(1000).ContinueWith(_ =>
		{
			Stop();
		});
	}

	private static void WriteStringToResponse(HttpListenerResponse response, string content)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(content);
		response.ContentLength64 = bytes.Length;
		response.OutputStream.Write(bytes, 0, bytes.Length);
	}
}
