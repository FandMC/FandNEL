using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Com4399;

public class Entity4399VipInfo
{
	[JsonPropertyName("level")]
	public int Level { get; set; }

	[JsonPropertyName("score")]
	public int Score { get; set; }
}
