using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.MPay.WhoAmi;

public class Isp
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("names")]
	public Names Names { get; set; }
}
