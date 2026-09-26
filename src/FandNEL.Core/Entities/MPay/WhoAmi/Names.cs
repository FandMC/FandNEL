using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.MPay.WhoAmi;

public class Names
{
	[JsonPropertyName("en")]
	public string En { get; set; }
}
