using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame;

public class EntitySearchByIdsQuery
{
	[JsonPropertyName("item_id_list")]
	public required List<ulong> ItemIdList { get; set; }
}
