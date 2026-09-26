using System;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.G79;

public class EntityQueryUserDetail
{
	[JsonPropertyName("version")]
	public required Version Version { get; set; }
}
