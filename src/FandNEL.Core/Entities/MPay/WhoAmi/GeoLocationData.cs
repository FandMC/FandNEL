using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.MPay.WhoAmi;

public class GeoLocationData
{
	[JsonPropertyName("code_1")]
	public Code1 Code1 { get; set; }

	[JsonPropertyName("code_2")]
	public Code2 Code2 { get; set; }

	[JsonPropertyName("code_3")]
	public Code3 Code3 { get; set; }

	[JsonPropertyName("code_4")]
	public Code4 Code4 { get; set; }

	[JsonPropertyName("isp")]
	public Isp Isp { get; set; }

	[JsonPropertyName("ip")]
	public string Ip { get; set; }
}
