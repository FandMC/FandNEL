using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame;

public class EntityQueryNetGameRequest
{
	[JsonPropertyName("channel_id")]
	public string ChannelId { get; set; } = "21";


	[JsonPropertyName("entity_ids")]
	public required string[] EntityIds { get; set; }

	[JsonPropertyName("is_has")]
	public bool IsHas { get; set; }

	[JsonPropertyName("with_price")]
	public int WithPrice { get; set; }

	[JsonPropertyName("with_title_image")]
	public int WithTitleImage { get; set; } = 1;

}
