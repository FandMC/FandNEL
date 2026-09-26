using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities;

public class IdCard
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;


	[JsonPropertyName("idNumber")]
	public string IdNumber { get; set; } = string.Empty;

}
