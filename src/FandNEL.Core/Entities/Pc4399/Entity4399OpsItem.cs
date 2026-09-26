using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Pc4399;

public class Entity4399OpsItem
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;


	[JsonPropertyName("link")]
	public string Link { get; set; } = string.Empty;


	[JsonPropertyName("banner")]
	public string Banner { get; set; } = string.Empty;


	[JsonPropertyName("type")]
	public int Type { get; set; }
}
