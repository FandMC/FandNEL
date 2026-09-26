using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame;

public class EntityQueryNetGameItem
{
	[JsonPropertyName("title_image_url")]
	public string TitleImageUrl { get; set; } = string.Empty;

}
