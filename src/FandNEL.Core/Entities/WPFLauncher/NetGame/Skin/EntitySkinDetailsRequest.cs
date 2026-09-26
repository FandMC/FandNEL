using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame.Skin;

public class EntitySkinDetailsRequest
{
	[JsonPropertyName("channel_id")]
	public required string ChannelId { get; set; }

	[JsonPropertyName("entity_ids")]
	public required List<string> EntityIds { get; set; }

	[JsonPropertyName("is_has")]
	public required int IsHas { get; set; }

	[JsonPropertyName("with_price")]
	public required int WithPrice { get; set; }

	[JsonPropertyName("with_title_image")]
	public required int WithTitleImage { get; set; }
}
