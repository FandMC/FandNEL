using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79;

public class EntityUserDetails
{
	[JsonPropertyName("name")]
	public required string Name { get; set; }
}
