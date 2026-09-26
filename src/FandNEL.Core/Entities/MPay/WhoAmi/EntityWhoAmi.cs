using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.MPay.WhoAmi;

public class EntityWhoAmi
{
	[JsonPropertyName("payload")]
	public string Payload { get; set; }

	[JsonPropertyName("sig")]
	public string Signature { get; set; }

	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("why")]
	public string Why { get; set; }
}
