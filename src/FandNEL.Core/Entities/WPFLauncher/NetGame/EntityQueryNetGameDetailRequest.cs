using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame;

public class EntityQueryNetGameDetailRequest
{
	[JsonPropertyName("item_id")]
	public required string ItemId { get; set; }
}
