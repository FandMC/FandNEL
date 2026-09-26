using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79;

public class EntitySetNickName
{
	[JsonPropertyName("name")]
	public required string Name { get; set; }
}
