using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.NetGame;

public class EntityNetGameServerAddressRequest
{
	[JsonPropertyName("item_id")]
	public required string ItemId { get; set; }
}
