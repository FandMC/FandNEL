using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.RPC;

public class EntityCheckPlayerMessage
{
	[JsonPropertyName("a")]
	public required int Length { get; set; }

	[JsonPropertyName("b")]
	public required string Message { get; set; }
}
