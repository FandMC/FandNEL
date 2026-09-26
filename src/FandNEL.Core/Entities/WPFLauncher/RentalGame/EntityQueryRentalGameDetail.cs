using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.RentalGame;

public class EntityQueryRentalGameDetail
{
	[JsonPropertyName("server_id")]
	public string ServerId { get; set; } = string.Empty;

}
