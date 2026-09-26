using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.RentalGame;

public class EntityQueryRentalGameById
{
	[JsonPropertyName("offset")]
	public ulong Offset { get; set; }

	[JsonPropertyName("sort_type")]
	public EnumSortType SortType { get; set; }

	[JsonPropertyName("world_name_key")]
	public List<string> WorldNameKey { get; set; } = new List<string>();

}
