using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79;

public class EntityAuthenticationOtp
{
	[JsonPropertyName("entity_id")]
	public required string EntityId { get; set; }

	[JsonPropertyName("token")]
	public required string Token { get; set; }
}
