using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.Minecraft;

public class EntityMcDownloadVersion
{
	[JsonPropertyName("mc_version")]
	public required int McVersion { get; set; }
}
