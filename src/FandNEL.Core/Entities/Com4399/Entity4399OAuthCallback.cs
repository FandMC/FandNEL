using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Com4399;

public class Entity4399OAuthCallback
{
	[JsonPropertyName("code")]
	public string Code { get; set; } = string.Empty;

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("result")]
	public string Result { get; set; } = string.Empty;
}
