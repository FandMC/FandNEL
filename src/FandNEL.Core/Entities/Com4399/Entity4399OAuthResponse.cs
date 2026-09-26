using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Com4399;

public class Entity4399OAuthResponse
{
	[JsonPropertyName("code")]
	public int Code { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;


	[JsonPropertyName("result")]
	public Entity4399OAuthResult Result { get; set; } = new Entity4399OAuthResult();

}
