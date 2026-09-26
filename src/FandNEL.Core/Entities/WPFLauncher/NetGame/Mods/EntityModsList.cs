using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame.Mods;

public class EntityModsList
{
	[JsonPropertyName("mods")]
	public List<EntityModsInfo> Mods { get; set; } = new List<EntityModsInfo>();

}
