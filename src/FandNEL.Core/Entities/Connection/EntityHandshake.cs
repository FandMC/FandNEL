using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Connection;

public class EntityHandshake
{
	[JsonPropertyName("handshakeBody")]
	public required string HandshakeBody { get; set; }
}
