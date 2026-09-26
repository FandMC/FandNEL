using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Pc4399;

public class Entity4399Response
{
	[JsonPropertyName("code")]
	public int Code { get; set; }

	[JsonPropertyName("msg")]
	public string Msg { get; set; } = string.Empty;


	[JsonPropertyName("data")]
	public Entity4399ResponseData Data { get; set; } = new Entity4399ResponseData();

}
