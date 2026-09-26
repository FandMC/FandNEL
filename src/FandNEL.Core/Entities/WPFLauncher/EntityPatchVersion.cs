using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher;

public class EntityPatchVersion
{
	[JsonPropertyName("size")]
	public long Size { get; set; }

	[JsonPropertyName("url")]
	public string Url { get; set; } = "";


	[JsonPropertyName("md5")]
	public string Md5 { get; set; } = "";

}
