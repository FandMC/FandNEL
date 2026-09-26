using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities;

public class BodyIn
{
	[JsonPropertyName("body")]
	public required string Body { get; set; }
}
