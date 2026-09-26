using System.Text.Json.Serialization;
using FandNEL.Core.Entities.Converter;

namespace FandNEL.Core.Entities.G79;

public class Entities<T> : EntityResponse
{
	[JsonPropertyName("details")]
	public string Details { get; set; } = string.Empty;


	[JsonPropertyName("entity")]
	public T? Data { get; set; }

	[JsonPropertyName("total")]
	[JsonConverter(typeof(NetEaseStringConverter))]
	public int Total { get; set; }
}
