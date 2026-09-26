using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame.Skin;

public class EntitySkinSettings
{
	[JsonPropertyName("client_type")]
	public required string ClientType { get; set; }

	[JsonPropertyName("game_type")]
	public required int GameType { get; set; }

	[JsonPropertyName("skin_id")]
	public required string SkinId { get; set; }

	[JsonPropertyName("skin_mode")]
	public required int SkinMode { get; set; }

	[JsonPropertyName("skin_type")]
	public required int SkinType { get; set; }
}
