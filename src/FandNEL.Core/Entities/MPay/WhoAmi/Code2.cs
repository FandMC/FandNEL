using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.MPay.WhoAmi;

public class Code2
{
	[JsonPropertyName("iso_code")]
	public string IsoCode { get; set; }

	[JsonPropertyName("names")]
	public Names Names { get; set; }
}
