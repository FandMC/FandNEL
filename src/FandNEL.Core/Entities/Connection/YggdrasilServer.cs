using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Connection;

public class YggdrasilServer
{
	[JsonPropertyName("IP")]
	public string Ip { get; set; } = string.Empty;

	[JsonPropertyName("Port")]
	public int Port { get; set; }
}
