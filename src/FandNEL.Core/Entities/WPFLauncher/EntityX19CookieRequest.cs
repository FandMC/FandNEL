using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher;

public class EntityX19CookieRequest
{
	[JsonPropertyName("sauth_json")]
	public required string Json { get; set; }
}
