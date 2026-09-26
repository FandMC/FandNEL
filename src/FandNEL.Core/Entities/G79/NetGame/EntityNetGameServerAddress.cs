using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.NetGame;

public class EntityNetGameServerAddress
{
	[JsonPropertyName("host")]
	public required string Host { get; set; }

	[JsonPropertyName("port")]
	public required int Port { get; set; }
}
