using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.RentalGame;

public class EntityRentalGameServerAddressRequest
{
	[JsonPropertyName("server_id")]
	public required string ServerId { get; set; }

	[JsonPropertyName("pwd")]
	public required string Password { get; set; }
}
