using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Connection.G79;

public class EntityZkpGetCheckNum
{
	[JsonPropertyName("body")]
	public required string Body { get; set; }
}
