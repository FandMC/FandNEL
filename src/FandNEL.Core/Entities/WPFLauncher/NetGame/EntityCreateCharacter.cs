using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame;

public class EntityCreateCharacter
{
	[JsonPropertyName("game_id")]
	public required string GameId { get; set; }

	[JsonPropertyName("game_type")]
	public int GameType { get; set; } = 2;


	[JsonPropertyName("user_id")]
	public required string UserId { get; set; }

	[JsonPropertyName("name")]
	public required string Name { get; set; }

	[JsonPropertyName("create_time")]
	public int CreateTime { get; set; } = 555555;


	[JsonPropertyName("expire_time")]
	public int ExpireTime { get; set; }
}
