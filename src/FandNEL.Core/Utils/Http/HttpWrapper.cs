using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FandNEL.Core.Utils.Http;

public class HttpWrapper : IDisposable
{
	public class HttpWrapperBuilder
	{
		private readonly Dictionary<string, string> _headers;

		public string Domain { get; }

		public string Url { get; }

		public string Body { get; }

		public HttpWrapperBuilder(string domain, string url, string body)
		{
			Domain = domain;
			Url = url;
			Body = body;
			_headers = new Dictionary<string, string>();
		}

		public HttpWrapperBuilder AddHeader(Dictionary<string, string> headers)
		{
			foreach (KeyValuePair<string, string> header in headers)
			{
				_headers.Add(header.Key, header.Value);
			}
			return this;
		}

		public HttpWrapperBuilder AddHeader(string key, string value)
		{
			_headers.Add(key, value);
			return this;
		}

		public HttpWrapperBuilder UserAgent(string userAgent)
		{
			_headers.Add("User-Agent", userAgent);
			return this;
		}

		public Dictionary<string, string> GetHeaders()
		{
			return _headers;
		}
	}

	private readonly HttpClient _httpClient;

	private readonly string _domain;

	private readonly Action<HttpWrapperBuilder>? _extension;

	private readonly Version? _version;

	public HttpWrapper(string domain = "", Action<HttpWrapperBuilder>? extension = null, HttpClientHandler? handler = null, Version? version = null)
	{
		_domain = domain;
		_extension = extension;
		_version = version;
		_httpClient = new HttpClient(handler ?? new HttpClientHandler());
	}

	public void Dispose()
	{
		_httpClient.Dispose();
		GC.SuppressFinalize(this);
	}

	public HttpClient GetClient()
	{
		return _httpClient;
	}

	public async Task<HttpResponseMessage> PostAsync(string url, string body, Action<HttpWrapperBuilder> block)
	{
		return await PostAsync(url, body, "application/json", block);
	}

	public async Task<HttpResponseMessage> PostAsync(string url, string body, string contentType = "application/json", Action<HttpWrapperBuilder>? block = null)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _domain + url);
		if (_version != null)
		{
			request.Version = _version;
		}
		request.Content = new StringContent(body, Encoding.UTF8, contentType);
		if (block == null)
		{
			return await _httpClient.SendAsync(request);
		}
		HttpWrapperBuilder httpWrapperBuilder = new HttpWrapperBuilder(_domain, url, body);
		_extension?.Invoke(httpWrapperBuilder);
		block(httpWrapperBuilder);
		foreach (KeyValuePair<string, string> header in httpWrapperBuilder.GetHeaders())
		{
			request.Headers.Add(header.Key, header.Value);
		}
		return await _httpClient.SendAsync(request);
	}

	public async Task<HttpResponseMessage> PostAsync(string url, byte[] body, Action<HttpWrapperBuilder>? block = null)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _domain + url);
		if (_version != null)
		{
			request.Version = _version;
		}
		request.Content = new ByteArrayContent(body);
		if (block == null)
		{
			return await _httpClient.SendAsync(request);
		}
		HttpWrapperBuilder httpWrapperBuilder = new HttpWrapperBuilder(_domain, url, Encoding.UTF8.GetString(body));
		_extension?.Invoke(httpWrapperBuilder);
		block(httpWrapperBuilder);
		foreach (KeyValuePair<string, string> header in httpWrapperBuilder.GetHeaders())
		{
			request.Headers.Add(header.Key, header.Value);
		}
		return await _httpClient.SendAsync(request);
	}

	public async Task<HttpResponseMessage> GetAsync(string url, Action<HttpWrapperBuilder>? block = null)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage
		{
			Method = HttpMethod.Get,
			RequestUri = new Uri(_domain + url)
		};
		if (_version != null)
		{
			httpRequestMessage.Version = _version;
		}
		if (block == null)
		{
			return await _httpClient.SendAsync(httpRequestMessage);
		}
		HttpWrapperBuilder httpWrapperBuilder = new HttpWrapperBuilder(_domain, url, "");
		_extension?.Invoke(httpWrapperBuilder);
		block(httpWrapperBuilder);
		foreach (KeyValuePair<string, string> header in httpWrapperBuilder.GetHeaders())
		{
			httpRequestMessage.Headers.Add(header.Key, header.Value);
		}
		return await _httpClient.SendAsync(httpRequestMessage);
	}
}
