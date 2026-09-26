using System;
using System.Collections.Generic;
using System.Linq;

namespace FandNEL.Core.Utils.Http;

public class ParameterBuilder
{
	private readonly Dictionary<string, string> _parameters = new Dictionary<string, string>();

	public string? Url { get; set; }

	public ParameterBuilder()
	{
	}

	public ParameterBuilder(string parameter)
	{
		if (parameter.Contains('?'))
		{
			int queryStart = parameter.IndexOf('?');
			Url = parameter.Substring(0, queryStart);
			parameter = parameter.Substring(queryStart + 1);
		}

		string[] parameters = parameter.Split('&');
		foreach (string item in parameters)
		{
			string[] parts = item.Split('=');
			if (parts.Length == 2)
			{
				_parameters.Add(parts[0], parts[1]);
			}
		}
	}

	public string Get(string parameter)
	{
		if (!_parameters.TryGetValue(parameter, out string value))
		{
			return string.Empty;
		}
		return value;
	}

	public ParameterBuilder Append(string key, string value)
	{
		_parameters[key] = value;
		return this;
	}

	public ParameterBuilder Remove(string key)
	{
		_parameters.Remove(key);
		return this;
	}

	public string FormUrlEncode()
	{
		IEnumerable<string> values = _parameters.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> p) => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value));
		return string.Join("&", values);
	}

	public string ToQueryUrl()
	{
		return Url + "?" + FormUrlEncode();
	}

	public override string ToString()
	{
		return _parameters.Aggregate<KeyValuePair<string, string>, string>(string.Empty, (string current, KeyValuePair<string, string> kv) => ((current == string.Empty) ? current : (current + "&")) + kv.Key + "=" + kv.Value);
	}
}
