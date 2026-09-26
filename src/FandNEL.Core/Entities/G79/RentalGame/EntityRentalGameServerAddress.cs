using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.RentalGame;

public class EntityRentalGameServerAddress
{
	[JsonPropertyName("mcserver_host")]
	public string? Host { get; set; }

	[JsonPropertyName("mcserver_port")]
	public int? Port { get; set; }

	[JsonPropertyName("user_id")]
	public string UserId { get; set; } = string.Empty;
}
