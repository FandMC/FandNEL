using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using FandNEL.Core.Entities.MPay.WhoAmi;
using FandNEL.Core.Utils.Http;

namespace FandNEL.Core.Protocol;

public static class InternalQuery
{
	private static readonly HttpWrapper Client = new HttpWrapper();
	private static readonly object Sync = new object();

	private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private static string _gw = "";
	private static EntityWhoAmi _whoAmi = new EntityWhoAmi();
	private static bool _initialized;

	public static string Gw
	{
		get
		{
			EnsureInitialized();
			return _gw;
		}
	}

	public static EntityWhoAmi WhoAmi
	{
		get
		{
			EnsureInitialized();
			return _whoAmi;
		}
	}

	public static void Initialize()
	{
		EnsureInitialized();
	}

	private static void EnsureInitialized()
	{
		if (_initialized) return;
		lock (Sync)
		{
			if (_initialized) return;

			EntityWhoAmi whoAmi = GetWhoAmi().GetAwaiter().GetResult();
			if (whoAmi == null || String.IsNullOrWhiteSpace(whoAmi.Payload))
			{
				throw new InvalidDataException("网易 whoami 响应缺少有效 payload。");
			}

			string gw = GetGw().GetAwaiter().GetResult();
			if (String.IsNullOrWhiteSpace(gw))
			{
				throw new InvalidDataException("网易内部查询响应缺少 gw。");
			}

			_whoAmi = whoAmi;
			_gw = gw;
			_initialized = true;
		}
	}

	public static string ToAimInfo()
	{
		EnsureInitialized();
		GeoLocationData geoLocationData;
		try
		{
			geoLocationData = JsonSerializer.Deserialize<GeoLocationData>(Convert.FromBase64String(_whoAmi.Payload))
				?? throw new InvalidDataException("网易 whoami payload 不是有效的地理信息 JSON。");
		}
		catch (FormatException exception)
		{
			throw new InvalidDataException("网易 whoami payload 不是有效的 Base64 数据。", exception);
		}
		return JsonSerializer.Serialize(new EntityAimInfo
		{
			Code1 = geoLocationData.Code1,
			Code2 = geoLocationData.Code2,
			Code3 = geoLocationData.Code3,
			Code4 = geoLocationData.Code4,
			Isp = geoLocationData.Isp,
			Aim = geoLocationData.Ip
		}, DefaultOptions);
	}

	private static async Task<string> GetGw()
	{
		HttpResponseMessage response = await Client.GetAsync("http://nstool.netease.com/internalquery", builder =>
		{
			builder.UserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
		});
		response.EnsureSuccessStatusCode();
		string responseText = await response.Content.ReadAsStringAsync();
		Dictionary<string, string> values = new Dictionary<string, string>();
		string[] lines = responseText.Split('\n');
		foreach (string line in lines)
		{
			string[] parts = line.Split('=');
			if (parts.Length == 2)
			{
				values[parts[0].Trim()] = parts[1].Trim();
			}
		}

		return values.TryGetValue("gw", out string? gw) ? gw : "";
	}

	private static async Task<EntityWhoAmi> GetWhoAmi()
	{
		HttpResponseMessage response = await Client.GetAsync("https://whoami.nie.netease.com/v6", builder =>
		{
			builder.AddHeader("X-AUTH-PRODUCT", "g0");
			builder.AddHeader("X-AUTH-TOKEN", "token.efa8zUW6sxjR");
			builder.AddHeader("X-IPDB-LOCALE", "en");
			builder.AddHeader("X-PROJECT_CODE", "x19");
		});
		response.EnsureSuccessStatusCode();
		return JsonSerializer.Deserialize<EntityWhoAmi>(await response.Content.ReadAsStringAsync(), DefaultOptions)
			?? throw new InvalidDataException("网易 whoami 响应不是有效 JSON。");
	}
}
