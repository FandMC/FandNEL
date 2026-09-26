using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame;

public class EntityMcVersion
{
	[JsonPropertyName("mcversionid")]
	public int McVersionId { get; set; }

	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

}
