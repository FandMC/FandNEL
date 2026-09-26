using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Com4399;

public class Entity4399UserInfoResponse
{
	[JsonPropertyName("code")]
	public string Code { get; set; } = string.Empty;


	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;


	[JsonPropertyName("result")]
	public Entity4399UserInfoResult? Result { get; set; } = new Entity4399UserInfoResult();

}
