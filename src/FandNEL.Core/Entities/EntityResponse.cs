using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities;

public class EntityResponse
{
	[JsonPropertyName("code")]
	public int Code { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

}
