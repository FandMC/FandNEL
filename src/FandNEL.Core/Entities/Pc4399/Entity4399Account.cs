using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Pc4399;

public record Entity4399Account
{
	[JsonPropertyName("account")]
	public string Account { get; set; } = string.Empty;


	[JsonPropertyName("password")]
	public string Password { get; set; } = string.Empty;

}
