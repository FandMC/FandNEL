using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities;

public class Entity<T> : EntityResponse
{
	[JsonPropertyName("details")]
	public string Details { get; set; } = string.Empty;


	[JsonPropertyName("entity")]
	public T? Data { get; set; }
}
