using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79;

public class EntitySetNickNameRequest
{
	[JsonPropertyName("name")]
	public required string Name { get; set; }

	[JsonPropertyName("entity_id")]
	public required string EntityId { get; set; }
}
