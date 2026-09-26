using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.NetGame;

public class EntityNetGameRequest
{
	[JsonPropertyName("version")]
	public required string Version { get; set; }

	[JsonPropertyName("channel_id")]
	public required int ChannelId { get; set; }
}
