using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.RentalGame;

public class EntityRentalGame
{
	[JsonPropertyName("entity_id")]
	public required string EntityId { get; set; }

	[JsonPropertyName("name")]
	public required string Name { get; set; }

	[JsonPropertyName("player_count")]
	public required int PlayerCount { get; set; }

	[JsonPropertyName("server_name")]
	public required string ServerName { get; set; }
}
