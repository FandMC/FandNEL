using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79.RentalGame;

public class EntityRentalGameRequest
{
	[JsonPropertyName("sort_type")]
	public required int SortType { get; set; }

	[JsonPropertyName("order_type")]
	public required int OrderType { get; set; }

	[JsonPropertyName("offset")]
	public required int Offset { get; set; }
}
