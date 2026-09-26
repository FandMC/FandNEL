using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.NetGame;

public class EntityNetGame
{
	[JsonPropertyName("res")]
	public required List<EntityNetGameItem> Res { get; set; }

	[JsonPropertyName("tag")]
	public required int Tag { get; set; }

	[JsonPropertyName("campaign_id")]
	public required int CampaignId { get; set; }
}
