using System;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame.Skin;

public class EntitySkinItems
{
	[JsonPropertyName("item_ids")]
	public string[] ItemIds { get; set; } = Array.Empty<string>();
}
